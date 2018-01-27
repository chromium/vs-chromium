// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Utility {
  public static class HashCode {
    private static readonly int[] Primes = {
      3,
      7,
      11,
      17,
      23,
      29,
      37,
      47,
      59,
      71,
      89,
      107,
      131,
      163,
      197,
      239,
      293,
      353,
      431,
      521,
      631,
      761,
      919,
      1103,
      1327,
      1597,
      1931,
      2333,
      2801,
      3371,
      4049,
      4861,
      5839,
      7013,
      8419,
      10103,
      12143,
      14591,
      17519,
      21023,
      25229,
      30293,
      36353,
      43627,
      52361,
      62851,
      75431,
      90523,
      108631,
      130363,
      156437,
      187751,
      225307,
      270371,
      324449,
      389357,
      467237,
      560689,
      672827,
      807403,
      968897,
      1162687,
      1395263,
      1674319,
      2009191,
      2411033,
      2893249,
      3471899,
      4166287,
      4999559,
      5999471,
      7199369
    };

    public static int Combine(int h1, int h2) {
      // http://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
      unchecked {
        int hash = 17;
        hash = hash * 31 + h1;
        hash = hash * 31 + h2;
        return hash;
      }
    }

    public static int Combine(int h1, int h2, int h3) {
      return Combine(Combine(h1, h2), h3);
    }

    public static int Combine(int h1, int h2, int h3, int h4) {
      return Combine(Combine(h1, h2), Combine(h3, h4));
    }

    public static int GetPrime(int min) {
      Invariants.CheckArgument(min >= 0, nameof(min), "Invalid prime seed");
      for (int index = 0; index < Primes.Length; ++index) {
        int prime = Primes[index];
        if (prime >= min)
          return prime;
      }

      int candidate = min | 1;
      while (candidate < int.MaxValue) {
        if (IsPrime(candidate) && (candidate - 1) % 101 != 0)
          return candidate;
        candidate += 2;
      }

      return min;
    }

    public static bool IsPrime(int candidate) {
      if ((candidate & 1) == 0)
        return candidate == 2;
      int num1 = (int)Math.Sqrt(candidate);
      int num2 = 3;
      while (num2 <= num1) {
        if (candidate % num2 == 0)
          return false;
        num2 += 2;
      }
      return true;
    }

  }
}