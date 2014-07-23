// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace VsChromium.DkmIntegration.IdeComponent
{
  // Summary:
  //     Base class for all custom visualizers.  Instances of all
  //     BasicVisualizer-derived classes are bound to a single visualized
  //     expression.  BasicVisualizer encapsulates logic for registering and
  //     executing handlers for visualizing child elements.
  public abstract class BasicVisualizer
  {
    private List<IChildEvaluator> childEvaluators_;
    private int childCount_;
    protected DkmVisualizedExpression expression_;

    public BasicVisualizer(DkmVisualizedExpression expression)
    {
      childEvaluators_ = new List<IChildEvaluator>();
      expression_ = expression;
    }

    protected void RegisterCustomChildEntry(string name, CustomEvaluationHandler evaluator)
    {
      RegisterChildEvaluator(new CustomChildEvaluator(expression_, name, evaluator));
    }

    protected void RegisterDefaultChildEntries(ChildDisplayMode mode)
    {
      RegisterChildEvaluator(new DefaultChildEvaluator(expression_, mode));
    }

    protected void RegisterChildEvaluator(IChildEvaluator evaluator)
    {
      childEvaluators_.Add(evaluator);
      childCount_ += evaluator.ChildCount;
    }

    public DkmChildVisualizedExpression[] GetChildItems(int startIndex, int count)
    {
      int end = startIndex + count - 1;

      DkmChildVisualizedExpression[] results = new DkmChildVisualizedExpression[count];
      int writeidx = 0;
      for (int i=0; i < childEvaluators_.Count; ++i)
      {
        int numWritten = 0;
        childEvaluators_[i].EvaluateChildren(results, startIndex + writeidx, out numWritten);
        writeidx += numWritten;
      }

      return results;
    }

    public int ChildElementCount
    {
      get { return childCount_; }
    }

    public abstract DkmEvaluationResult EvaluationResult { get; }
    public abstract ChildDisplayFlags ChildDisplayFlags { get; }
  }
}
