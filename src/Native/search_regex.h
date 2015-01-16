// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include "search_base.h"

class RegexSearchImpl;

class RegexSearch : public AsciiSearchBase {
 public:
  RegexSearch();
  virtual ~RegexSearch() OVERRIDE;

  virtual void PreProcess(const char *pattern, int patternLen, SearchOptions options, SearchCreateResult& result) OVERRIDE;
  virtual int GetSearchBufferSize() OVERRIDE;
  virtual void Search(SearchParams* searchParams) OVERRIDE;
  virtual void CancelSearch(SearchParams* searchParams) OVERRIDE;

 private:
  const char *pattern_;
  int patternLen_;
  RegexSearchImpl* impl_;
};
