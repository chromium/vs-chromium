// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "stdafx.h"

#include <regex>

#include "search_re2.h"

#include "re2/re2_wrapper.h"

class RE2SearchImpl {
public:
  RE2SearchImpl() : re2_wrapper(nullptr) {
  }
  ~RE2SearchImpl() {
    delete re2_wrapper;
  }
  RE2Wrapper* re2_wrapper;
};

RE2Search::RE2Search()
    : pattern_(NULL),
      patternLen_(0),
      impl_(new RE2SearchImpl()) {
}

RE2Search::~RE2Search() {
  delete impl_;
}

void RE2Search::StartSearchWorker(
    const char *pattern,
    int patternLen,
    SearchOptions options,
    SearchCreateResult& result) {
  RE2Wrapper* re2_wrapper = new RE2Wrapper();
  bool caseSensitive = (options & kMatchCase);
  std::string error;
  re2_wrapper->Compile(pattern, patternLen, caseSensitive, &error);
  if (!error.empty()) {
    result.HResult = E_FAIL;
    strcpy_s(result.ErrorMessage, error.c_str());
    delete re2_wrapper;
    return;
  }

  impl_->re2_wrapper = re2_wrapper;
  result.HResult = S_OK;
}

int RE2Search::GetSearchBufferSize() {
  return 0;
}

void RE2Search::FindNextWorker(SearchParams* searchParams) {
  const char* text;
  int textLength;
  if (searchParams->MatchStart == nullptr) {
    text = searchParams->TextStart;
    textLength = searchParams->TextLength;
  } else {
    text = searchParams->MatchStart + searchParams->MatchLength;
    // TODO (rpaquay): 2GB limit
    textLength = static_cast<int>(searchParams->TextStart + searchParams->TextLength - text);
  }

  const char* match;
  int matchLength;
  impl_->re2_wrapper->Match(text, textLength, &match, &matchLength);
  if (matchLength == 0)
    matchLength++;
  searchParams->MatchStart = match;
  searchParams->MatchLength = matchLength;
}

void RE2Search::CancelSearch(SearchParams* searchParams) {
  // Nothing to do, since out buffer is simple integers.
}
