// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#pragma once

#include "search_base.h"

class RE2SearchImpl;

class RE2Search : public AsciiSearchBase {
 public:
  RE2Search();
  virtual ~RE2Search() OVERRIDE;

  virtual int GetSearchBufferSize() OVERRIDE;
  virtual void CancelSearch(SearchParams* searchParams) OVERRIDE;

 protected:
  virtual void StartSearchWorker(const char *pattern, int patternLen, SearchOptions options, SearchCreateResult& result) OVERRIDE;
  virtual void FindNextWorker(SearchParams* searchParams) OVERRIDE;

 private:
  const char *pattern_;
  int patternLen_;
  RE2SearchImpl* impl_;
};
