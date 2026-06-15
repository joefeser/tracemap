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
      "path": "/local/path/not/committed",
      "kind": "legacy-ui"
    },
    {
      "label": "large-public-dotnet-client",
      "path": "/local/path/not/committed",
      "kind": "large-public"
    },
    {
      "label": "legacy-unknown-dotnet-app",
      "path": "/local/path/not/committed",
      "kind": "unknown-legacy"
    }
  ]
}
```

Only the labels may appear in committed summaries.

## Proposed Command Shape

Prefer a script first:

```text
scripts/validate-legacy-codebases.sh .tmp/legacy-codebase-validation/repos.local.json .tmp/legacy-codebase-validation/out
```

The script should:

- validate that the manifest path is under `.tmp/legacy-codebase-validation/`
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

If no current facts expose these patterns, record a validation gap and propose a
future `legacy-ui-event-surfaces` spec.

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

The private path guard remains mandatory before commit/PR.

## Follow-Up Decisions

Possible follow-up specs after validation:

- `legacy-sdk-requirement-reporting`
- `legacy-ui-event-surfaces`
- `legacy-webforms-endpoint-alignment`
- `legacy-scan-performance-bounds`

