// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include "search_base.h"

class StrStrSearch : public AsciiSearchBase {
 public:
  StrStrSearch();

 protected:
  virtual void StartSearchWorker(const char *pattern, int patternLen, SearchOptions options, SearchCreateResult& result) OVERRIDE;
  virtual void FindNextWorker(SearchParams* searchParams) OVERRIDE;

 private:
  const char *pattern_;
  int patternLen_;
};
