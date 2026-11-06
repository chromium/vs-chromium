# Builds, on this machine, every release package that can be built here, into
# Binaries/Release/Packages/<version>/ with SHA256SUMS.txt (see docs/release.md). On Windows: all
# the packages (the macOS executables are not code signed: build them on macOS for a release).
#
# Usage: pwsh tools/release/build-release.ps1 [--no-aot] [--out <directory>]
# Requires: .NET 10 SDK, Node.js, Visual Studio 2022/2026 with the "Visual Studio extension
# development" workload (for the Visual Studio extension).
$ErrorActionPreference = 'Stop'
node (Join-Path $PSScriptRoot 'release.js') all @args
exit $LASTEXITCODE
