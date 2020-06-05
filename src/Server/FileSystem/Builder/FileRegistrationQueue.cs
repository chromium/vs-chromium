// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystem.Builder {
  public class FileRegistrationQueue {
    /// <summary>
    /// Set this flag to enable debugging of the file registration request received by the server over time.
    /// 
    /// <para>Note: Use this for debugging purposes only as all requests are kept in memory</para>
    /// </summary>
    private static bool Debug_KeepAllItems = false;
    /// <summary>
    /// This is only active (and non-null) when <see cref="Debug_KeepAllItems"/> is set to <code>true</code>
    /// at build time.
    /// </summary>
    private readonly ConcurrentBufferQueue<FileRegistrationEntry> _allItemsQueue;

    private readonly ConcurrentBufferQueue<FileRegistrationEntry> _queue = new ConcurrentBufferQueue<FileRegistrationEntry>();

    public FileRegistrationQueue() {
      _allItemsQueue = (Debug_KeepAllItems ? new ConcurrentBufferQueue<FileRegistrationEntry>() : null);
    }

    public void Enqueue(FileRegistrationKind registrationKind, FullPath path) {
      if (_allItemsQueue != null) {
        _allItemsQueue.Enqueue(new FileRegistrationEntry(path, registrationKind));
      }
      _queue.Enqueue(new FileRegistrationEntry(path, registrationKind));
    }

    public IList<FullPath> MergedItems {
      get {
        if (_allItemsQueue == null) {
          throw new InvalidOperationException("Set the Debug_KeepAllItems flag to enable this method");
        }
        HashSet<FullPath> entries = new HashSet<FullPath>();
        foreach (var entry in _allItemsQueue.GetCopy()) {
          switch (entry.Kind) {
            case FileRegistrationKind.Register:
              entries.Add(entry.Path);
              break;
            case FileRegistrationKind.Unregister:
              entries.Remove(entry.Path);
              break;
          }
        }
        return entries.OrderBy(x => x).ToList();
      }
    }

    public IList<FileRegistrationEntry> GetEntries(string pattern) {
      if (_allItemsQueue == null) {
        throw new InvalidOperationException("Set the Debug_KeepAllItems flag to enable this method");
      }
      return _allItemsQueue.GetCopy().Where(x => x.Path.Value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
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

    public override string ToString() {
      return String.Format("{0}: {1}", _kind, _path);
    }
  }

  public enum FileRegistrationKind {
    Register,
    Unregister,
  }
}