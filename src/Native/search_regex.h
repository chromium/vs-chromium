// Copyright 2014 The Chromium Authors. All rights reserved.
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
  virtual int GetSearchBufferSize() OVERRIDE;
  virtual void Search(SearchParams* searchParams) OVERRIDE;

 private:
  const char *pattern_;
  int patternLen_;
  std::regex* regex_;
  std::cregex_iterator it_end_;
};
