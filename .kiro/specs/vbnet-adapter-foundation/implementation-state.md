# VB.NET Adapter Foundation Implementation State

- Status: ready-for-implementation
- Branch: `codex/issue-736-vbnet-adapter-foundation`
- Base: `origin/dev` at
  `c50f82ce0920d3c948e6ec798c6eb4b4d0959c24`
- Issue: [#736](https://github.com/joefeser/tracemap/issues/736)
- Parent: [#1](https://github.com/joefeser/tracemap/issues/1)
- Follow-ups: [#738](https://github.com/joefeser/tracemap/issues/738) for
  events/Web Forms and [#737](https://github.com/joefeser/tracemap/issues/737)
  for data/external boundaries.

## Current observations

- `.vbproj` is already inventoried, but `.vb` source is not.
- `TraceMap.Core` currently references only Roslyn C# and shared workspace
  packages.
- `ScanEngine` invokes a C#-specific semantic result path and several existing
  legacy extractors parse C# syntax directly.
- The shared language-adapter, fact, storage, report, source-snapshot, and
  artifact-validation contracts can be reused.

## Parallel-work boundary

The foundation owner controls inventory, scanner orchestration, shared result
merging, package references, rule IDs, and canonical VB symbol/fact shapes.
Independent work may prepare fixtures, validation research, and focused test
expectations without modifying those production files. #738 and #737 should
start implementation only after the foundation contracts are committed, then
work in separate branches/worktrees with distinct extractor files.

## Private validation boundary

A proprietary legacy application may be scanned locally after the public
fixtures pass. Only sanitized counts, rule IDs, tiers, gap categories, and
public-safe defect reproductions may return to this repository. Private source,
paths, SQL, configuration, BRD logic, and modernization prompts remain outside
the public project.

