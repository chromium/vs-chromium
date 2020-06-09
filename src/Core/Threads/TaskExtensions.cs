// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace VsChromium.Core.Threads {
  public static class TaskExtensions {
    public static Task ContinueWithTask<T>(this Task<T> task, Func<Task<T>, Task> continuationFunction, CancellationToken cancellationToken) {
      var tcs = new TaskCompletionSource<object>();
      task.ContinueWith(t => {
        if (cancellationToken.IsCancellationRequested) {
          tcs.TrySetCanceled();
        } else {
          // This block ensures we terminate "tcs" if "continuationFunction" throws
          Task newTask = null;
          try {
            newTask = continuationFunction(t);
          }
          catch (Exception e) {
            tcs.TrySetException(e);
          }

          // Continue only if the new task is valid
          newTask?.ContinueWith(t2 => {
            if (cancellationToken.IsCancellationRequested || t2.IsCanceled) {
              tcs.TrySetCanceled();
            }
            else if (t2.Exception != null) {
              tcs.TrySetException(t2.Exception);
            }
            else {
              tcs.TrySetResult(null);
            }
          });
        }
      });
      return tcs.Task;
    }

  }
}
