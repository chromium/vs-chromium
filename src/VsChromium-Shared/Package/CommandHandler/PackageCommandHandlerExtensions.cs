// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Shell;
using VsChromium.Commands;

namespace VsChromium.Package.CommandHandler {
  public static class PackageCommandHandlerExtensions {
    /// <summary>
    /// Convert an instance of <see cref="IPackageCommandHandler"/> to an <see
    /// cref="OleMenuCommand"/>. Note: When using the <see
    /// cref="OleMenuCommand"/> with an implementation of <see
    /// cref="OleMenuCommandService"/>, make sure to override <see
    /// cref="Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget "/> and forward calls to <see
    /// cref="OleCommandTargetSpy"/>.
    /// </summary>
    public static OleMenuCommand ToOleMenuCommand(this IPackageCommandHandler handler) {
      var command = new OleMenuCommand(handler.Execute, handler.CommandId);
      command.BeforeQueryStatus += (sender, args) =>
        OleCommandTargetSpy.WrapBeforeQueryStatus(command, handler);
      return command;
    }
  }
}