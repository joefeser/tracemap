# Design

## Overview

The legacy validation phase adds a safe, repeatable way to run TraceMap against
operator-provided old or large .NET repositories. The phase should answer:

- What facts can TraceMap extract when the project cannot build?
- Does the scan clearly explain missing SDK/runtime/MSBuild requirements?
- Do legacy UI event entry points appear in the current evidence model?
- Does a very large repository complete within practical bounds?
- Can we publish a redacted summary without leaking local machine details?

The implementation should avoid treating local sample repositories as fixtures.
They are validation inputs, not source material for committed tests.

## Local Input Model

Use an ignored manifest path:

`./.tmp/legacy-codebase-validation/repos.local.json`

Example shape:

```json
{
  "samples": [
    {
      "label": "legacy-winforms-app",
      "path": "<operator-local-repo>",
      "kind": "legacy-ui",
      "timeoutSeconds": 1200,
      "maxArtifactBytes": 524288000
    },
    {
      "label": "large-public-dotnet-client",
      "path": "<operator-local-repo>",
      "kind": "large-public"
    },
    {
      "label": "legacy-unknown-dotnet-app",
      "path": "<operator-local-repo>",
      "kind": "unknown-legacy"
    }
  ]
}
```

Only the labels may appear in committed summaries.

`timeoutSeconds` and `maxArtifactBytes` are optional per-sample overrides. When
omitted, the harness defaults to 20 minutes per sample and 500 MB per sample
output directory. Exceeding either bound should produce a truncated or deferred
result with a visible limitation.

## Proposed Command Shape

Prefer a script first:

```text
./scripts/validate-legacy-codebases.sh .tmp/legacy-codebase-validation/repos.local.json .tmp/legacy-codebase-validation/out
```

The script should:

- validate that the manifest path is under `.tmp/legacy-codebase-validation/`
- fail if any file under `.tmp/legacy-codebase-validation/` is git-tracked
- reject absolute paths in any committed output candidate
- run `tracemap scan` for each sample
- capture exit code, duration, artifact existence, fact counts, coverage labels,
  build/project-load status, and analyzer gap counts
- run focused SQLite queries for UI event/call/dependency evidence where useful
- write raw outputs under ignored `.tmp/legacy-codebase-validation/out/`
- write a redacted summary candidate under ignored `.tmp/legacy-codebase-validation/summary/`

If the validation shape proves useful, a later phase can promote it into a
first-class CLI command.

## Evidence Probes

### Legacy Environment Probe

Read project/config files through existing scan outputs where possible. The
validation summary may report:

- target framework monikers
- old-style project indicators
- packages.config presence
- binding redirect presence
- MSBuild ToolsVersion values
- missing SDK/runtime/build-tool hints emitted by TraceMap

These are environment clues, not guaranteed remediation instructions.

Representative load failures to record include missing SDK/runtime, missing or
unsupported MSBuild toolset, broken solution/project files, unsupported legacy
project types, package restore failures, and project references that cannot be
resolved. Each scenario should surface as reduced coverage with a visible
limitation and any available fallback evidence, not as a clean scan.

### UI Event Probe

The validation report should look for evidence in this order:

1. Existing semantic method/call facts for handler methods.
2. Existing syntax facts that mention event assignment or markup handlers.
3. Text/config facts that reveal legacy UI wiring.
4. Analysis gaps showing current scanner cannot extract this yet.

Potential patterns to inspect:

- WinForms `Click += Handler`
- WinForms designer-generated `InitializeComponent`
- WebForms markup event attributes such as `OnClick`
- code-behind handler methods

Concrete implementation probes:

- query `facts` for method declaration facts whose symbols or contract elements
  look like event handlers
- query call-edge facts from handler-like methods to downstream methods or
  dependency surfaces
- query dependency-surface facts reached from handler-like methods, if current
  evidence can establish that link
- search safe `facts.ndjson` properties for handler wiring tokens such as
  `+=`, `Click`, `OnClick`, `InitializeComponent`, and code-behind method names
  without rendering raw source snippets
- record a validation gap when the current fact vocabulary cannot expose event
  wiring

If no current facts expose these patterns, record a validation gap and propose a
future `legacy-ui-event-surfaces` spec.

All UI event findings are static wiring evidence only. They do not prove that a
control exists at runtime, that a handler executes, that a user can reach the
event, or that backend behavior occurred.

### Large Repository Probe

For the large sample, prioritize:

- scan completion or bounded failure
- fact count
- artifact existence
- coverage level
- elapsed time
- analyzer gaps
- whether output size becomes impractical

Do not commit generated scan artifacts.

## Output Shape

Raw local output:

```text
.tmp/legacy-codebase-validation/out/<label>/
  scan-manifest.json
  facts.ndjson
  index.sqlite
  report.md
  logs/analyzer.log
```

Redacted summary candidate:

```text
.tmp/legacy-codebase-validation/summary/legacy-validation-summary.md
.tmp/legacy-codebase-validation/summary/legacy-validation-summary.json
```

Committed docs should only include the spec and, later, a public-safe template
or sample generated from non-private checked-in fixtures.

## Safety

The validation process must scan for and reject:

- local absolute paths
- raw repository remotes
- raw private repository names
- source snippets
- raw SQL text
- config values
- connection strings
- secrets

The redaction step is the primary defense. The private path guard remains
mandatory before commit/PR, but it is a machine-specific backstop and must not
be treated as sufficient coverage for remotes, secrets, SQL, connection strings,
config values, private repository names, or source snippets.

The validation script and tests should also fail if any path under
`.tmp/legacy-codebase-validation/` becomes git-tracked.

Redaction failures should be surfaced as a non-zero validation result plus a
category-only explanation such as `absolute-path`, `raw-remote`, `private-name`,
`raw-sql`, `connection-string`, `config-value`, `secret`, or `snippet`. Failure
messages must not echo the rejected value.

## Pre-Publish Checklist

Before any redacted legacy validation summary is copied out of ignored `.tmp/`
and committed or published, verify:

- sample identity uses neutral labels only
- no local absolute paths
- no raw repository remotes
- no private repository names
- no raw SQL
- no config values
- no connection strings or secrets
- no source snippets
- counts, evidence tiers, coverage labels, rule IDs, and limitations are visible
  wherever the summary makes a claim

## Follow-Up Decisions

Possible follow-up specs after validation:

- `legacy-sdk-requirement-reporting`
- `legacy-ui-event-surfaces`
- `legacy-webforms-endpoint-alignment`
- `legacy-scan-performance-bounds`
