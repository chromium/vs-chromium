using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Debugger.Evaluation;

namespace ChromeVis
{
  [Flags]
  public enum ChildDisplayFlags
  {
    HasCustomFields = 0x1,
    HasDefaultFields = 0x2,
    DefaultFieldsInline = 0x4
  }

  public enum ChildDisplayMode
  {
    Nested,
    Inline
  }
}
