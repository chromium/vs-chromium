// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "re2_wrapper.h"

#include "re2/re2.h"

class RE2WrapperImpl {
public:
  RE2WrapperImpl() : regex_(nullptr) {
  }
  ~RE2WrapperImpl() {
    delete regex_;
  }
  RE2* regex_;
};

RE2Wrapper::RE2Wrapper()
    : pattern_(NULL),
      patternLen_(0),
      impl_(new RE2WrapperImpl()) {
}

RE2Wrapper::~RE2Wrapper() {
  delete impl_;
}

void RE2Wrapper::Compile(
    const char *pattern,
    int patternLen,
    std::string* error) {
  pattern_ = pattern;
  patternLen_ = patternLen;

  re2::StringPiece patternPiece(pattern, patternLen);
  RE2* re2 = new re2::RE2(patternPiece);
  if (re2 == nullptr) {
    *error = "Out of memory";
    return;
  }

  if (!re2->ok()) {
    *error = re2->error();
    delete re2;
    return;
  }

  impl_->regex_ = re2;
  *error = "";
}

void RE2Wrapper::Match(
    const char* textStart,
    int textLength,
    const char** matchStart,
    int* matchLength) {
  re2::StringPiece text(textStart, textLength);
  re2::StringPiece match;
  bool found = impl_->regex_->Match(text, 0, textLength, RE2::UNANCHORED, &match, 1);
  if (!found) {
    (*matchStart) = nullptr;
    (*matchLength) = 0;
    return;
  }
  (*matchStart) = match.data();
  (*matchLength) = match.length();
}
