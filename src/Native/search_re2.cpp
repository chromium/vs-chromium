// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "stdafx.h"

#include <regex>

#include "search_re2.h"

#include "re2/re2_wrapper.h"

class RE2SearchImpl {
public:
  RE2SearchImpl() : regex_(nullptr) {
  }
  ~RE2SearchImpl() {
    delete regex_;
  }
  RE2Wrapper* regex_;
};

RE2Search::RE2Search()
    : pattern_(NULL),
      patternLen_(0),
      impl_(new RE2SearchImpl()) {
}

RE2Search::~RE2Search() {
  delete impl_;
}

void RE2Search::PreProcess(
    const char *pattern,
    int patternLen,
    SearchOptions options,
    SearchCreateResult& result) {
}

int RE2Search::GetSearchBufferSize() {
  //return sizeof(std::cregex_iterator);
  return 0;
}

void RE2Search::Search(SearchParams* searchParams) {
}

void RE2Search::CancelSearch(SearchParams* searchParams) {
}
