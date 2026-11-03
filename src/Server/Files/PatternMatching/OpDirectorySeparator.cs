using System;
using System.IO;

namespace VsChromium.Core.Files.PatternMatching {
  public class OpDirectorySeparator : BaseOperator {
    public override int MatchWorker(MatchKind kind, IPathComparer comparer, BaseOperator[] operators, int operatorIndex, ReadOnlySpan<char> path, int pathIndex) {
      if (pathIndex < path.Length && path[pathIndex] == Path.DirectorySeparatorChar)
        return Match(kind, comparer, operators, operatorIndex + 1, path, pathIndex + 1);
      return -1;
    }

    public override string ToString() {
      return "<dir separator>";
    }
  }
}
