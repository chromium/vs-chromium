# Publishes vschromium-bench as self-contained single-file executables, one per platform, in
# Binaries/Publish/Bench/<rid>/ (see README.md):
#   - NativeAOT for the platforms of the OS running the script (NativeAOT can't cross-compile to
#     another OS; a different architecture of the same OS needs the native tools of that
#     architecture: falls back to single-file if they're missing);
#   - single-file, trimmed, compressed (.NET runtime included) for the other platforms.
#
# Usage: pwsh src/Tools/Bench/publish.ps1 [-Rids win-x64,linux-x64] [-NoAot] [-Configuration Release]
param(
  [string[]]$Rids = @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-arm64'),
  [string]$Configuration = 'Release',
  [switch]$NoAot
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Bench.csproj'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$publishRoot = Join-Path $root 'Binaries\Publish\Bench'

if ($IsWindows) { $hostOs = 'win' } elseif ($IsMacOS) { $hostOs = 'osx' } else { $hostOs = 'linux' }

$summary = @()
foreach ($rid in $Rids) {
  $output = Join-Path $publishRoot $rid
  if (Test-Path $output) { Remove-Item -Recurse -Force $output }
  $ridOs = $rid.Split('-')[0]
  $mode = 'single-file'
  $ok = $false
  if (-not $NoAot -and $ridOs -eq $hostOs) {
    Write-Host "== $rid (NativeAOT)"
    dotnet publish $project -c $Configuration -r $rid -p:BenchAot=true -o $output --nologo -v q
    if ($LASTEXITCODE -eq 0) {
      $mode = 'NativeAOT'
      $ok = $true
    } else {
      Write-Host "NativeAOT publish failed for ${rid}: falling back to single-file."
      if (Test-Path $output) { Remove-Item -Recurse -Force $output }
    }
  }
  if (-not $ok) {
    Write-Host "== $rid (single-file, trimmed)"
    dotnet publish $project -c $Configuration -r $rid -p:BenchAot=false -o $output --nologo -v q
    $ok = $LASTEXITCODE -eq 0
  }
  if (-not $ok) {
    $summary += "{0,-12} FAILED" -f $rid
    continue
  }
  # Symbols are not shipped.
  Get-ChildItem $output -Include *.pdb, *.dbg, *.dSYM -Recurse | Remove-Item -Recurse -Force
  $exe = Get-ChildItem $output -File | Where-Object { $_.Name -like 'vschromium-bench*' } | Select-Object -First 1
  $summary += "{0,-12} {1,-12} {2,8:n1} MB  {3}" -f $rid, $mode, ($exe.Length / 1MB), $exe.FullName
}

Write-Host ''
$summary | ForEach-Object { Write-Host $_ }
