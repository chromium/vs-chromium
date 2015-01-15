// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#include "stdafx.h"

#include <stdint.h>
#include <stdlib.h>
#include <assert.h>

#include "search_boyer_moore.h"

namespace {

//////////////////////////////////////////////////////////////////////////////
// BOYER-MOORE

// delta1 table: delta1[c] contains the distance between the last
// character of needle and the rightmost occurence of c in needle.
// If c does not occur in needle, then delta1[c] = nlen.
// If c is at string[i] and c != needle[nlen-1], we can
// safely shift i over by delta1[c], which is the minimum distance
// needed to shift needle forward to get string[i] lined up 
// with some character in needle.
// this algorithm runs in alphabet_len+nlen time.
void make_delta1(int *delta1, int delta1Size, const uint8_t *pattern, int patternLen, bool matchCase) {
  int NOT_FOUND = patternLen;
  for (int i = 0; i < delta1Size; i++) {
    delta1[i] = NOT_FOUND;
  }
  for (int i = 0; i < patternLen - 1; i++) {
    delta1[AsciiSearchBase::read_byte(pattern, i, matchCase)] = patternLen - 1 - i;
  }
}
 
// true if the suffix of word starting from word[pos] is a prefix 
// of word
int is_prefix(const uint8_t *word, int wordlen, int pos, bool matchCase) {
  int suffixlen = wordlen - pos;
  // could also use the strncmp() library function here
  for (int i = 0; i < suffixlen; i++) {
    if (AsciiSearchBase::read_byte(word, i, matchCase) != AsciiSearchBase::read_byte(word, pos+i, matchCase)) {
      return 0;
    }
  }
  return 1;
}
 
// length of the longest suffix of word ending on word[pos].
// suffix_length("dddbcabc", 8, 4) = 2
int suffix_length(const uint8_t *word, int wordlen, int pos, bool matchCase) {
  int i;
  // increment suffix length i to the first mismatch or beginning
  // of the word
  for (i = 0; (AsciiSearchBase::read_byte(word, pos-i, matchCase) == AsciiSearchBase::read_byte(word, wordlen-1-i, matchCase)) && (i < pos); i++);
  return i;
}
 
// delta2 table: given a mismatch at needle[pos], we want to align 
// with the next possible full match could be based on what we
// know about needle[pos+1] to needle[nlen-1].
//
// In case 1:
// needle[pos+1] to needle[nlen-1] does not occur elsewhere in needle,
// the next plausible match starts at or after the mismatch.
// If, within the substring needle[pos+1 .. nlen-1], lies a prefix
// of needle, the next plausible match is here (if there are multiple
// prefixes in the substring, pick the longest). Otherwise, the
// next plausible match starts past the character aligned with 
// needle[nlen-1].
// 
// In case 2:
// needle[pos+1] to needle[nlen-1] does occur elsewhere in needle. The
// mismatch tells us that we are not looking at the end of a match.
// We may, however, be looking at the middle of a match.
// 
// The first loop, which takes care of case 1, is analogous to
// the KMP table, adapted for a 'backwards' scan order with the
// additional restriction that the substrings it considers as 
// potential prefixes are all suffixes. In the worst case scenario
// needle consists of the same letter repeated, so every suffix is
// a prefix. This loop alone is not sufficient, however:
// Suppose that needle is "ABYXCDEYX", and text is ".....ABYXCDEYX".
// We will match X, Y, and find B != E. There is no prefix of needle
// in the suffix "YX", so the first loop tells us to skip forward
// by 9 characters.
// Although superficially similar to the KMP table, the KMP table
// relies on information about the beginning of the partial match
// that the BM algorithm does not have.
//
// The second loop addresses case 2. Since suffix_length may not be
// unique, we want to take the minimum value, which will tell us
// how far away the closest potential match is.
void make_delta2(int *delta2, const uint8_t *pattern, int patternLen, bool matchCase) {
  int p;
  int last_prefix_index = patternLen-1;

  // first loop
  for (p = patternLen - 1; p >= 0; p--) {
    if (is_prefix(pattern, patternLen, p + 1, matchCase)) {
      last_prefix_index = p + 1;
    }
    delta2[p] = last_prefix_index + (patternLen-1 - p);
  }
 
  // second loop
  for (p = 0; p < patternLen - 1; p++) {
    int slen = suffix_length(pattern, patternLen, p, matchCase);
    if (AsciiSearchBase::read_byte(pattern, p - slen, matchCase) != AsciiSearchBase::read_byte(pattern, patternLen - 1 - slen, matchCase)) {
      delta2[patternLen - 1 - slen] = patternLen - 1 - p + slen;
    }
  }
}


const uint8_t* boyer_moore_algo(const uint8_t* text, int textLen,
                                const uint8_t* pattern, int patternLen,
                                bool matchCase,
                                const int* delta1, const int* delta2) {
  int i = patternLen - 1;
  while (i < textLen) {
    int j = patternLen - 1;
    while (j >= 0 && (AsciiSearchBase::read_byte(text, i, matchCase) == AsciiSearchBase::read_byte(pattern, j, matchCase))) {
      --i;
      --j;
    }
    if (j < 0) {
      return (text + i + 1);
    }
 
    i += max(delta1[AsciiSearchBase::read_byte(text, i, matchCase)], delta2[j]);
  }
  return NULL;
}

}  // namespace

BoyerMooreSearch::BoyerMooreSearch()
    : pattern_(NULL),
      patternLen_(0),
      matchCase_(true) {
}

BoyerMooreSearch::~BoyerMooreSearch() {
  if (delta2_)
    free(delta2_);
}

bool BoyerMooreSearch::PreProcess(const char *pattern, int patternLen, SearchOptions options) {
  pattern_ = pattern;
  patternLen_ = patternLen;
  matchCase_ = (options & kMatchCase);
  delta2_ = (int *)malloc(patternLen * sizeof(int));
  if (delta2_ == NULL)
    return false;
  make_delta1(delta1_, kAlphabetLen, (const uint8_t*)pattern, patternLen, matchCase_);
  make_delta2(delta2_, (const uint8_t*)pattern, patternLen, matchCase_);
  return true;
}

#include "stdio.h"

void BoyerMooreSearch::Search(const char *text, int textLen, Callback matchFound) {
  const char* end = text + textLen;
  while(true) {
      const char* str = (const char*)boyer_moore_algo(
        (const uint8_t*)text,
        textLen,
        (const uint8_t*)pattern_,
        patternLen_,
        matchCase_,
        delta1_,
        delta2_);
      if (str == nullptr)
        break;

      if (!matchFound(str, patternLen_))
        break;

      text = str + patternLen_;
      textLen = (int)(end - text);
    }
}
