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
}

void RE2Wrapper::Match() {
}
