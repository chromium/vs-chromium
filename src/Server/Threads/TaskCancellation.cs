// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;

namespace VsChromium.Core.Threads {
  /// <summary>
  /// Keeps track of cancellation tokens of multiple independent tasks. Thread
  /// safe.
  /// </summary>
  public class TaskCancellation {
    private readonly object _lock = new object();
    private List<Entry> _entries = new List<Entry>();

    public void CancelAll() {
      List<Entry> tempCopy = null;

      lock (_lock) {
        if (_entries.Count > 0) {
          tempCopy = _entries;
          _entries = new List<Entry>();
        }
      }

      if (tempCopy != null) {
        foreach (var entry in tempCopy) {
          entry.Source.Cancel();
          entry.ReleaseRef();
        }
        tempCopy.Clear();
      }
    }

    public CancellationToken GetNewToken() {
      var source = new CancellationTokenSource();
      var token = source.Token;
      var entry = new Entry(source, initialRefs: 1);
      lock (_lock) {
        _entries.Add(entry);
      }
      return token;
    }

    /// <summary>
    /// Creates a tracked cancellation scope linked with <paramref name="externalToken"/> so that
    /// cancelling either <paramref name="externalToken"/> or <see cref="CancelAll"/> cancels
    /// <see cref="Scope.Token"/>. Disposing the scope unregisters and disposes the underlying
    /// <see cref="CancellationTokenSource"/> once both the scope and any concurrent
    /// <see cref="CancelAll"/> call have completed.
    /// </summary>
    public Scope CreateScope(CancellationToken externalToken = default) {
      var source = externalToken.CanBeCanceled
        ? CancellationTokenSource.CreateLinkedTokenSource(externalToken)
        : new CancellationTokenSource();
      var token = source.Token;
      var entry = new Entry(source, initialRefs: 2);
      lock (_lock) {
        _entries.Add(entry);
      }
      return new Scope(this, entry, token);
    }

    private void ReleaseScope(Entry entry) {
      if (!entry.TryMarkScopeDisposed()) {
        return;
      }
      bool removed;
      lock (_lock) {
        removed = _entries.Remove(entry);
      }
      if (removed) {
        // Removed before CancelAll() saw it: drop both the list ref and the scope ref.
        entry.Source.Dispose();
      } else {
        // CancelAll() already dequeued this entry; drop only the scope ref.
        entry.ReleaseRef();
      }
    }

    internal sealed class Entry {
      public readonly CancellationTokenSource Source;
      private int _refs;
      private int _scopeDisposed;

      public Entry(CancellationTokenSource source, int initialRefs) {
        Source = source;
        _refs = initialRefs;
      }

      public bool TryMarkScopeDisposed() {
        return Interlocked.Exchange(ref _scopeDisposed, 1) == 0;
      }

      public void ReleaseRef() {
        if (Interlocked.Decrement(ref _refs) == 0) {
          Source.Dispose();
        }
      }
    }

    public readonly struct Scope : IDisposable {
      private readonly TaskCancellation _owner;
      private readonly Entry _entry;

      internal Scope(TaskCancellation owner, Entry entry, CancellationToken token) {
        _owner = owner;
        _entry = entry;
        Token = token;
      }

      public CancellationToken Token { get; }

      public void Dispose() {
        _owner?.ReleaseScope(_entry);
      }
    }
  }
}
