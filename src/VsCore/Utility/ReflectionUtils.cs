// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

    /// <summary>
    /// Return the default value of a <see cref="Type"/> value.
    /// Runtime equivalent of <code>default(T)</code>.
    /// </summary>
    public static object GetTypeDefaultValue(Type type) {
      if (type.IsValueType) {
        return Activator.CreateInstance(type);
      }
      return null;
    }

    /// <summary>
    /// Copy properties from one object instance to another. The property names
    /// must match exactly, or match according to the source/destination
    /// prefix. <paramref name="throwOnExtraProperty"/> is true if the method
    /// throws when the source instance contains public properties not present
    /// in the destination instance.
    /// </summary>
    public static void CopyDeclaredPublicProperties(
        object source,
        string sourcePrefix,
        object destination,
        string destinationPrefix,
        bool throwOnExtraProperty) {
      sourcePrefix = sourcePrefix ?? "";
      destinationPrefix = destinationPrefix ?? "";
      const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
      var sourceProperties = source.GetType().GetProperties(bindingFlags);
      var destinationProperties = destination.GetType().GetProperties(bindingFlags);
      foreach (var sourceProperty in sourceProperties) {
        var destinationPropertyName = 
          GetDestinationPropertyName(sourcePrefix, destinationPrefix, sourceProperty);
        var destinationProperty = destinationProperties
          .FirstOrDefault(x => x.Name == destinationPropertyName);
        if (destinationProperty != null) {
          var sourceValue = sourceProperty.GetValue(source);
          destinationProperty.SetValue(destination, sourceValue);
        } else if (throwOnExtraProperty) {
          throw new InvalidOperationException(string.Format(
            "Property \"{0}\" in source type \"{1}\" does not have an " + 
            "equivalent property in destination type \"{2}\".", 
            sourceProperty.Name,
            source.GetType().FullName,
            destination.GetType().FullName));
        }
      }
    }

    private static string GetDestinationPropertyName(string sourcePrefix, string destinationPrefix, PropertyInfo sourceProperty) {
      // The source property may not exist...
      if (sourceProperty.Name.Length < sourcePrefix.Length)
        return "";

      return destinationPrefix + sourceProperty.Name.Substring(sourcePrefix.Length);
    }
  }
}
