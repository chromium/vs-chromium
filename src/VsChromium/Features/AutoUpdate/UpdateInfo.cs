using System;

namespace VsChromium.Features.AutoUpdate {
  public class UpdateInfo {
    /// <summary>
    /// Version # of the VsChromium package.
    /// </summary>
    public Version Version { get; set; }
    /// <summary>
    /// Url where the VsChromium package installation can be found.
    /// </summary>
    public Uri Url { get; set; }
  }
}