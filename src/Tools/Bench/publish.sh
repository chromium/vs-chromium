#!/bin/sh
# Publishes vschromium-bench as self-contained single-file executables, one per platform, in
# Binaries/Publish/Bench/<rid>/ (see README.md):
#   - NativeAOT for the platforms of the OS running the script (NativeAOT can't cross-compile to
#     another OS; falls back to single-file if the native tools of the target are missing);
#   - single-file, trimmed, compressed (.NET runtime included) for the other platforms.
#
# Usage: src/Tools/Bench/publish.sh [rid...]   (default: win-x64 win-arm64 linux-x64 linux-arm64 osx-arm64)
#        NO_AOT=1 src/Tools/Bench/publish.sh   (single-file only)
set -u
here=$(cd "$(dirname "$0")" && pwd)
project="$here/Bench.csproj"
publish_root="$here/../../../Binaries/Publish/Bench"
configuration=${CONFIGURATION:-Release}
case "$(uname -s)" in
  Darwin) host_os=osx ;;
  Linux) host_os=linux ;;
  *) host_os=win ;;
esac
[ $# -gt 0 ] || set -- win-x64 win-arm64 linux-x64 linux-arm64 osx-arm64

summary=""
for rid in "$@"; do
  output="$publish_root/$rid"
  rm -rf "$output"
  mode=single-file
  ok=0
  if [ -z "${NO_AOT:-}" ] && [ "${rid%%-*}" = "$host_os" ]; then
    echo "== $rid (NativeAOT)"
    if dotnet publish "$project" -c "$configuration" -r "$rid" -p:BenchAot=true -o "$output" --nologo -v q; then
      mode=NativeAOT
      ok=1
    else
      echo "NativeAOT publish failed for $rid: falling back to single-file."
      rm -rf "$output"
    fi
  fi
  if [ $ok -eq 0 ]; then
    echo "== $rid (single-file, trimmed)"
    if dotnet publish "$project" -c "$configuration" -r "$rid" -p:BenchAot=false -o "$output" --nologo -v q; then
      ok=1
    fi
  fi
  if [ $ok -eq 0 ]; then
    summary="$summary
$rid FAILED"
    continue
  fi
  # Symbols are not shipped.
  find "$output" \( -name '*.pdb' -o -name '*.dbg' -o -name '*.dSYM' \) -prune -exec rm -rf {} +
  exe=$(ls "$output"/vschromium-bench* | head -n 1)
  size=$(wc -c < "$exe" | tr -d ' ')
  summary="$summary
$rid $mode $((size / 1048576)) MB $exe"
done
echo "$summary"
