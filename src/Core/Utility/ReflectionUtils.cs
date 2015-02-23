// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Utility {
  public static class ReflectionUtils {
    private static MemberInfo GetMemberInfoImpl(Type type, LambdaExpression lambda) {
      var member = lambda.Body as MemberExpression;
      if (member == null)
        throw new ArgumentException(string.Format(
          "Expression '{0}' refers to a method, not a property.",
          lambda.ToString()));

      var memberInfo = member.Member;

      if (type != null) {
        if (type != memberInfo.ReflectedType &&
            !type.IsSubclassOf(memberInfo.ReflectedType))
          throw new ArgumentException(
            string.Format(
              "Expresion '{0}' refers to a property that is not from type {1}.",
              lambda.ToString(),
              type));
      }

      return memberInfo;
    }


    private static PropertyInfo GetPropertyInfoImpl(Type type, LambdaExpression propertyLambda) {
      var memberInfo = GetMemberInfoImpl(type, propertyLambda);

      var propInfo = memberInfo as PropertyInfo;
      if (propInfo == null)
        throw new ArgumentException(string.Format(
          "Expression '{0}' refers to a field, not a property.",
          propertyLambda.ToString()));

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
    /// Return the <see cref="MemberInfo"/> for an instance member.
    /// </summary>
    public static MemberInfo GetMemberInfo<TSource, TMember>(
        TSource source,
        Expression<Func<TSource, TMember>> propertyLambda) {
      Type type = typeof(TSource);
      return GetMemberInfoImpl(type, propertyLambda);
    }

    /// <summary>
    /// Return the <see cref="MemberInfo"/> for a static member.
    /// </summary>
    public static MemberInfo GetMemberInfo<TMember>(
      Expression<Func<TMember>> lambda) {
      return GetMemberInfoImpl(null, lambda);
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
    public static void CopyDeclaredPublicProperties(
        object source,
        string sourcePrefix,
        object destination,
        string destinationPrefix,
        bool throwOnExtraProperty) {
      sourcePrefix = sourcePrefix ?? "";
      destinationPrefix = destinationPrefix ?? "";
      var sourceProperties = source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
      var destinationProperties = destination.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
      foreach (var sourceProperty in sourceProperties) {
        var sourceValue = sourceProperty.GetValue(source);
        var destName = destinationPrefix + sourceProperty.Name.Substring(sourcePrefix.Length);
        var destinationProperty = destinationProperties.FirstOrDefault(x => x.Name == destName);
        if (destinationProperty != null) {
          destinationProperty.SetValue(destination, sourceValue);
          Logger.LogInfo("Copyging property value {0}-{5} from {1}.{2} to {3}.{4}",
            sourceValue,
            source.GetType().FullName, sourceProperty.Name,
            destination.GetType().FullName, destinationProperty.Name,
            destinationProperty.GetValue(destination));
        } else if (throwOnExtraProperty) {
          throw new InvalidOperationException(string.Format(
            "Property \"{0}\" in destination type \"{1}\" not found from property \"{2}\" in source type \"{3}\".", 
            destName, destination.GetType().FullName,
            sourceProperty.Name, source.GetType().FullName));
        }
      }
    }
  }
}
