using System;
using System.Collections.ObjectModel;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public class FilesChangedEventArgs : EventArgs {
    public ReadOnlyCollection<Tuple<IProject, FileName>> ChangedFiles { get; set; }
  }
}