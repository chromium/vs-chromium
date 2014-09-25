// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.Threads {
  /// <summary>
  /// Ability to run tasks sequentially on a thread from the custom thread pool.
  /// </summary>
  public interface ITaskQueue {
    /// <summary>
    /// Enqueue a new task to be run sequentially after all currently enqueue tasks have been
    /// run. If a task with the same <see cref="TaskId"/> is enqueued and not currently executing,
    /// it it removed from the queue before the new task is enqueued.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="task"></param>
    void Enqueue(TaskId id, Action task);
  }

  /// <summary>
  /// A simple task identifier used for determining if a previous task needs to be
  /// removed from the queue when enqueuing new tasks. <see cref="Description"/> is
  /// only used for logging and debugging purposes.
  /// </summary>
  public class TaskId : IEquatable<TaskId> {
    private readonly String _description;

    public TaskId(string description) {
      _description = description;
    }

    public String Description {
      get { return _description; }
    }

    public override bool Equals(object obj) {
      return Equals(obj as TaskId);
    }

    public override int GetHashCode() {
      return base.GetHashCode();
    }

    public bool Equals(TaskId other) {
      return object.ReferenceEquals(this, other);
    }
  }
}
