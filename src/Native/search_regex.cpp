// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "stdafx.h"

#include <regex>

#include "search_regex.h"


class regex_traits_fast_icase : public std::regex_traits<char> {
public:
  char translate_nocase(char _Ch) const {
    if (_Ch >= 'A' && _Ch <= 'Z')
      _Ch |= 0x20;
    return _Ch;
  }
};

#if 1
typedef std::basic_regex<char, regex_traits_fast_icase> regex_t;
typedef std::regex_iterator<const char *, char, regex_traits_fast_icase> regex_iterator_t;
#else
typedef std::regex regex_t;
typedef std::cregex_iterator regex_iterator_t;
#endif

class RegexSearchImpl {
public:
  RegexSearchImpl() : regex_(nullptr) {
  }
  ~RegexSearchImpl() {
    delete regex_;
  }
  regex_t* regex_;
  regex_iterator_t it_end_;
};

RegexSearch::RegexSearch()
    : pattern_(NULL),
      patternLen_(0),
      impl_(new RegexSearchImpl()) {
}

RegexSearch::~RegexSearch() {
  delete impl_;
}

void RegexSearch::PreProcess(
    const char *pattern,
    int patternLen,
    SearchOptions options,
    SearchCreateResult& result) {
  auto flags = std::regex::ECMAScript /*| std::regex::optimize*/;
  if ((options & kMatchCase) == 0) {
    flags = flags | std::regex::icase;
  }
  try {
    impl_->regex_ = new regex_t(pattern, patternLen, flags);
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
    strcpy_s(result.ErrorMessage, errorMessage.c_str());
  }
  pattern_ = pattern;
  patternLen_ = patternLen;
}

int RegexSearch::GetSearchBufferSize() {
  return sizeof(std::cregex_iterator);
}

void RegexSearch::Search(SearchParams* searchParams) {
  regex_iterator_t* pit =
      reinterpret_cast<regex_iterator_t*>(searchParams->SearchBuffer);
  // Placement new for the iterator on the 1st call
  if (searchParams->MatchStart == nullptr) {
    pit = new(pit) regex_iterator_t(
        searchParams->TextStart,
        searchParams->TextStart + searchParams->TextLength,
        *impl_->regex_);
  }
  // Iterate
  regex_iterator_t& it(*pit);
  if (it == impl_->it_end_) {
    // Implicit cleanup on completed search.
    CancelSearch(searchParams);
    return;
  }
  // Set result if match found
  searchParams->MatchStart = searchParams->TextStart + it->position();
  searchParams->MatchLength = (int)it->length(); // TODO(rpaquay): 2GB limit
  ++it;
}

void RegexSearch::CancelSearch(SearchParams* searchParams) {
  regex_iterator_t* pit =
      reinterpret_cast<regex_iterator_t*>(searchParams->SearchBuffer);
  // Explicit destructor call to match placement new call.
  pit->regex_iterator_t::~regex_iterator_t();
  searchParams->MatchStart = nullptr;
}
