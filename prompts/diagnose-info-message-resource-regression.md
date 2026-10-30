# InfoMessage resource regression — existing evidence only, no rerun

This supersedes the suspended continue-sql-info-message-entry.md run protocol.
No build, test, scan, retry, profiler collection, dependency install, private code
edit or implementation work is authorized. No agents/subagents or background jobs.
Return one sanitized text report, at most 30 lines, and STOP.

1. If the previously launched runaway TraceMap job still runs, identify it from
   the recorded launch and current executable/command identity. Stop that job and
   its child processes only; do not reuse a PID from a photograph or kill all
   dotnet/compiler processes. Report stopped/already stopped/unable to identify.
2. Use existing terminal history, captured stdout/stderr, receipts and process
   observations only. Do not relaunch either old or new version for comparison.
3. Report the actual tool commit (40 characters if available), clean/dirty status,
   extractor version, executable base name and subcommand. Distinguish a build,
   scanner process, test host, compiler server and agent shell. Record whether the
   large memory observation belonged to the scanner or a child; unknown is valid.
4. Compare saved old/new argument OPTION NAMES and their categorical differences:
   repo/source scope, solution/project selection, include/exclude, restore,
   target framework, explicit VB context, compiled inputs, selected roots and
   boundary flags. Alias paths and identifiers; never echo tokens or source.
   Say whether source snapshot/compiled hashes and effective scopes actually match.
   A stated same-input claim is not a verified same-scope comparison.
5. From existing output/progress/receipt files, report the last confirmed stage,
   last completion and any categorical error. If no live progress was enabled,
   report phase unknown. An absent output directory does not identify the phase:
   ordinary scan creates final outputs after ScanEngine returns.
6. Distinguish old run completion/failure from entry candidates and early refusals.
   Preserve the exact categorical gap (if known) and duration basis: wall time,
   process CPU time or agent elapsed time. Do not relabel one as another.
7. If an EXISTING dump/profile is available, inspect it locally with already
   installed tools and return only categorical top allocating/retained type
   families and broad stack phase (Roslyn/source, IL metadata, callback CFG,
   materialization/reporting or unknown). Do not create or transfer a dump.
8. State the last observed scanner/child memory, process count and whether the
   observation is working set, private bytes, managed heap or unknown. No guess.

Do not infer that Roslyn caused this, that the new flag caused it directly, or
that instruction/body bounds cap end-to-end memory. Normal scan performs source
semantic extraction before IL lifecycle materialization. Removing --repo is not
an IL-only form of the normal scan command. An existing compiled-only attachment
route has a separate parent/admission contract; do not substitute it without a
reviewed protocol. No further private run is authorized by this prompt.

Future reproduction must first have independently tested wall-time and whole
process-tree memory containment with cleanup and live phase reporting. A sampled
parent-process watchdog or managed-heap limit alone is not a process-tree ceiling.
Keep source, private names, paths, command values, full logs and dumps local. No
email, uploads, source/IL samples or new photographs requested. STOP after report.
