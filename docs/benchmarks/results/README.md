# Benchmark results

Shareable reports of [vschromium-bench](../../../src/Tools/Bench/README.md) runs, committed for
history: one Markdown file per run, written by the bench after each run (or with
`vschromium-bench report <results.json>` for an existing results file).

File name: `<yyyy-MM-dd>-<machine id>-<file system>[-devdrive]-<corpus>.md`, e.g.
`2026-11-06-0029b307-refs-devdrive-400k.md`.

Each report has:

- the date (UTC), the bench and server versions (version and release tag, never a local path);
- the machine: OS edition, version and build, CPU model, cores and clocks, RAM (type and speed when
  readable without admin), GPUs and driver versions, power plan;
- all the fixed drives (file system, Dev Drive, size, free space, media, model, bus, change journal)
  and the drive of the test;
- the security software (product names, Defender mode, filter drivers when listed elevated), unless
  the run used `--redact-security`;
- the corpus (generator version, counts, files per directory histogram), the options, warm or cold
  file cache;
- the results of every scenario (median, min, max of the repeats);
- the non-interactive command line, with the paths replaced by placeholders (`<corpora-dir>`,
  `<server>`, ...).

No identifying data: no machine, user or domain name, no path, no volume label, no serial number or
hardware id. The machine id is a random GUID created once in the data directory of the bench
(first 8 hex digits shown): reports of the same machine can be grouped, nothing more. The optional
`--machine-label` text is included verbatim. The bench scrubs the text before writing it and refuses
to write a report that still contains the user, machine or domain name, a local path, a SID or a
GUID; review the file anyway before committing it.

Compare reports side by side with `vschromium-bench compare <results.json...>` (from the results
files, kept locally).
