// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "stdafx.h"

#include <regex>

#include "search_regex.h"

RegexSearch::RegexSearch()
    : pattern_(NULL),
      patternLen_(0),
      regex_(nullptr) {
}

RegexSearch::~RegexSearch() {
  delete regex_;
}

void RegexSearch::PreProcess(const char *pattern, int patternLen, SearchOptions options, SearchCreateResult& result) {
  auto flags = std::regex::ECMAScript;
  if ((options & kMatchCase) == 0) {
    flags = flags | std::regex::icase;
  }
  try {
    regex_ = new std::regex(pattern, patternLen, flags);
  } catch(std::regex_error& error) {
    result.HResult = E_INVALIDARG;
    // Format the error message: remove the leading text up to ':'
    //
    // See regex_error stringify function, for example:
    //  "regex_error(error_brace): The expression contained mismatched { and }."
    std::string errorMessage = error.what();
    size_t index = errorMessage.find(": ");
    if (index != std::string::npos) {
      errorMessage = errorMessage.substr(index + 2);
    }
    errorMessage = std::string("Invalid Regular expression: ") + errorMessage;
    strcpy_s(result.ErrorMessage, errorMessage.c_str());
  }
  pattern_ = pattern;
  patternLen_ = patternLen;
}

int RegexSearch::GetSearchBufferSize() {
  return sizeof(std::cregex_iterator);
}

void RegexSearch::Search(SearchParams* searchParams) {
  std::cregex_iterator* pit =
      reinterpret_cast<std::cregex_iterator *>(searchParams->SearchBuffer);
  // Placement new for the iterator on the 1st call
  if (searchParams->MatchStart == nullptr) {
    pit = new(pit) std::cregex_iterator(
        searchParams->TextStart,
        searchParams->TextStart + searchParams->TextLength,
        *regex_);
  }
  // Iterate
  std::cregex_iterator& it(*pit);
  if (it == it_end_) {
    // Explicit call to destructor on last call.
    searchParams->MatchStart = nullptr;
    pit->std::cregex_iterator::~cregex_iterator();
    return;
  }
  // Set result if match found
  searchParams->MatchStart = searchParams->TextStart + it->position();
  searchParams->MatchLength = (int)it->length(); // TODO(rpaquay): 2GB limit
  ++it;
}
