// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq.Expressions;
using System.Reflection;

namespace VsChromium.Core.Utility {
  public static class ReflectionUtils {
    private static PropertyInfo GetPropertyInfoImpl(Type type, LambdaExpression propertyLambda) {
      var member = propertyLambda.Body as MemberExpression;
      if (member == null)
        throw new ArgumentException(string.Format(
          "Expression '{0}' refers to a method, not a property.",
          propertyLambda.ToString()));

      var propInfo = member.Member as PropertyInfo;
      if (propInfo == null)
        throw new ArgumentException(string.Format(
          "Expression '{0}' refers to a field, not a property.",
          propertyLambda.ToString()));

      if (type != null) {
        if (type != propInfo.ReflectedType &&
            !type.IsSubclassOf(propInfo.ReflectedType))
          throw new ArgumentException(
            string.Format(
              "Expresion '{0}' refers to a property that is not from type {1}.",
              propertyLambda.ToString(),
              type));
      }

      return propInfo;
    }

    /// <summary>
    /// Return the <see cref="PropertyInfo"/> for an instance property.
    /// </summary>
    public static PropertyInfo GetPropertyInfo<TSource, TProperty>(
        TSource source,
        Expression<Func<TSource, TProperty>> propertyLambda) {
      Type type = typeof(TSource);
      return GetPropertyInfoImpl(type, propertyLambda);
    }

    /// <summary>
    /// Return the <see cref="PropertyInfo"/> for a static property.
    /// </summary>
    public static PropertyInfo GetPropertyInfo<TProperty>(
        Expression<Func<TProperty>> propertyLambda) {
      return GetPropertyInfoImpl(null, propertyLambda);
    }

    /// <summary>
    /// Return the property name for an instance property.
    /// </summary>
    public static string GetPropertyName<TSource, TProperty>(
      TSource source,
      Expression<Func<TSource, TProperty>> propertyLambda) {
      return GetPropertyInfoImpl(typeof(TSource), propertyLambda).Name;
    }

    /// <summary>
    /// Return the property name for an static property.
    /// </summary>
    public static string GetPropertyName<TProperty>(
      Expression<Func<TProperty>> propertyLambda) {
      return GetPropertyInfoImpl(null, propertyLambda).Name;
    }
  }
}
