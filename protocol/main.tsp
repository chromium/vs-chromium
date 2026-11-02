// VsChromium protocol: messages exchanged between VsChromium.Server and its clients (Visual Studio
// extension, VS Code extension), as JSON-RPC 2.0 over the standard input/output of the server.
//
// This directory is the single source of truth of the protocol: the C# and TypeScript code of the
// server and the clients is generated from it by src/Tools/ProtocolGen (on every build). The files
// use a subset of TypeSpec (https://typespec.io), see README.md for the rules.

import "./common.tsp";
import "./server.tsp";
import "./files.tsp";
import "./project.tsp";
import "./search.tsp";
import "./tree.tsp";
import "./index.tsp";
