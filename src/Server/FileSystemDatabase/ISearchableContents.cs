using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// The most basic piece of contents that can be searched.
  /// There is at least one instance per searchable file, and
  /// there may be more than one if the file is large enough.
  /// </summary>
  public interface ISearchableContents {
    FileName FileName { get; }
    int FileId { get; }
    List<FilePositionSpan> Search(SearchContentsData searchContentsData, IOperationProgressTracker progressTracker);
  }
}