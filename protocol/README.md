# VsChromium protocol

The messages exchanged between `VsChromium.Server` and its clients (the Visual Studio extension and
the VS Code extension): **JSON-RPC 2.0** over the standard input/output of the server.

This directory is the **single source of truth** of the protocol. The code of the server and of the
clients is generated from it by [src/Tools/ProtocolGen](../src/Tools/ProtocolGen), on every build
(incrementally: only when a `.tsp` file or the generator changed):

| Output | Generated file | Status |
|---|---|---|
| Server (C#, System.Text.Json source generation, NativeAOT compatible) | [src/Core.Server/Protocol/Protocol.g.cs](../src/Core.Server/Protocol/Protocol.g.cs) | Generated |
| Visual Studio extension (C#, StreamJsonRpc) | to come (migration phase P4) | |
| VS Code extension (TypeScript, vscode-jsonrpc) | to come (migration phase P3) | |

The generated files are checked in (diffs show the effect of protocol changes). The test
`ProtocolGenTests.RepositoryGeneratedFilesAreUpToDate` fails if a checked-in file is stale.

## Language: a subset of TypeSpec

The files use a subset of [TypeSpec](https://typespec.io), parsed by our own generator (no Node.js
needed to build). They are valid TypeSpec: the official editor extensions ("TypeSpec for Visual
Studio", "TypeSpec for VS Code") give syntax colors, errors and navigation, and the official
compiler accepts them (`tsp compile protocol/main.tsp --no-emit`, checked with TypeSpec 1.0).

Supported:

```typespec
namespace VsChromium.Protocol;          // one file level namespace (the same in all the files)
import "./other.tsp";                   // imports (for the TypeSpec tools; the generator reads all the files)

/** Documentation comments are copied to the generated code. */
model SearchParams {
  query: SearchQuery;                   // model reference
  maxResults: int32;                    // scalars: string, boolean, int32, int64, float64, utcDateTime
  patterns: string[];                   // arrays
  matchCase?: boolean;                  // optional property (omitted from the JSON when not set)
  ...DetailsParams;                     // spread: copies the properties of another model
}

enum IndexingStatus { idle, paused, `yield`, busy }   // serialized as strings ("idle")

interface Search {                      // requests (client -> server): method "search/code"
  code(...SearchParams): SearchCodeResult;            // parameters: the properties of a model
  register(path: string): void;                       // inline parameters, no result
  refresh(): void;                                    // no parameters
}

interface IndexNotifications {          // notifications (server -> client): method "index/stateChanged"
  stateChanged(...IndexStateChangedParams): void;
}
```

Not supported (reported as errors by the generator, with the location): decorators, directives,
templates, inheritance (`extends`, `is`), unions and literal types, aliases, scalars declarations,
anonymous models, default values, namespace blocks, other scalar types.

## Rules

- **Names**: models, enums and interfaces in PascalCase; properties, parameters, operations and enum
  members in camelCase: they are the JSON names.
- **Methods**: `<group>/<operation>`, where the group is the interface name in camelCase. Interfaces
  whose name ends with `Notifications` declare notifications sent by the server (operations
  returning `void`); the group is the name without the suffix (`IndexNotifications` -> `index/...`).
- **Parameters** are always an object (JSON-RPC named parameters): the properties of the spread model,
  or of a model generated from the inline parameters (`<Group><Operation>Params`, e.g.
  `FilesRegisterParams`).
- **Protobuf compatibility** (every message can be expressed in proto3): results are models (not
  scalars or arrays), no optional arrays (use an empty array), no arrays of arrays, no inheritance
  or polymorphism, no maps.
- **JSON**: camelCase properties, enums as strings, `utcDateTime` as RFC 3339 strings, 64 bit integers
  as JSON numbers (the server also accepts strings, as written by the protobuf JSON mapping).

## Methods

| Method | Former request (typed messages protocol) |
|---|---|
| `server/initialize` | (hello protocol) |
| `server/statistics` | `GetServerStatisticsRequest` |
| `files/register`, `files/unregister` | `RegisterFileRequest`, `UnregisterFileRequest` |
| `files/extracts` | `GetFileExtractsRequest` |
| `project/setOptions`, `project/details` | `SetProjectOptionsRequest`, `GetProjectDetailsRequest` |
| `search/code`, `search/paths` | `SearchCodeRequest`, `SearchFilePathsRequest` (results: flat lists of files per project instead of a directory tree) |
| `tree/get`, `tree/entries`, `tree/entriesMultiple`, `tree/refresh` | `GetFileSystemTreeRequest`, `GetDirectoryEntriesRequest`, `GetDirectoryEntriesMultipleRequest`, `RefreshFileSystemTreeRequest` |
| `tree/directoryDetails`, `tree/directoryStatistics` | `GetDirectoryDetailsRequest`, `GetDirectoryStatisticsRequest` |
| `index/pause`, `index/resume` | `PauseIndexingRequest`, `ResumeIndexingRequest` |
| `index/statistics`, `index/details` | `GetDatabaseStatisticsRequest`, `GetDatabaseDetailsRequest` |
| Notifications `index/stateChanged`, `index/scanStarted`, `index/scanFinished`, `index/loading`, `index/loadingProgress`, `index/loaded`, `index/progress` | `IndexingServerStateChangedEvent`, `FileSystemScanStarted/Finished`, `SearchEngineFilesLoading/LoadingProgress/Loaded`, `ProgressReportEvent` |
| (dropped) | `GetFileSystemRequest` (obsolete), `GetFileSystemVersionRequest` (unused) |

Cancellation (`$/cancelRequest`) and the framing (`Content-Length` headers, as LSP) are part of the
JSON-RPC transport, not of the definition.
