// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Abstract over a component responsible for pre-processing a text search
  /// request.
  /// </summary>
  public interface ICompiledTextSearchDataFactory {
    /// <summary>
    /// Note: Returns null if the <paramref name="searchParams"/> query would
    /// not produce any result.
    /// Note: Throws an exception if the <paramref name="searchParams"/> query is
    /// invalid.
    /// </summary>
    CompiledTextSearchData Create(SearchParams searchParams);
  }
}