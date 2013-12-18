using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using VsChromiumCore;

namespace VsChromiumPackage.Features.AutoUpdate {
  /// <summary>
  /// Implementation of IUpdateInfoProvider checking for the content of
  /// the "latest_version.txt" file on the public VsChromium GitHub URL.
  /// </summary>
  [Export(typeof(IUpdateInfoProvider))]
  public class UpdateInfoProvider : IUpdateInfoProvider {
    private const string _requestUriString = "http://chromium.github.io/vs-chromium/latest_version.txt";

    public UpdateInfo GetUpdateInfo() {
      var webRequest = WebRequest.Create(_requestUriString);
      using (var response = webRequest.GetResponse()) {
        using (var stream = response.GetResponseStream()) 
        using (var reader = new StreamReader(stream)) {
          var contents = reader.ReadToEnd();
          try {
            return ParseUpdateInfo(contents);
          }
          catch (Exception e) {
            Logger.LogException(e, "Error parsing version info file from url {0}:\r\n{1}", _requestUriString, contents);
            throw new FileFormatException("Invalid version file", e);
          }
        }
      }
    }

    private UpdateInfo ParseUpdateInfo(string contents) {
      using (var reader = new StringReader(contents)) {
        var lines = ParseEntries(ReadLines(reader)).ToList();
        var version = lines.First(x => StringComparer.OrdinalIgnoreCase.Equals(x.Key, "version")).Value;
        var url = lines.First(x => StringComparer.OrdinalIgnoreCase.Equals(x.Key, "url")).Value;
        return new UpdateInfo {
          Version = new Version(version),
          Url = new Uri(url)
        };
      }
    }

    private IEnumerable<LineEntry> ParseEntries(IEnumerable<string> lines) {
      foreach (var line in lines) {
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
          continue;
        var key = line.Substring(0, separatorIndex);
        var value = line.Substring(separatorIndex + 1);
        yield return new LineEntry {
          Key = key.Trim(),
          Value = value.Trim()
        };
      }
    }

    private IEnumerable<string> ReadLines(TextReader reader) {
      for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
        yield return line;
    }

    class LineEntry {
      public string Key { get; set; }
      public string Value { get; set; }
    }
  }
}