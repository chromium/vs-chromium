// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using VsChromium.Bench.Platform;

namespace VsChromium.Bench.Inventory;

/// <summary>
/// Collects the machine and disk inventory: Windows (P/Invoke, Security Center through PowerShell),
/// Linux (/proc, /sys), macOS (sysctl, diskutil). Nothing needs admin, except the filter drivers
/// (fltmc) and the Dev Drive details (fsutil), only when elevated and accepted.
/// </summary>
public static partial class InventoryCollector {
  public static MachineInventory Collect(bool listElevatedDetails, Action<string>? log = null) {
    log ??= _ => { };
    var machine = CollectMachine();
    var disks = CollectDisks();
    var security = CollectSecurity(machine.IsElevated && listElevatedDetails, disks);
    return new MachineInventory { Machine = machine, Disks = disks, Security = security };
  }

  public static bool IsElevated() {
    if (OperatingSystem.IsWindows()) {
      return WindowsNative.IsUserAnAdmin();
    }
    return UnixNative.geteuid() == 0;
  }

  // -----------------------------------------------------------------------------------------------
  // Machine

  public static MachineInfo CollectMachine() {
    var os = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : RuntimeInformation.OSDescription;
    string? powerPlan = null;
    string cpu = "";
    var osDescription = RuntimeInformation.OSDescription;
    if (OperatingSystem.IsWindows()) {
      cpu = ReadRegistryString(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "";
      var product = ReadRegistryString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName");
      var display = ReadRegistryString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
      var build = Environment.OSVersion.Version;
      // Note: ProductName says "Windows 10" on Windows 11 (build 22000 and later).
      if (product != null && build.Build >= 22000) {
        product = product.Replace("Windows 10", "Windows 11");
      }
      osDescription = $"{product ?? "Windows"} {display} (build {build.Build}.{ReadRegistryInt(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR")})".Replace("  ", " ");
      var scheme = ProcessRunner.Run("powercfg", ["/getactivescheme"], TimeSpan.FromSeconds(10));
      if (scheme is { Success: true } && Regex.Match(scheme.Output, @"\(([^)]+)\)\s*$") is { Success: true } match) {
        powerPlan = match.Groups[1].Value;
      }
    } else if (OperatingSystem.IsLinux()) {
      cpu = ReadLines("/proc/cpuinfo").Select(l => l.Split(':', 2)).FirstOrDefault(p => p.Length == 2 && p[0].Trim() is "model name" or "Model")?[1].Trim() ?? "";
      var pretty = ReadLines("/etc/os-release").FirstOrDefault(l => l.StartsWith("PRETTY_NAME=", StringComparison.Ordinal));
      if (pretty != null) {
        osDescription = $"{pretty["PRETTY_NAME=".Length..].Trim('"')} (kernel {Environment.OSVersion.Version})";
      }
    } else if (OperatingSystem.IsMacOS()) {
      cpu = ProcessRunner.Run("sysctl", ["-n", "machdep.cpu.brand_string"])?.Output.Trim() ?? "";
      var version = ProcessRunner.Run("sw_vers", ["-productVersion"])?.Output.Trim();
      var build = ProcessRunner.Run("sw_vers", ["-buildVersion"])?.Output.Trim();
      if (version != null) {
        osDescription = $"macOS {version} ({build})";
      }
    }
    return AddHardwareDetails(new MachineInfo {
      HostName = Environment.MachineName,
      Os = os,
      OsDescription = osDescription,
      Architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
      CpuModel = cpu.Trim(),
      LogicalCores = Environment.ProcessorCount,
      PhysicalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
      PowerPlan = powerPlan,
      BenchRuntime = RuntimeInformation.FrameworkDescription + (RuntimeFeature.IsDynamicCodeSupported ? "" : " (NativeAOT)"),
      IsElevated = IsElevated(),
    });
  }

  private static class RuntimeFeature {
    public static bool IsDynamicCodeSupported => System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
  }

  [SupportedOSPlatform("windows")]
  private static string? ReadRegistryString(string key, string name) {
    using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
    return k?.GetValue(name) as string;
  }

  [SupportedOSPlatform("windows")]
  private static int ReadRegistryInt(string key, string name) {
    using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
    return k?.GetValue(name) is int value ? value : 0;
  }

  private static IEnumerable<string> ReadLines(string path) {
    try {
      return File.ReadAllLines(path);
    } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      return [];
    }
  }

  // -----------------------------------------------------------------------------------------------
  // Disks

  private static readonly HashSet<string> PseudoFileSystems = new(StringComparer.OrdinalIgnoreCase) {
    "proc", "sysfs", "devtmpfs", "devpts", "tmpfs", "cgroup", "cgroup2", "pstore", "securityfs", "debugfs", "tracefs",
    "configfs", "fusectl", "mqueue", "hugetlbfs", "bpf", "autofs", "binfmt_misc", "efivarfs", "rpc_pipefs", "nsfs",
    "squashfs", "overlay", "ramfs", "devfs", "nullfs", "fdescfs", "selinuxfs", "fuse.portal", "fuse.gvfsd-fuse",
  };

  public static List<DiskInfo> CollectDisks() {
    var result = new List<DiskInfo>();
    Dictionary<string, string>? linuxDevices = OperatingSystem.IsLinux() ? ReadLinuxMounts() : null;
    foreach (var drive in DriveInfo.GetDrives()) {
      try {
        if (!drive.IsReady || drive.TotalSize == 0) {
          continue;
        }
        if (drive.DriveType is DriveType.CDRom or DriveType.Ram or DriveType.NoRootDirectory) {
          continue;
        }
        if (!OperatingSystem.IsWindows() && (PseudoFileSystems.Contains(drive.DriveFormat) || IsSystemMount(drive.Name))) {
          continue;
        }
        var disk = new DiskInfo {
          MountPoint = drive.Name,
          Label = OperatingSystem.IsWindows() && drive.VolumeLabel.Length > 0 ? drive.VolumeLabel : null,
          FileSystem = drive.DriveFormat,
          TotalBytes = drive.TotalSize,
          FreeBytes = drive.AvailableFreeSpace,
          MediaType = drive.DriveType switch {
            DriveType.Network => "Network",
            DriveType.Removable => "Removable",
            _ => "Unknown",
          },
        };
        if (OperatingSystem.IsWindows()) {
          disk = AddWindowsDetails(disk);
        } else if (OperatingSystem.IsLinux()) {
          disk = AddLinuxDetails(disk, linuxDevices!);
        } else if (OperatingSystem.IsMacOS()) {
          disk = AddMacDetails(disk);
        }
        result.Add(disk);
      } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        // Not accessible: skipped.
      }
    }
    return result;
  }

  private static bool IsSystemMount(string mountPoint) =>
    mountPoint.StartsWith("/proc", StringComparison.Ordinal) || mountPoint.StartsWith("/sys", StringComparison.Ordinal) ||
    mountPoint.StartsWith("/dev", StringComparison.Ordinal) || mountPoint.StartsWith("/run", StringComparison.Ordinal) ||
    mountPoint.StartsWith("/snap/", StringComparison.Ordinal) || mountPoint.StartsWith("/boot/efi", StringComparison.Ordinal) ||
    (mountPoint.StartsWith("/System/Volumes/", StringComparison.Ordinal) && mountPoint != "/System/Volumes/Data") ||
    mountPoint.StartsWith("/private/var/vm", StringComparison.Ordinal);

  /// <summary>The disk of a path: the disk with the longest mount point containing it.</summary>
  public static DiskInfo? FindDisk(IReadOnlyList<DiskInfo> disks, string path) {
    var full = Path.GetFullPath(path);
    var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    return disks
      .Where(d => full.StartsWith(d.MountPoint, comparison) || full.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar == d.MountPoint)
      .OrderByDescending(d => d.MountPoint.Length)
      .FirstOrDefault();
  }

  /// <summary>Finds a disk from the user's input ("D", "D:", "D:\", "/mnt/data").</summary>
  public static DiskInfo? FindDiskByName(IReadOnlyList<DiskInfo> disks, string name) {
    var normalized = name.Trim();
    if (OperatingSystem.IsWindows() && normalized.Length is >= 1 and <= 3 && char.IsAsciiLetter(normalized[0])) {
      normalized = char.ToUpperInvariant(normalized[0]) + @":\";
    }
    var exact = disks.FirstOrDefault(d => string.Equals(d.MountPoint.TrimEnd('/', '\\'), normalized.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(d.MountPoint, normalized, StringComparison.OrdinalIgnoreCase));
    return exact ?? (Directory.Exists(normalized) ? FindDisk(disks, normalized) : null);
  }

  [SupportedOSPlatform("windows")]
  private static unsafe DiskInfo AddWindowsDetails(DiskInfo disk) {
    var notes = new List<string>();
    bool? devDrive = null, trusted = null;
    string? journal = null;
    long? journalMax = null;
    using (var root = WindowsNative.OpenForAttributes(disk.MountPoint)) {
      if (!root.IsInvalid) {
        var input = new WindowsNative.FILE_FS_PERSISTENT_VOLUME_INFORMATION { Version = 1, FlagMask = 0xFFFFFFFF };
        var output = default(WindowsNative.FILE_FS_PERSISTENT_VOLUME_INFORMATION);
        if (WindowsNative.DeviceIoControl(root, WindowsNative.FSCTL_QUERY_PERSISTENT_VOLUME_STATE, &input, (uint)sizeof(WindowsNative.FILE_FS_PERSISTENT_VOLUME_INFORMATION),
                                          &output, (uint)sizeof(WindowsNative.FILE_FS_PERSISTENT_VOLUME_INFORMATION), out _, IntPtr.Zero)) {
          devDrive = (output.VolumeFlags & WindowsNative.PERSISTENT_VOLUME_STATE_DEV_VOLUME) != 0;
          trusted = devDrive == true ? (output.VolumeFlags & WindowsNative.PERSISTENT_VOLUME_STATE_TRUSTED_VOLUME) != 0 : null;
        } else if (disk.FileSystem is "NTFS" or "FAT32" or "exFAT" or "FAT") {
          devDrive = false;
        }

        WindowsNative.USN_JOURNAL_DATA_V0 data;
        if (WindowsNative.DeviceIoControl(root, WindowsNative.FSCTL_QUERY_USN_JOURNAL, null, 0, &data, (uint)sizeof(WindowsNative.USN_JOURNAL_DATA_V0), out _, IntPtr.Zero)) {
          journal = "active";
          journalMax = (long)data.MaximumSize;
        } else {
          journal = Marshal.GetLastPInvokeError() switch {
            WindowsNative.ERROR_JOURNAL_NOT_ACTIVE => "inactive",
            WindowsNative.ERROR_JOURNAL_DELETE_IN_PROGRESS => "being deleted",
            WindowsNative.ERROR_INVALID_FUNCTION => "not supported",
            var error => $"unknown (error {error})",
          };
        }
      }
    }

    string media = disk.MediaType, device = "";
    string? bus = null;
    if (disk.MediaType != "Network") {
      using var volume = WindowsNative.CreateFile(@"\\.\" + disk.MountPoint.TrimEnd('\\'), 0, WindowsNative.FILE_SHARE_ALL, IntPtr.Zero, WindowsNative.OPEN_EXISTING, 0, IntPtr.Zero);
      if (!volume.IsInvalid) {
        var query = new WindowsNative.STORAGE_PROPERTY_QUERY { PropertyId = 7 /* StorageDeviceSeekPenaltyProperty */ };
        var seek = default(WindowsNative.DEVICE_SEEK_PENALTY_DESCRIPTOR);
        bool? seekPenalty = null;
        if (WindowsNative.DeviceIoControl(volume, WindowsNative.IOCTL_STORAGE_QUERY_PROPERTY, &query, (uint)sizeof(WindowsNative.STORAGE_PROPERTY_QUERY),
                                          &seek, (uint)sizeof(WindowsNative.DEVICE_SEEK_PENALTY_DESCRIPTOR), out _, IntPtr.Zero)) {
          seekPenalty = seek.IncursSeekPenalty != 0;
        }
        query.PropertyId = 0; // StorageDeviceProperty
        var buffer = stackalloc byte[1024];
        if (WindowsNative.DeviceIoControl(volume, WindowsNative.IOCTL_STORAGE_QUERY_PROPERTY, &query, (uint)sizeof(WindowsNative.STORAGE_PROPERTY_QUERY),
                                          buffer, 1024, out var returned, IntPtr.Zero) && returned >= sizeof(WindowsNative.STORAGE_DEVICE_DESCRIPTOR)) {
          var descriptor = (WindowsNative.STORAGE_DEVICE_DESCRIPTOR*)buffer;
          bus = BusTypeName(descriptor->BusType);
          if (descriptor->ProductIdOffset > 0 && descriptor->ProductIdOffset < returned) {
            device = Marshal.PtrToStringAnsi((IntPtr)(buffer + descriptor->ProductIdOffset))?.Trim() ?? "";
          }
        }
        media = bus switch {
          "NVMe" => "NVMe",
          "FileBackedVirtual" or "Virtual" => "Virtual disk",
          "USB" or "SD" or "MMC" => "Removable",
          _ => seekPenalty switch { true => "HDD", false => "SSD", null => media },
        };
        if (bus is "FileBackedVirtual" or "Virtual") {
          notes.Add("virtual disk (e.g. a VHDX file): the speed depends on the disk of the file");
        }
      }
    }
    return disk with {
      DevDrive = devDrive,
      DevDriveTrusted = trusted,
      ChangeJournal = journal,
      ChangeJournalMaxBytes = journalMax,
      MediaType = media,
      BusType = bus,
      Device = device.Length > 0 ? device : null,
      Notes = notes,
    };
  }

  private static string BusTypeName(int busType) => busType switch {
    1 => "SCSI", 2 => "ATAPI", 3 => "ATA", 4 => "IEEE1394", 5 => "SSA", 6 => "Fibre", 7 => "USB", 8 => "RAID", 9 => "iSCSI",
    10 => "SAS", 11 => "SATA", 12 => "SD", 13 => "MMC", 14 => "Virtual", 15 => "FileBackedVirtual", 16 => "Spaces",
    17 => "NVMe", 18 => "SCM", 19 => "UFS", _ => $"Unknown ({busType})",
  };

  /// <summary>Mount point → device, from /proc/mounts.</summary>
  private static Dictionary<string, string> ReadLinuxMounts() {
    var result = new Dictionary<string, string>();
    foreach (var line in ReadLines("/proc/mounts")) {
      var parts = line.Split(' ');
      if (parts.Length >= 3) {
        result[UnescapeMount(parts[1])] = parts[0];
      }
    }
    return result;
  }

  private static string UnescapeMount(string text) =>
    Regex.Replace(text, @"\\([0-7]{3})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 8)).ToString());

  private static DiskInfo AddLinuxDetails(DiskInfo disk, Dictionary<string, string> devices) {
    var mount = disk.MountPoint.Length > 1 ? disk.MountPoint.TrimEnd('/') : disk.MountPoint;
    if (!devices.TryGetValue(mount, out var device)) {
      return disk;
    }
    var media = disk.MediaType;
    if (disk.FileSystem is "nfs" or "nfs4" or "cifs" or "smb3" or "smbfs" or "fuse.sshfs" or "9p" or "virtiofs") {
      media = "Network";
    } else if (device.StartsWith("/dev/", StringComparison.Ordinal)) {
      var name = Path.GetFileName(ResolveLink(device));
      var block = $"/sys/class/block/{name}";
      // A partition: its disk is the parent directory in /sys.
      if (File.Exists(Path.Combine(block, "partition"))) {
        var parent = Path.GetFileName(Path.GetDirectoryName(ResolveLink(block)) ?? "");
        if (parent.Length > 0) {
          name = parent;
        }
      }
      var rotational = ReadLines($"/sys/block/{name}/queue/rotational").FirstOrDefault();
      media = name.StartsWith("nvme", StringComparison.Ordinal) ? "NVMe" :
        name.StartsWith("dm-", StringComparison.Ordinal) || name.StartsWith("md", StringComparison.Ordinal) ? (rotational == "1" ? "HDD" : "SSD") + " (device mapper/RAID)" :
        rotational == "1" ? "HDD" : rotational == "0" ? "SSD" : media;
    }
    return disk with { Device = device, MediaType = media };
  }

  private static string ResolveLink(string path) {
    try {
      var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
      return target?.FullName ?? Path.GetFullPath(path);
    } catch (IOException) {
      return path;
    }
  }

  private static DiskInfo AddMacDetails(DiskInfo disk) {
    var result = ProcessRunner.Run("diskutil", ["info", "-plist", disk.MountPoint], TimeSpan.FromSeconds(15));
    if (result is not { Success: true }) {
      return disk;
    }
    var plist = ParsePlistDictionary(result.Output);
    var solid = plist.GetValueOrDefault("SolidState");
    var protocol = plist.GetValueOrDefault("BusProtocol");
    var media = protocol is "Disk Image" ? "Virtual disk" :
      solid == "true" ? (protocol is "PCI-Express" or "Apple Fabric" or "NVMe" ? "NVMe" : "SSD") :
      solid == "false" ? "HDD" : disk.MediaType;
    return disk with {
      Device = plist.GetValueOrDefault("DeviceIdentifier"),
      FileSystem = plist.GetValueOrDefault("FilesystemType") ?? disk.FileSystem,
      BusType = protocol,
      MediaType = media,
    };
  }

  /// <summary>The top-level string/bool/integer values of an XML property list (diskutil -plist).</summary>
  public static Dictionary<string, string> ParsePlistDictionary(string xml) {
    var result = new Dictionary<string, string>();
    try {
      var dict = XDocument.Parse(xml).Root?.Element("dict");
      if (dict == null) {
        return result;
      }
      string? key = null;
      foreach (var element in dict.Elements()) {
        if (element.Name == "key") {
          key = element.Value;
          continue;
        }
        if (key != null) {
          var value = element.Name.LocalName switch {
            "true" => "true",
            "false" => "false",
            "string" or "integer" or "real" => element.Value,
            _ => null,
          };
          if (value != null) {
            result[key] = value;
          }
          key = null;
        }
      }
    } catch (System.Xml.XmlException) {
      // Not a property list.
    }
    return result;
  }

  // -----------------------------------------------------------------------------------------------
  // Security software

  private static SecurityInfo CollectSecurity(bool elevatedDetails, List<DiskInfo> disks) {
    if (!OperatingSystem.IsWindows()) {
      return new SecurityInfo {
        AntivirusNote = "not checked on this OS",
        FilterDriversNote = "not applicable",
      };
    }
    var info = new SecurityInfo();
    var antivirus = new List<AntivirusProduct>();
    string? antivirusNote = null;
    var json = ProcessRunner.PowerShell(
      "Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct -ErrorAction Stop | " +
      "Select-Object displayName,productState | ConvertTo-Json -Compress", TimeSpan.FromSeconds(30));
    if (json == null) {
      antivirusNote = "not available (Windows Security Center missing, e.g. on Windows Server)";
    } else {
      foreach (var item in ParseJsonArray(json)) {
        var state = (int?)item["productState"] ?? 0;
        antivirus.Add(new AntivirusProduct {
          Name = (string?)item["displayName"] ?? "?",
          State = DecodeAntivirusState(state),
          UpToDate = ((state >> 0) & 0xFF) == 0x00,
        });
      }
      if (antivirus.Count == 0) {
        antivirusNote = "no antivirus registered in Windows Security Center";
      }
    }

    bool? realTime = null;
    string? mode = null, defenderNote = null;
    var defender = ProcessRunner.PowerShell(
      "Get-MpComputerStatus -ErrorAction Stop | Select-Object AMRunningMode,RealTimeProtectionEnabled | ConvertTo-Json -Compress", TimeSpan.FromSeconds(30));
    var defenderObject = defender != null ? ParseJsonArray(defender).FirstOrDefault() : null;
    if (defenderObject != null) {
      realTime = (bool?)defenderObject["RealTimeProtectionEnabled"];
      mode = (string?)defenderObject["AMRunningMode"];
    } else {
      defenderNote = "not readable (Defender not installed, or its state is not accessible)";
    }

    List<string>? filters = null;
    string? filtersNote;
    Dictionary<string, string>? devDriveDetails = null;
    if (elevatedDetails) {
      var fltmc = ProcessRunner.Run("fltmc", ["filters"], TimeSpan.FromSeconds(15));
      if (fltmc is { Success: true }) {
        filters = ParseFltmc(fltmc.Output);
        filtersNote = null;
      } else {
        filtersNote = "fltmc failed";
      }
      devDriveDetails = [];
      foreach (var disk in disks.Where(d => d.DevDrive == true)) {
        var query = ProcessRunner.Run("fsutil", ["devdrv", "query", disk.MountPoint.TrimEnd('\\')], TimeSpan.FromSeconds(15));
        if (query is { Success: true }) {
          devDriveDetails[disk.MountPoint] = string.Join(" | ", query.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
      }
    } else {
      filtersNote = IsElevated() ? "not listed (declined)" : "not available (needs admin)";
    }
    return info with {
      Antivirus = antivirus,
      AntivirusNote = antivirusNote,
      DefenderRealTimeProtection = realTime,
      DefenderMode = mode,
      DefenderNote = defenderNote,
      FilterDrivers = filters,
      FilterDriversNote = filtersNote,
      DevDriveDetails = devDriveDetails,
    };
  }

  /// <summary>
  /// Decodes the productState of Windows Security Center (undocumented but stable: 0xAABBCC, BB =
  /// real-time protection state, CC = signatures state).
  /// </summary>
  public static string DecodeAntivirusState(int productState) => ((productState >> 8) & 0xFF) switch {
    0x10 or 0x11 => "on",
    0x00 or 0x01 => "off",
    0x20 or 0x21 => "snoozed",
    0x30 or 0x31 => "expired",
    var other => $"unknown (0x{other:x2})",
  };

  private static IEnumerable<JsonObject> ParseJsonArray(string json) {
    JsonNode? node;
    try {
      node = JsonNode.Parse(json.Trim().Length == 0 ? "[]" : json);
    } catch (System.Text.Json.JsonException) {
      yield break;
    }
    if (node is JsonArray array) {
      foreach (var item in array.OfType<JsonObject>()) {
        yield return item;
      }
    } else if (node is JsonObject single) {
      yield return single;
    }
  }

  /// <summary>Filter names and altitudes from the output of "fltmc filters".</summary>
  public static List<string> ParseFltmc(string output) {
    var result = new List<string>();
    foreach (var line in output.Split('\n').Select(l => l.Trim())) {
      var parts = Regex.Split(line, @"\s+");
      // "Filter Name   Num Instances   Altitude   Frame", then a line of dashes.
      if (parts.Length >= 3 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
          decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out _)) {
        result.Add($"{parts[0]} (altitude {parts[2]})");
      }
    }
    return result;
  }
}

/// <summary>libc functions (Linux, macOS).</summary>
internal static partial class UnixNative {
  [LibraryImport("libc", SetLastError = false)]
  public static partial uint geteuid();
}
