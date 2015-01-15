// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include <regex>

#include "search_base.h"

class RegexSearch : public AsciiSearchBase {
 public:
  RegexSearch();
  virtual ~RegexSearch() OVERRIDE;

  virtual bool PreProcess(const char *pattern, int patternLen, SearchOptions options) OVERRIDE;
  virtual void Search(const char *text, int texLen, Callback matchFound) OVERRIDE;

 private:
  const char *pattern_;
  int patternLen_;
  std::regex* regex_;
};
