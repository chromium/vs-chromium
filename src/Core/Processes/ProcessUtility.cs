using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Processes {
  public static class ProcessUtility {

    public static MachineType GetMachineType(string path) {
      // Open the PE File as a binary file, and parse just enough information to determine the
      // machine type.
      //http://www.microsoft.com/whdc/system/platform/firmware/PECOFF.mspx
      using (SafeFileHandle safeHandle =
                Win32.Files.NativeMethods.CreateFile(
                    path,
                    NativeAccessFlags.GenericRead,
                    FileShare.Read,
                    IntPtr.Zero,
                    FileMode.Open,
                    FileAttributes.Normal,
                    IntPtr.Zero)) {
        FileStream fs = new FileStream(safeHandle, FileAccess.Read);
        using (BinaryReader br = new BinaryReader(fs)) {
          fs.Seek(0x3c, SeekOrigin.Begin);
          Int32 peOffset = br.ReadInt32();
          fs.Seek(peOffset, SeekOrigin.Begin);
          UInt32 peHead = br.ReadUInt32();
          if (peHead != 0x00004550) // "PE\0\0", little-endian
            throw new Exception("Can't find PE header");
          return (MachineType)br.ReadUInt16();
        }
      }
    }
  }
}
