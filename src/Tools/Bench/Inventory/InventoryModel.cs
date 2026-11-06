// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Serialization;

namespace VsChromium.Bench.Inventory;

/// <summary>The machine, its disks and its security software (no admin needed, except the filter drivers).</summary>
public sealed record MachineInventory {
  public MachineInfo Machine { get; init; } = new();
  public List<DiskInfo> Disks { get; init; } = [];
  public SecurityInfo Security { get; init; } = new();
}

public sealed record MachineInfo {
  public string HostName { get; init; } = "";
  /// <summary>"Windows", "Linux", "macOS".</summary>
  public string Os { get; init; } = "";
  /// <summary>Name and version, e.g. "Microsoft Windows 10.0.26100", "Ubuntu 24.04.1 LTS".</summary>
  public string OsDescription { get; init; } = "";
  public string Architecture { get; init; } = "";
  public string CpuModel { get; init; } = "";
  public int LogicalCores { get; init; }
  /// <summary>Physical cores (null if unknown).</summary>
  public int? PhysicalCores { get; init; }
  /// <summary>Base (nominal) clock of the CPU in MHz, if known.</summary>
  public int? BaseClockMHz { get; init; }
  /// <summary>Maximum (boost) clock of the CPU in MHz, if known.</summary>
  public int? MaxClockMHz { get; init; }
  public long PhysicalMemoryBytes { get; init; }
  /// <summary>Memory type (e.g. "DDR5"), if readable without admin.</summary>
  public string? MemoryType { get; init; }
  /// <summary>Memory speed in MT/s, if readable without admin.</summary>
  public int? MemorySpeedMTs { get; init; }
  public List<GpuInfo> Gpus { get; init; } = [];
  /// <summary>Windows power plan (e.g. "Balanced"), null elsewhere.</summary>
  public string? PowerPlan { get; init; }
  /// <summary>The .NET runtime of the bench.</summary>
  public string BenchRuntime { get; init; } = "";
  /// <summary>Running as administrator (Windows) or root.</summary>
  public bool IsElevated { get; init; }
}

public sealed record DiskInfo {
  /// <summary>"C:\", "/", "/mnt/data".</summary>
  public string MountPoint { get; init; } = "";
  public string? Label { get; init; }
  /// <summary>Device (Linux: /dev/nvme0n1p2; macOS: disk3s1; Windows: physical device product name).</summary>
  public string? Device { get; init; }
  public string FileSystem { get; init; } = "";
  /// <summary>Windows Dev Drive (null: unknown or not Windows).</summary>
  public bool? DevDrive { get; init; }
  /// <summary>Dev Drive trusted (antivirus in performance mode / filters not attached by default).</summary>
  public bool? DevDriveTrusted { get; init; }
  public long TotalBytes { get; init; }
  public long FreeBytes { get; init; }
  /// <summary>"NVMe", "SSD", "HDD", "Network", "Virtual disk", "Removable", "Unknown".</summary>
  public string MediaType { get; init; } = "Unknown";
  /// <summary>Bus or protocol, e.g. "NVMe", "SATA", "USB", "FileBackedVirtual".</summary>
  public string? BusType { get; init; }
  /// <summary>Windows change journal: "active", "inactive", "not supported", null if unknown.</summary>
  public string? ChangeJournal { get; init; }
  public long? ChangeJournalMaxBytes { get; init; }
  public List<string> Notes { get; init; } = [];

  [JsonIgnore]
  public string Summary =>
    $"{MountPoint} {FileSystem}{(DevDrive == true ? " (Dev Drive" + (DevDriveTrusted == true ? ", trusted)" : ")") : "")}, {MediaType}" +
    $", {FormatBytes(FreeBytes)} free of {FormatBytes(TotalBytes)}" +
    (ChangeJournal != null ? $", change journal {ChangeJournal}" : "");

  public static string FormatBytes(long bytes) =>
    bytes >= 1L << 40 ? $"{bytes / (double)(1L << 40):n1} TB" :
    bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):n1} GB" :
    bytes >= 1L << 20 ? $"{bytes / (double)(1L << 20):n1} MB" :
    $"{bytes / 1024.0:n1} KB";
}

public sealed record GpuInfo {
  public string Name { get; init; } = "";
  public string? DriverVersion { get; init; }
}

public sealed record AntivirusProduct {
  public string Name { get; init; } = "";
  /// <summary>"on", "off", "snoozed", "expired", or the raw state.</summary>
  public string State { get; init; } = "";
  public bool? UpToDate { get; init; }
}

public sealed record SecurityInfo {
  public List<AntivirusProduct> Antivirus { get; init; } = [];
  /// <summary>Why the antivirus list is not available (e.g. Security Center missing on Server SKUs).</summary>
  public string? AntivirusNote { get; init; }
  public bool? DefenderRealTimeProtection { get; init; }
  /// <summary>Defender running mode: "Normal", "Passive", "EDR Block Mode", ...</summary>
  public string? DefenderMode { get; init; }
  public string? DefenderNote { get; init; }
  /// <summary>File system filter drivers (fltmc, elevated only), null if not listed.</summary>
  public List<string>? FilterDrivers { get; init; }
  public string? FilterDriversNote { get; init; }
  /// <summary>Dev Drive state per volume (fsutil devdrv query, elevated only).</summary>
  public Dictionary<string, string>? DevDriveDetails { get; init; }
}
