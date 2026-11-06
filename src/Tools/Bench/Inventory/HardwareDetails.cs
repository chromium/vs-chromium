// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Globalization;
using System.Text.Json.Nodes;
using VsChromium.Bench.Platform;

namespace VsChromium.Bench.Inventory;

/// <summary>
/// CPU cores and clocks, memory type and speed, GPUs: only what is readable without admin. Never
/// serial numbers, UUIDs or other identifiers (the inventory goes into shareable reports).
/// </summary>
public static partial class InventoryCollector {
  public static MachineInfo AddHardwareDetails(MachineInfo machine) {
    try {
      if (OperatingSystem.IsWindows()) {
        return AddWindowsHardware(machine);
      }
      if (OperatingSystem.IsLinux()) {
        return AddLinuxHardware(machine);
      }
      if (OperatingSystem.IsMacOS()) {
        return AddMacHardware(machine);
      }
    } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException) {
      // Best effort: the details are optional.
    }
    return machine;
  }

  private static MachineInfo AddWindowsHardware(MachineInfo machine) {
    // One PowerShell process, three JSON documents separated by a marker line (no custom objects:
    // works in constrained language mode).
    var output = ProcessRunner.PowerShell(
      "Get-CimInstance Win32_Processor | Select-Object -First 1 NumberOfCores,MaxClockSpeed | ConvertTo-Json -Compress; '---'; " +
      "@(Get-CimInstance Win32_PhysicalMemory | Select-Object Speed,ConfiguredClockSpeed,SMBIOSMemoryType) | ConvertTo-Json -Compress; '---'; " +
      "@(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion) | ConvertTo-Json -Compress",
      TimeSpan.FromSeconds(30));
    if (output == null) {
      return machine;
    }
    var parts = output.Split("---");
    if (parts.Length < 3) {
      return machine;
    }
    var cpu = ParseJsonArray(parts[0]).FirstOrDefault();
    var memory = ParseJsonArray(parts[1]).ToList();
    var gpus = ParseJsonArray(parts[2])
      .Select(g => new GpuInfo { Name = ((string?)g["Name"] ?? "").Trim(), DriverVersion = (string?)g["DriverVersion"] })
      .Where(g => g.Name.Length > 0)
      .ToList();
    var speed = memory.Select(m => (int?)m["ConfiguredClockSpeed"] is > 0 and var c ? c : (int?)m["Speed"]).Where(s => s > 0).DefaultIfEmpty().Max();
    var type = memory.Select(m => MemoryTypeName((int?)m["SMBIOSMemoryType"] ?? 0)).FirstOrDefault(t => t != null);
    return machine with {
      PhysicalCores = (int?)cpu?["NumberOfCores"],
      BaseClockMHz = (int?)cpu?["MaxClockSpeed"],
      MemorySpeedMTs = speed,
      MemoryType = type,
      Gpus = gpus,
    };
  }

  /// <summary>SMBIOS memory types (SMBIOS specification, type 17).</summary>
  public static string? MemoryTypeName(int smbiosType) => smbiosType switch {
    18 => "DDR",
    19 => "DDR2",
    24 => "DDR3",
    26 => "DDR4",
    27 => "LPDDR",
    28 => "LPDDR2",
    29 => "LPDDR3",
    30 => "LPDDR4",
    34 => "DDR5",
    35 => "LPDDR5",
    _ => null,
  };

  private static MachineInfo AddLinuxHardware(MachineInfo machine) {
    // Physical cores: distinct (physical id, core id) pairs.
    var cores = new HashSet<(string, string)>();
    string physical = "0";
    foreach (var line in ReadLines("/proc/cpuinfo")) {
      var parts = line.Split(':', 2);
      if (parts.Length != 2) {
        continue;
      }
      switch (parts[0].Trim()) {
        case "physical id":
          physical = parts[1].Trim();
          break;
        case "core id":
          cores.Add((physical, parts[1].Trim()));
          break;
      }
    }
    int? KHzFile(string path) =>
      ReadLines(path).FirstOrDefault() is { } text && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var khz) ? khz / 1000 : null;
    var gpus = new List<GpuInfo>();
    var lspci = ProcessRunner.Run("lspci", [], TimeSpan.FromSeconds(10));
    if (lspci is { Success: true }) {
      foreach (var line in lspci.Output.Split('\n')) {
        var index = line.IndexOf(" controller: ", StringComparison.Ordinal);
        if (index > 0 && (line.Contains("VGA", StringComparison.Ordinal) || line.Contains("3D", StringComparison.Ordinal) || line.Contains("Display", StringComparison.Ordinal))) {
          gpus.Add(new GpuInfo { Name = line[(index + " controller: ".Length)..].Trim() });
        }
      }
    }
    return machine with {
      PhysicalCores = cores.Count > 0 ? cores.Count : null,
      BaseClockMHz = KHzFile("/sys/devices/system/cpu/cpu0/cpufreq/base_frequency"),
      MaxClockMHz = KHzFile("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq"),
      Gpus = gpus,
    };
  }

  private static MachineInfo AddMacHardware(MachineInfo machine) {
    int? Sysctl(string name) =>
      ProcessRunner.Run("sysctl", ["-n", name])?.Output.Trim() is { Length: > 0 } text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? (int)(name.Contains("frequency", StringComparison.Ordinal) ? value / 1_000_000 : value)
        : null;
    var gpus = new List<GpuInfo>();
    var displays = ProcessRunner.Run("system_profiler", ["SPDisplaysDataType", "-json"], TimeSpan.FromSeconds(30));
    if (displays is { Success: true } && JsonNode.Parse(displays.Output)?["SPDisplaysDataType"] is JsonArray items) {
      foreach (var item in items.OfType<JsonObject>()) {
        if ((string?)item["sppci_model"] is { Length: > 0 } model) {
          gpus.Add(new GpuInfo { Name = model });
        }
      }
    }
    string? memoryType = null;
    var memory = ProcessRunner.Run("system_profiler", ["SPMemoryDataType", "-json"], TimeSpan.FromSeconds(30));
    if (memory is { Success: true } && JsonNode.Parse(memory.Output)?["SPMemoryDataType"] is JsonArray memoryItems) {
      memoryType = memoryItems.OfType<JsonObject>().Select(m => (string?)m["dimm_type"]).FirstOrDefault(t => !string.IsNullOrEmpty(t));
    }
    return machine with {
      PhysicalCores = Sysctl("hw.physicalcpu"),
      MaxClockMHz = Sysctl("hw.cpufrequency_max"),
      MemoryType = memoryType,
      Gpus = gpus,
    };
  }
}
