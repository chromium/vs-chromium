// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Text.Json.Serialization;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Core.Ipc.Json {
  /// <summary>
  /// System.Text.Json source generated metadata of the protocol message classes: no reflection at
  /// run time (NativeAOT and trimming compatible). The options of the protocol (camelCase, enums as
  /// strings, "$type" discriminators, ...) are applied by <see cref="IpcJsonSerializer"/>.
  ///
  /// Every message class must be listed here and in <see cref="MessageTypes"/> (checked by the
  /// <c>TestIpcJsonSerializer</c> tests).
  /// </summary>
  [JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    Converters = new[] { typeof(UtcDateTimeConverter) })]
  [JsonSerializable(typeof(DirectoryDetails))]
  [JsonSerializable(typeof(DirectoryEntry))]
  [JsonSerializable(typeof(DoneResponse))]
  [JsonSerializable(typeof(ErrorResponse))]
  [JsonSerializable(typeof(FileByExtensionDetails))]
  [JsonSerializable(typeof(FileEntry))]
  [JsonSerializable(typeof(FileExtract))]
  [JsonSerializable(typeof(FilePositionsData))]
  [JsonSerializable(typeof(FilePositionSpan))]
  [JsonSerializable(typeof(FileSystemEntry))]
  [JsonSerializable(typeof(FileSystemEntryData))]
  [JsonSerializable(typeof(FileSystemScanFinished))]
  [JsonSerializable(typeof(FileSystemScanStarted))]
  [JsonSerializable(typeof(FileSystemTree))]
  [JsonSerializable(typeof(FileSystemTree_Obsolete))]
  [JsonSerializable(typeof(GetDatabaseDetailsRequest))]
  [JsonSerializable(typeof(GetDatabaseDetailsResponse))]
  [JsonSerializable(typeof(GetDatabaseStatisticsRequest))]
  [JsonSerializable(typeof(GetDatabaseStatisticsResponse))]
  [JsonSerializable(typeof(GetDirectoryDetailsRequest))]
  [JsonSerializable(typeof(GetDirectoryDetailsResponse))]
  [JsonSerializable(typeof(GetDirectoryEntriesMultipleRequest))]
  [JsonSerializable(typeof(GetDirectoryEntriesMultipleResponse))]
  [JsonSerializable(typeof(GetDirectoryEntriesRequest))]
  [JsonSerializable(typeof(GetDirectoryEntriesResponse))]
  [JsonSerializable(typeof(GetDirectoryStatisticsRequest))]
  [JsonSerializable(typeof(GetDirectoryStatisticsResponse))]
  [JsonSerializable(typeof(GetFileExtractsRequest))]
  [JsonSerializable(typeof(GetFileExtractsResponse))]
  [JsonSerializable(typeof(GetFileSystemRequest))]
  [JsonSerializable(typeof(GetFileSystemResponse))]
  [JsonSerializable(typeof(GetFileSystemTreeRequest))]
  [JsonSerializable(typeof(GetFileSystemTreeResponse))]
  [JsonSerializable(typeof(GetFileSystemVersionRequest))]
  [JsonSerializable(typeof(GetFileSystemVersionResponse))]
  [JsonSerializable(typeof(GetProjectDetailsRequest))]
  [JsonSerializable(typeof(GetProjectDetailsResponse))]
  [JsonSerializable(typeof(GetServerStatisticsRequest))]
  [JsonSerializable(typeof(GetServerStatisticsResponse))]
  [JsonSerializable(typeof(IndexingServerStateChangedEvent))]
  [JsonSerializable(typeof(IpcEvent))]
  [JsonSerializable(typeof(IpcMessage))]
  [JsonSerializable(typeof(IpcMessageData))]
  [JsonSerializable(typeof(IpcRequest))]
  [JsonSerializable(typeof(IpcResponse))]
  [JsonSerializable(typeof(IpcStringData))]
  [JsonSerializable(typeof(LargeFileDetails))]
  [JsonSerializable(typeof(OperationTiming))]
  [JsonSerializable(typeof(OptionalDirectoryEntry))]
  [JsonSerializable(typeof(PairedTypedEvent))]
  [JsonSerializable(typeof(PauseIndexingRequest))]
  [JsonSerializable(typeof(PauseResumeIndexingResponse))]
  [JsonSerializable(typeof(ProgressReportEvent))]
  [JsonSerializable(typeof(ProjectConfigurationDetails))]
  [JsonSerializable(typeof(ProjectConfigurationSectionDetails))]
  [JsonSerializable(typeof(ProjectDetails))]
  [JsonSerializable(typeof(ProjectEntry))]
  [JsonSerializable(typeof(RefreshFileSystemTreeRequest))]
  [JsonSerializable(typeof(RefreshFileSystemTreeResponse))]
  [JsonSerializable(typeof(RegisterFileRequest))]
  [JsonSerializable(typeof(ResumeIndexingRequest))]
  [JsonSerializable(typeof(SearchCodeRequest))]
  [JsonSerializable(typeof(SearchCodeResponse))]
  [JsonSerializable(typeof(SearchEngineFilesLoaded))]
  [JsonSerializable(typeof(SearchEngineFilesLoading))]
  [JsonSerializable(typeof(SearchEngineFilesLoadingProgress))]
  [JsonSerializable(typeof(SearchFilePathsRequest))]
  [JsonSerializable(typeof(SearchFilePathsResponse))]
  [JsonSerializable(typeof(SearchParams))]
  [JsonSerializable(typeof(TypedEvent))]
  [JsonSerializable(typeof(TypedMessage))]
  [JsonSerializable(typeof(TypedRequest))]
  [JsonSerializable(typeof(TypedResponse))]
  [JsonSerializable(typeof(UnregisterFileRequest))]
  internal partial class IpcJsonContext : JsonSerializerContext {
  }

  /// <summary>
  /// The list of the protocol message classes, used to declare the "$type" discriminators of the
  /// polymorphic values (see <see cref="IpcJsonSerializer"/>). Same list as <see
  /// cref="IpcJsonContext"/>.
  /// </summary>
  public static class MessageTypes {
    public static readonly Type[] All = {
      typeof(DirectoryDetails),
      typeof(DirectoryEntry),
      typeof(DoneResponse),
      typeof(ErrorResponse),
      typeof(FileByExtensionDetails),
      typeof(FileEntry),
      typeof(FileExtract),
      typeof(FilePositionsData),
      typeof(FilePositionSpan),
      typeof(FileSystemEntry),
      typeof(FileSystemEntryData),
      typeof(FileSystemScanFinished),
      typeof(FileSystemScanStarted),
      typeof(FileSystemTree),
      typeof(FileSystemTree_Obsolete),
      typeof(GetDatabaseDetailsRequest),
      typeof(GetDatabaseDetailsResponse),
      typeof(GetDatabaseStatisticsRequest),
      typeof(GetDatabaseStatisticsResponse),
      typeof(GetDirectoryDetailsRequest),
      typeof(GetDirectoryDetailsResponse),
      typeof(GetDirectoryEntriesMultipleRequest),
      typeof(GetDirectoryEntriesMultipleResponse),
      typeof(GetDirectoryEntriesRequest),
      typeof(GetDirectoryEntriesResponse),
      typeof(GetDirectoryStatisticsRequest),
      typeof(GetDirectoryStatisticsResponse),
      typeof(GetFileExtractsRequest),
      typeof(GetFileExtractsResponse),
      typeof(GetFileSystemRequest),
      typeof(GetFileSystemResponse),
      typeof(GetFileSystemTreeRequest),
      typeof(GetFileSystemTreeResponse),
      typeof(GetFileSystemVersionRequest),
      typeof(GetFileSystemVersionResponse),
      typeof(GetProjectDetailsRequest),
      typeof(GetProjectDetailsResponse),
      typeof(GetServerStatisticsRequest),
      typeof(GetServerStatisticsResponse),
      typeof(IndexingServerStateChangedEvent),
      typeof(IpcEvent),
      typeof(IpcMessage),
      typeof(IpcMessageData),
      typeof(IpcRequest),
      typeof(IpcResponse),
      typeof(IpcStringData),
      typeof(LargeFileDetails),
      typeof(OperationTiming),
      typeof(OptionalDirectoryEntry),
      typeof(PairedTypedEvent),
      typeof(PauseIndexingRequest),
      typeof(PauseResumeIndexingResponse),
      typeof(ProgressReportEvent),
      typeof(ProjectConfigurationDetails),
      typeof(ProjectConfigurationSectionDetails),
      typeof(ProjectDetails),
      typeof(ProjectEntry),
      typeof(RefreshFileSystemTreeRequest),
      typeof(RefreshFileSystemTreeResponse),
      typeof(RegisterFileRequest),
      typeof(ResumeIndexingRequest),
      typeof(SearchCodeRequest),
      typeof(SearchCodeResponse),
      typeof(SearchEngineFilesLoaded),
      typeof(SearchEngineFilesLoading),
      typeof(SearchEngineFilesLoadingProgress),
      typeof(SearchFilePathsRequest),
      typeof(SearchFilePathsResponse),
      typeof(SearchParams),
      typeof(TypedEvent),
      typeof(TypedMessage),
      typeof(TypedRequest),
      typeof(TypedResponse),
      typeof(UnregisterFileRequest),
    };
  }
}
