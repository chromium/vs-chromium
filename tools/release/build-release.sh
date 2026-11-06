#!/bin/sh
# Builds, on this machine, every release package that can be built here, into
# Binaries/Release/Packages/<version>/ with SHA256SUMS.txt (see docs/release.md). On Linux and
# macOS: the VS Code extensions, the server and vschromium-bench archives of all the platforms (not
# the Visual Studio extension, built on Windows only). The macOS executables are code signed
# (ad-hoc) only when built on macOS.
#
# Usage: tools/release/build-release.sh [--no-aot] [--out <directory>]
# Requires: .NET 10 SDK, Node.js (and clang + zlib development files for NativeAOT on Linux).
set -e
exec node "$(dirname "$0")/release.js" all "$@"
