using System.IO;
using VsChromiumCore.Win32.Processes;

namespace VsChromiumCore.Processes {
  public class ProcessResult {
    public StreamWriter StandardInput { get; set; }
    public StreamReader StandardOutput { get; set; }
    public StreamReader StandardError { get; set; }
    public SafeProcessHandle ProcessHandle { get; set; }
    public int ProcessId { get; set; }
  }
}