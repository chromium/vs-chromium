﻿// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Package {
  /// <summary>
  /// Event bus that uses <see cref="VsChromium.Threads.ISynchronizationContextProvider.DispatchThreadContext"/>.
  /// Events are identified by arbitrary strings.
  /// </summary>
  public interface IDispatchThreadEventBus {
    /// <summary>
    /// Register a handler for a given event name. Returns a cookie object used
    /// when unregistring the handler (<see cref="UnregisterHandler(object)"/>).
    /// <para>This method is multi-thread safe.</para>
    /// </summary>
    object RegisterHandler(string eventName, EventHandler handler);

    /// <summary>
    /// Unregister a handler given its cookie from <see cref="RegisterHandler(string, EventHandler)"/>.
    /// <para>This method is multi-thread safe.</para>
    /// </summary>
    void UnregisterHandler(object cookie);

    /// <summary>
    /// Fire an event given its name, running all handlers in the context of the
    /// <see cref="VsChromium.Threads.ISynchronizationContextProvider.DispatchThreadContext"/>
    /// <para>This method is multi-thread safe.</para>
    /// </summary>
    void PostEvent(string eventName, object sender, EventArgs args);
  }

  public static class EventNames {
    public static class TextDocument {
      public static string DocumentOpened = "TextDocument-Open";
      public static string DocumentClosed = "TextDocument-Closed";
      public static string DocumentChanged = "TextDocument-Changed";
      public static string DocumentFileActionOccurred = "TextDocumentFile-FileActionOccurred";
    }

    public static class ToolsOptions {
      public static string PageApply = "ToolsOptions-PageApply";
    }

    public static class SolutionExplorer {
      public static string ShowFile = "SolutionExplorer-ShowFile";
    }
  }
}
