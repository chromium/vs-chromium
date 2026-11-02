# VsChromium end-to-end tests and benchmarks

Tests of the indexing server (`VsChromium.Server.exe`) driven as a **black box** over its stdio JSON
protocol (`--stdio`), so they work with any version of the server: the current .NET Framework server
and the portable server of the port (see
[PORTING_PRINCIPLES.md](../../docs/portable-server/PORTING_PRINCIPLES.md)).

Build the solution first (the tests use `Binaries/<Configuration>/VsChromium.Server.exe`), then:

```
cd src/Tests.EndToEnd
dotnet test --filter "TestCategory!=Benchmark"   # regression and end-to-end tests
dotnet test --filter "TestCategory=Benchmark"    # benchmarks
```

## Contents

| Directory | Content |
|---|---|
| `Server/` | Stdio JSON client (`StdioServerClient`) and protocol helpers |
| `Corpus/` | Generator of real-world-like code bases (`CodeBaseGenerator`) and their on-disk cache (`CorpusCache`) |
| `Regression/` | Regression tests of the behavior changes of the port. Tests of known bugs of the current server are reported as **skipped** (inconclusive) with the reason, and **fail** once the bug is fixed, as a reminder to remove the `KnownBug` marker |
| `Benchmarks/` | Benchmarks: cold indexing, idle/steady state memory, code/file path searches, extracts. Measure response time, CPU time and memory (working set and private bytes, which include native memory, sampled from outside the server process every 50 ms, plus the server's own `GetServerStatisticsRequest`) |

## Settings (environment variables)

| Variable | Default | Description |
|---|---|---|
| `VSCHROMIUM_SERVER_PATH` | `<repo>/Binaries/<Configuration>/VsChromium.Server.exe` | Server executable under test |
| `VSCHROMIUM_TEST_CORPORA` | `%LOCALAPPDATA%/VsChromium/TestCorpora` | Where generated code bases are kept |
| `VSCHROMIUM_BENCHMARK_SIZE` | `small` | `small` (3k files), `medium` (40k files), `large` (400k files, ~8 GB) |
| `VSCHROMIUM_BENCHMARK_RESULTS` | `src/Tests.EndToEnd/bin/BenchmarkResults` | Output directory of benchmark results (`.json` and `.md`) |
| `VSCHROMIUM_BENCHMARK_BASELINE` | (none) | Result `.json` of a previous run: writes a `.comparison.md` report |

## Generated code bases

Code bases are generated deterministically (same files for the same size, seed and generator
version) and **kept between test runs**: running the tests again reuses them instead of deleting and
re-creating hundreds of thousands of files. A `<corpus>.manifest.json` file, written next to the
corpus directory once generation is complete, marks a corpus as reusable; an incomplete or outdated
corpus is re-generated automatically. Delete the corpora directory to reclaim the disk space.

Notes:

- On Windows, process CPU times have a resolution of ~15.6 ms: the per-query CPU numbers of very fast
  queries are only meaningful over many runs (or on larger code bases).
- Benchmark the `Release` build of the server (`VSCHROMIUM_SERVER_PATH`, or build this project in
  `Release`).
