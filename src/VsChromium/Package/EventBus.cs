// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Core.Logging;

namespace VsChromium.Package {
  [Export(typeof(IEventBus))]
  public class EventBus : IEventBus {
    private readonly Dictionary<string, List<Entry>> _handlers = new Dictionary<string, List<Entry>>();
    private readonly object _lock = new object();

    private class Entry {
      private readonly string _eventName;
      private readonly EventHandler _handler;

      public Entry(string eventName, EventHandler handler) {
        _eventName = eventName;
        _handler = handler;
      }

      public string EventName {
        get { return _eventName; }
      }

      public EventHandler Handler {
        get { return _handler; }
      }
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
          if (object.ReferenceEquals(cookie, entry)) {
            list.Remove(item);
            return;
          }
        }
      }
      throw new ArgumentException("Invalid cookie");
    }

    public void Fire(string eventName, object sender, EventArgs args) {
      Entry[] temp;
      lock (_lock) {
        List<Entry> entries;
        if (!_handlers.TryGetValue(eventName, out entries)) {
          return;
        }
        temp = entries.ToArray();
      }

      foreach (var item in temp) {
        var localItem = item;
        Logger.WrapActionInvocation(() => localItem.Handler(sender, args));
      }
    }
  }
}