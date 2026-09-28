# Requirements

Status: planned
Readiness: public-and-work-first
Public claim level: hidden

## Overview

Turn the focused Web Forms validation workflow into a coherent, reproducible
operator experience without weakening TraceMap's evidence boundaries. Public
documentation and work-machine agent handoff are the first priority. Private
review/ticket workflow design follows after those surfaces are stable.

## Requirements

### Requirement 1: Public operator path

1. Provide one language-neutral quickstart for C# and VB.NET Web Forms.
2. Distinguish required retained stages from optional diagnostics.
3. Explain packet/corpus/workbench provenance and `P / F / S` call accounting.
4. Keep private and shareable artifact boundaries explicit.

### Requirement 2: PowerShell cleanup

1. Add one setup command that creates an ignored run folder and editable config
   containing the seven operator settings.
2. Add one orchestration command that reads the config, assigns one run ID and
   timestamp, executes retained stages, records exact output paths, and supports
   stage resume.
3. Validate wrong solution names and paths with typed, actionable errors and
   bounded candidate suggestions.
4. Discover projects only under the configured Web Forms, backend, and controls
   roots. Support multiple projects per root and a projectless Web Site mode.
5. Preserve current entry points as compatible wrappers until the replacement
   flow is validated.

### Requirement 3: Public use cases and prompts

1. Document evidence-led portfolio triage, technology discovery, conversion
   assessment, gap review, and human-review handoff.
2. Every prompt must preserve static-evidence limitations and separate facts,
   inferences, unknowns, and owner questions.
3. No prompt or core workflow may add LLM classification to the scanner.

### Requirement 4: Work-machine agent handoff

1. Provide a ready-to-paste prompt and commands for an authorized Claude Code
   evidence review.
2. Start from packet/corpus/workbench artifacts, not raw source.
3. Verify compatible provenance before analysis.
4. Keep source access and changes out of the first-pass review.

### Requirement 5: Private decision workflow

1. Keep human conclusions in a separately validated WITS overlay.
2. Design dry-run, alias-only ticket plans before adding external ticket writes.
3. Route approval and task custody through HACP/Routeboard, with ACK retaining
   implementation/review receipts.
4. Do not imply autonomous work authorization or release approval.

### Requirement 6: Artifact provenance

Every newly derived machine-readable artifact records the exact generator
SHA-256 and a bounded SHA-256 of its actual input. Shareable artifacts hash only
their privacy-projected input.

### Requirement 7: Compiled Web Site workflow and scale (planned)

1. Make optional published-site inputs part of the normal configured run. Keep
   source roots, published roots, scan artifacts, and report outputs distinct;
   never append generated files to the source checkout or commit private DLLs.
2. Perform source/DLL identity and binding preflight before expensive scanning.
   Missing compiler provenance, PDBs, maps, or attestation remains an explicit
   gap or review-only candidate, not an inferred source/binary identity.
3. Support adding compiled evidence to a retained source scan by creating a
   new immutable derived run with parent hashes, not modifying the old scan.
4. Move orchestration/report integration into .NET; preserve the proven
   PowerShell commands as compatibility wrappers until parity is validated.
5. Produce one navigable workbench and one documented handoff entry point,
   with compiled paths available through both. Separate review-only compiled
   paths from page-chain verdicts and private from shareable outputs.
6. Report file-admission, retained-input, IL extraction, graph traversal, and
   output limits separately, with observed usage, configured maxima, phase,
   and actionable resume guidance. Do not infer missing causes for old runs.
7. Validate representative large synthetic corpora, including an eight-times
   source-size scale case, before claiming larger-repository support. Measure
   elapsed time, disk bytes, peak memory, and retained/truncated evidence;
   source size alone is not a graph-work predictor. Never raise limits silently.
8. Keep durable run manifests and relative internal artifact links. Cleanup
   must be dry-run-first and preserve every dependency of retained handoffs;
   moving a run requires explicit locator handling and hash verification.
