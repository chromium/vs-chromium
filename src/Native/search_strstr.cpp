// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "stdafx.h"

#include "search_strstr.h"

#include <algorithm>

StrStrSearch::StrStrSearch()
    : pattern_(NULL),
      patternLen_(0) {
}

void StrStrSearch::PreProcess(
    const char *pattern,
    int patternLen,
    SearchOptions options,
    SearchCreateResult& result) {
  pattern_ = pattern;
  patternLen_ = patternLen;
}

void StrStrSearch::Search(SearchParams* searchParams) {
  const char* start = searchParams->TextStart;
  const char* last = searchParams->TextStart + searchParams->TextLength;
  if (searchParams->MatchStart) {
    start = searchParams->MatchStart + searchParams->MatchLength;
  }

  auto result = std::search(start, last, pattern_, pattern_ + patternLen_);
  if (result == last) {
    searchParams->MatchStart = nullptr;
    searchParams->MatchLength = 0;
    return;
  }
  searchParams->MatchStart = result;
  searchParams->MatchLength = patternLen_;
}
