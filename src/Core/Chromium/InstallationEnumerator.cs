using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Processes;
using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Chromium {
  public class InstallationEnumerator : IEnumerable<InstallationData> {
    private const string kUninstallSubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
    public IEnumerator<InstallationData> GetEnumerator() {
      foreach (InstallationData data in Enumerate(InstallationLevel.System))
        yield return data;
      foreach (InstallationData data in Enumerate(InstallationLevel.User))
        yield return data;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }

    private IEnumerable<InstallationData> Enumerate(InstallationLevel level) {
      List<InstallationData> results = new List<InstallationData>();

      RegistryHive hive = (level == InstallationLevel.User) 
          ? RegistryHive.CurrentUser 
          : RegistryHive.LocalMachine;
      using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default)) {
      using (RegistryKey uninstallKey = baseKey.OpenSubKey(kUninstallSubKey)) {
        foreach (string subkeyName in uninstallKey.GetSubKeyNames()) {
          using (RegistryKey subkey = uninstallKey.OpenSubKey(subkeyName)) {
            string location = (string)subkey.GetValue("InstallLocation");
            if (location == null)
              continue;

            string exePath = Path.Combine(location, "chrome.exe");
            if (!File.Exists(exePath))
              continue;

            int iconIndex = 0;
            string iconString = (string)subkey.GetValue("DisplayIcon");
            if (iconString != null) {
              int index = iconString.LastIndexOf(',');
              string indexString = iconString.Substring(index + 1);
              iconIndex = int.TryParse(indexString, out index) ? index : 0;
            }

            string name = (string)subkey.GetValue("DisplayName");
            string version = (string)subkey.GetValue("DisplayVersion");

            results.Add(new InstallationData(exePath, level, iconIndex, name, version));
          }
        }
      }
      }
      return results;
    }
  }
}
