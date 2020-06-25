// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.Search {
  public interface ISearchStringParser {
    ParsedSearchString Parse(string searchString, SearchStringParserOptions options);
  }

  public enum SearchStringParserOptions {
    /// <summary>
    /// Search string is returned &quot;as-is&quot;
    /// </summary>
    NoSpecialCharacter,
    /// <summary>
    /// Search string has only one special character: the &quot;*&quot; (asterisk) wildcard character.
    /// </summary>
    SupportsAsterisk,
    /// <summary>
    /// Search string has only one special character: the &quot;*&quot; (asterisk) wildcard character.
    /// Escaping the wildcard character is done by repeating the character twice (&quot;**&quot;).
    /// </summary>
    SupportsAsteriskAndEscaping,
  }
}
