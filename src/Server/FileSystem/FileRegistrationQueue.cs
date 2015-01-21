// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystem {
  public class FileRegistrationQueue {
    private readonly SimpleConcurrentQueue<FileRegistrationEntry> _queue = new SimpleConcurrentQueue<FileRegistrationEntry>();

    public void Enqueue(FileRegistrationKind registrationKind, FullPath path) {
      _queue.Enqueue(new FileRegistrationEntry(path, registrationKind));
    }

    public IList<FileRegistrationEntry> DequeueAll() {
      var allEntries = _queue.DequeueAll();
      if (allEntries.Count == 0) {
        return allEntries;
      }

      // Merge entries into a shorter list
      var singleEntries = new Dictionary<FullPath, FileRegistrationKind>();
      foreach (var entry in allEntries) {
        FileRegistrationKind previousKind;
        if (singleEntries.TryGetValue(entry.Path, out previousKind)) {
          // Path has been (un)registered, merge both kinds together
          switch(previousKind) {
            case FileRegistrationKind.Register:
              switch (entry.Kind) {
                case FileRegistrationKind.Register:
                  // Register + Register = leave as is
                  break;
                case FileRegistrationKind.Unregister:
                  // Register + Unregister = remove entry
                  singleEntries.Remove(entry.Path);
                  break;
                default:
                  throw new ArgumentOutOfRangeException();
              }
              break;
            case FileRegistrationKind.Unregister:
              switch (entry.Kind) {
                case FileRegistrationKind.Register:
                  // Unregister + Register = remove entry
                  singleEntries.Remove(entry.Path);
                  break;
                case FileRegistrationKind.Unregister:
                  // Unregister + Unregister = leave as is
                  break;
                default:
                  throw new ArgumentOutOfRangeException();
              }
              break;
            default:
              throw new ArgumentOutOfRangeException();
          }
        } else {
          // Path is not registered, simply add the entry.
          singleEntries.Add(entry.Path, entry.Kind);
        }
      }
      return singleEntries.Select(x => new FileRegistrationEntry(x.Key, x.Value)).ToList();
    }
  }

  public class FileRegistrationEntry {
    private readonly FullPath _path;
    private readonly FileRegistrationKind _kind;

    public FileRegistrationEntry(FullPath path, FileRegistrationKind kind) {
      _path = path;
      _kind = kind;
    }

    public FullPath Path {
      get { return _path; }
    }

    public FileRegistrationKind Kind {
      get { return _kind; }
    }
  }

  public enum FileRegistrationKind {
    Register,
    Unregister,
  }
}