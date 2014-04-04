// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.Dia;
using Microsoft.VisualStudio.Debugger.Native;
using Microsoft.VisualStudio.Debugger.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Win32;

namespace VsChromium.DkmIntegration.ServerComponent {
  class DiaUtil {
    public static ComPtr<IDiaSymbol> GetRootSymbol(DkmModule module) {
      ComPtr<IDiaSession> session;
      Guid diaSessionGuid = typeof(IDiaSession).GUID;
      using (session = ComPtr.Create((IDiaSession)module.GetSymbolInterface(diaSessionGuid))) {
        IDiaEnumSymbols enumerator;
        session.Ptr.findChildren(null, SymTagEnum.SymTagExe, null, 0, out enumerator);
        if (enumerator.count == 0)
          return new ComPtr<IDiaSymbol>();
        return ComPtr.Create(enumerator.Item(0));
      }
    }

    public static ComPtr<IDiaSymbol> FindChildSymbol(ComPtr<IDiaSymbol> parent, SymTagEnum tag, string name) {
      var result = new ComPtr<IDiaSymbol>();

      IDiaEnumSymbols enumerator;
      parent.Ptr.findChildren(tag, name, 1, out enumerator);
      using (ComPtr.Create(enumerator)) {
        if (enumerator.count == 0)
          return new ComPtr<IDiaSymbol>();

        result = ComPtr.Create(enumerator.Item((uint)0));
      }

      return result;
    }

    public static DkmNativeInstructionAddress GetFunctionAddress(DkmNativeModuleInstance moduleInstance, string name) {
      uint rva = 0;
      using (var rootSymbol = GetRootSymbol(moduleInstance.Module)) {
        if (rootSymbol.Ptr == null)
          return null;

        using (var funcSymbol = FindChildSymbol(rootSymbol, SymTagEnum.SymTagFunction, name)) {
          if (funcSymbol.Ptr == null)
            return null;
          rva = funcSymbol.Ptr.relativeVirtualAddress;
        }
      }

      return (DkmNativeInstructionAddress)moduleInstance.Process.CreateNativeInstructionAddress(moduleInstance.BaseAddress + rva);
    }
  }
}
