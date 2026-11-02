// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Core.Logging;
using VsChromium.Threads;

namespace VsChromium.Package {
  [Export(typeof(IDispatchThreadEventBus))]
  public class DispatchThreadEventBus : IDispatchThreadEventBus {
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly Dictionary<string, List<Entry>> _handlers = new Dictionary<string, List<Entry>>();
    private readonly object _lock = new object();

    [ImportingConstructor]
    public DispatchThreadEventBus(ISynchronizationContextProvider synchronizationContextProvider) {
      _synchronizationContextProvider = synchronizationContextProvider;
    }

    private class Entry {
      public Entry(string eventName, EventHandler handler) {
        EventName = eventName;
        Handler = handler;
      }

      public string EventName { get; }

      public EventHandler Handler { get; }
    }

    public object RegisterHandler(string eventName, EventHandler handler) {
      var entry = new Entry(eventName, handler);
      lock (_lock) {
        List<Entry> entries;
        if (!_handlers.TryGetValue(eventName, out entries)) {
          entries = new List<Entry>();
          _handlers.Add(eventName, entries);
        }
        entries.Add(entry);
      }
      return entry;
    }

    public void UnregisterHandler(object cookie) {
      var entry = (Entry)cookie;
      lock (_lock) {
        var list = _handlers[entry.EventName];
        foreach (var item in list) {
          if (ReferenceEquals(cookie, entry)) {
            list.Remove(item);
            return;
          }
        }
      }
      throw new ArgumentException("Invalid cookie");
    }

    public void PostEvent(string eventName, object sender, EventArgs args) {
      Entry[] temp;
      lock (_lock) {
        List<Entry> entries;
        if (!_handlers.TryGetValue(eventName, out entries)) {
          return;
        }
        temp = entries.ToArray();
      }

      _synchronizationContextProvider.DispatchThreadContext.Post(() => {
        foreach (var item in temp) {
          var localItem = item;
          Logger.WrapActionInvocation(() => localItem.Handler(sender, args));
        }
      });
    }
  }
}
