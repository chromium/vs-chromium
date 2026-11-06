# Releasing VsChromium

A release is a set of packages built from a tag `v<version>`. The workflow creates a **draft**
GitHub release, and you publish it by hand after review. Nothing is published to the VS Code or
Visual Studio marketplaces.

## Packages

| File | Content |
| --- | --- |
| `vs-chromium-<version>-<target>.vsix` | VS Code extension for one platform, with the self-contained server of that platform in `extension/bin/`. There are six packages: `win32-x64`, `win32-arm64`, `linux-x64`, `linux-arm64`, `darwin-x64`, `darwin-arm64`. |
| `vs-chromium-<version>-visualstudio.vsix` | Visual Studio 2022/2026 extension for x64 and ARM64 Visual Studio, with the `win-x64` and `win-arm64` self-contained servers (`Server\win-x64\`, `Server\win-arm64\`). |
| `vschromium-server-<version>-<rid>.zip` | Standalone server (self-contained, single file, ReadyToRun), with its default configuration. vschromium-bench downloads these files (`--server release:<tag>`). |
| `vschromium-bench-<version>-<rid>.zip` | vschromium-bench (NativeAOT when built on its platform, single file otherwise). |
| `SHA256SUMS.txt` | SHA-256 of all the files, in `sha256sum` format (`sha256sum -c SHA256SUMS.txt`). |

The `<rid>` (.NET runtime identifier) values are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-x64` and `osx-arm64`. VS Code uses its own names for the same platforms (`vsce package
--target`):

| .NET runtime identifier | VS Code target |
| --- | --- |
| `win-x64` | `win32-x64` |
| `win-arm64` | `win32-arm64` |
| `linux-x64` | `linux-x64` |
| `linux-arm64` | `linux-arm64` |
| `osx-x64` | `darwin-x64` |
| `osx-arm64` | `darwin-arm64` |

This mapping is defined in three places, which must stay in sync: `tools/release/release.js`,
`src/vscode/scripts/copy-server.js` and `ReleaseAssets.VsCodeTarget` (`src/Tools/Bench`).

On ARM64 Windows, the Visual Studio extension starts the native ARM64 server, even when Visual
Studio itself runs as an emulated x64 process (`ServerLocation`, `src/VsChromium/ServerProxy`).

## 1. Bump the version

The `VERSION` file at the root of the repository holds the version (`major.minor.patch`).
`tools/release/set-version.js` copies it into every file that contains the version: the server and
Visual Studio `VsChromiumVersion.cs` files, the VSIX manifest, the VS Code `package.json` and
vschromium-bench.

```sh
node tools/release/set-version.js 0.10.1
git commit -am "Version 0.10.1"
```

`node tools/release/set-version.js --check` checks that the files match `VERSION`. CI runs it in
`server.yml`.

## 2. Write the release notes

Write `docs/release-notes/<version>.md`. This file is the body of the GitHub release, and the release
workflow fails if it is missing. Write for users, and group the changes: Highlights, VS Code,
Visual Studio, Server, Performance, Compatibility notes. Use `git log v<previous>..HEAD` to list the
changes.

## 3. Build locally (optional, recommended)

This builds every package that can be built on this machine, with the same names as the release,
into `Binaries/Release/Packages/<version>/`. It then verifies the packages and writes
`SHA256SUMS.txt`.

```sh
pwsh tools/release/build-release.ps1      # Windows
tools/release/build-release.sh            # Linux, macOS
```

The script runs `node tools/release/release.js all`. It requires the .NET 10 SDK and Node.js. On
Windows, it also requires Visual Studio 2022/2026 with the "Visual Studio extension development"
workload, for the Visual Studio extension. On Linux, NativeAOT requires clang and zlib development
files.

What gets built:

- **Windows**: all the packages. The servers of the other platforms are cross-published. Only
  `win-x64` vschromium-bench is NativeAOT; the other platforms are single file (NativeAOT can't
  cross-compile to another OS, and `win-arm64` needs the ARM64 build tools). The macOS executables
  are **not code signed**: macOS refuses to run them on Apple Silicon. The script prints a
  warning for each one.
- **Linux, macOS**: all the packages except the Visual Studio extension. The macOS executables are
  signed (ad-hoc) only when built on macOS.

The script verifies each package:

- the executables are for the right OS and architecture (PE, ELF and Mach-O headers);
- the executables are marked executable in the archives;
- the VSIX manifests have the right version and target platform (amd64 and arm64 for Visual
  Studio);
- the servers and their configuration are in the VSIX files;
- the executables of this platform pass a smoke test (`--help`).

The individual steps can be run separately. See the comments at the top of
`tools/release/release.js`:

```sh
node tools/release/release.js server win-x64 linux-x64
node tools/release/release.js bench win-x64
node tools/release/release.js vscode win32-x64 --no-publish
node tools/release/release.js visualstudio --no-publish
node tools/release/release.js verify
node tools/release/release.js checksums
```

To test the VS Code extension, run `code --install-extension
Binaries/Release/Packages/<version>/vs-chromium-<version>-win32-x64.vsix`. To test the Visual Studio
extension, open `vs-chromium-<version>-visualstudio.vsix`.

## 4. Tag and push

```sh
git tag v0.10.1
git push origin v0.10.1
```

The tag push starts `.github/workflows/release.yml`:

1. **check**: the tag must be `v` + `VERSION`, every file must have the version, and the release
   notes must exist.
2. **build**: one job per platform, on a runner of that platform (`windows-latest`,
   `windows-11-arm`, `ubuntu-24.04`, `ubuntu-24.04-arm`, `macos-15-intel`, `macos-15`). Each job
   builds the server archive, vschromium-bench (NativeAOT) and the VS Code extension of its
   platform. It runs the smoke tests, and on macOS it signs the executables (ad-hoc) and checks
   them with `codesign --verify`.
3. **visualstudio**: the Visual Studio extension, with the Windows servers of the build jobs.
4. **release**: verifies the complete set of packages, writes `SHA256SUMS.txt`, uploads everything
   as a workflow artifact, and runs `gh release create v<version> --draft`.

To try the workflow without creating a release, run it by hand (**Actions > release > Run
workflow**) with "Dry run" checked (the default). It builds and verifies everything, and the
packages are available as the `vschromium-<version>` workflow artifact. Running it on a tag with
"Dry run" unchecked creates the draft release for that tag.

## 5. Review and publish

On GitHub, open **Releases** and review the draft: the notes and the 19 assets. Download and try a
package or two, then **Publish release**.

## Signing

| Package | Signing |
| --- | --- |
| macOS executables (server, vschromium-bench) | Ad-hoc code signature (`codesign --sign -`), applied on the macOS runners. This is required to run on Apple Silicon. Not notarized: macOS may quarantine files downloaded with a browser (`xattr -d com.apple.quarantine <file>`). VS Code extensions installed from a VSIX are not quarantined. |
| Windows executables | Not Authenticode signed. |
| Linux executables | Not signed. |
| VSIX files | Not signed (the marketplaces are not used). |
| Assemblies | Strong named (`Build/VsChromiumKey.snk`), as before. |

`SHA256SUMS.txt` lets users check the downloads.
