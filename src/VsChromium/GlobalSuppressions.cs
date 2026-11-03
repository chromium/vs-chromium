// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Reliability", "VSSDK006:Check services exist", Justification = "Migration")]
[assembly: SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Migration")]
[assembly: SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Migration")]
[assembly: SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "Migration")]
[assembly: SuppressMessage("Usage", "VSTHRD011:Use AsyncLazy<T>", Justification = "Migration")]
[assembly: SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "Migration")]
[assembly: SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Migration")]
