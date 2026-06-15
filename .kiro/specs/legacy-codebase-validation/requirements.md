# Requirements

## Introduction

TraceMap needs a realistic validation pass against old and unusually large .NET
codebases. Legacy repositories often cannot be restored or built on a modern
machine, but TraceMap should still extract deterministic evidence where possible
and label reduced coverage honestly.

This spec is validation-first. It must not add public claims about legacy
support until results are produced from safe, reproducible commands. Local sample
paths are operator-only inputs and must never be committed.

Public claim level: hidden until a redacted validation summary exists.

## Requirements

### Requirement 1: Local-Only Legacy Inputs

**User Story:** As a maintainer, I want to test real local legacy repositories
without leaking local machine paths or private repository names.

Acceptance Criteria:

1. WHEN the validation harness needs sample locations THEN it SHALL read them
   from an ignored local manifest under `.tmp/legacy-codebase-validation/`.
2. WHEN committed specs, docs, scripts, reports, or tests refer to samples THEN
   they SHALL use neutral labels only, such as `legacy-winforms-app`,
   `large-public-dotnet-client`, and `legacy-unknown-dotnet-app`.
3. WHEN output is committed or considered public-safe THEN it SHALL NOT contain
   local absolute paths, raw repository remotes, private repository names,
   secrets, config values, raw SQL, or source snippets.
4. WHEN local validation outputs are produced THEN raw scan artifacts SHALL stay
   under ignored `.tmp/legacy-codebase-validation/`.

### Requirement 2: Legacy Scan Resilience

**User Story:** As a user with old .NET code, I want TraceMap to tell me what it
could analyze even when the project cannot build.

Acceptance Criteria:

1. WHEN a legacy repository cannot load with the installed .NET/MSBuild tooling
   THEN the scanner SHALL continue with syntax/config fallback where possible.
2. WHEN build/project load fails THEN validation SHALL record the failure as
   reduced coverage, not as a clean scan.
3. WHEN project files declare old target frameworks, SDKs, ToolsVersion values,
   packages.config, binding redirects, or other legacy indicators THEN the
   validation report SHALL summarize those indicators without raw paths.
4. WHEN TraceMap can infer a missing SDK/runtime/tooling requirement THEN the
   report SHALL phrase it as evidence-backed environment guidance, not a hard
   guarantee.

### Requirement 3: Legacy UI Event Evidence Probe

**User Story:** As a maintainer, I want to know whether TraceMap can observe
legacy UI click/event entry points and connect them to downstream code evidence.

Acceptance Criteria:

1. WHEN validating WinForms-style code THEN validation SHALL check whether facts
   capture event handler assignments such as `Click += Handler`.
2. WHEN validating WebForms-style code THEN validation SHALL check whether facts
   capture markup/code-behind event handlers such as button click handlers.
3. WHEN event handlers are visible only through syntax/text THEN validation SHALL
   classify the result as syntax or structural evidence, not semantic proof.
4. WHEN event handlers can be linked to method/call/dependency facts THEN the
   validation summary SHALL show the static evidence chain with rule IDs and
   coverage labels.
5. WHEN event handlers are not captured by the current scanner THEN validation
   SHALL record a follow-up gap rather than claiming absence.
6. WHEN handler wiring evidence is found THEN validation SHALL state that static
   wiring does not prove the handler executes at runtime.

### Requirement 4: Large Repository Smoke

**User Story:** As a maintainer, I want to know whether TraceMap can handle a
large public .NET codebase without timing out or producing unsafe output.

Acceptance Criteria:

1. WHEN scanning the large sample THEN validation SHALL record duration, fact
   counts, output artifact existence, coverage level, and analyzer gaps.
2. WHEN outputs are summarized THEN the summary SHALL include counts and labels
   only, not raw file lists or snippets.
3. WHEN scan time or artifact size exceeds the configured default bounds of 20
   minutes per sample or 500 MB per sample output directory THEN validation
   SHALL mark the result as truncated or deferred, not failed silently.
4. WHEN operators need different bounds THEN they SHALL configure per-sample
   `timeoutSeconds` or `maxArtifactBytes` values in the ignored local manifest.

### Requirement 5: Public-Safe Summary

**User Story:** As a site/docs maintainer, I want a shareable legacy validation
summary that shows realistic behavior without exposing local details.

Acceptance Criteria:

1. WHEN validation completes THEN it SHALL produce a local raw output directory
   and a redacted summary candidate.
2. WHEN the redacted summary candidate is generated THEN it SHALL use sample
   labels, relative/safe artifact names, counts, coverage labels, rule IDs, and
   limitations.
3. WHEN a public summary is not safe to publish THEN validation SHALL explain
   why and keep it local-only.
4. WHEN results suggest a product change THEN validation SHALL record a proposed
   follow-up spec or issue, not silently change scanner behavior.
5. WHEN a redacted summary is promoted into committed docs or site copy THEN it
   SHALL pass a pre-publish checklist for labels-only sample identity, no
   absolute paths, no remotes, no raw SQL, no config values, no secrets, no
   snippets, and visible counts, tiers, coverage labels, limitations, and rule
   IDs where applicable.
