# Legacy Story Reconciliation Design

## Overview

This is a cleanup and proof phase after a `main` into `dev` merge. It has two
outputs:

- a focused coexistence regression test for legacy evidence families;
- corrected spec state so future agents can tell implemented MVP work from
  deferred follow-ups.

No production behavior changes are planned beyond making existing extractors
work together in the same tested scenario.

## Coexistence Test

Add focused synthetic scan tests as a new .NET test class with the fixture built
inline using `TempDirectory`, matching the existing legacy extractor test style.
The fixture should include:

- WCF endpoint/config, service contract, generated client, and service host;
- .NET Remoting API/config evidence;
- DBML metadata plus generated designer linkage;
- raw endpoint/config values that must remain hashed or omitted.

The tests assert facts from these existing families:

- WCF: `legacy.wcf.mapping.v1` from `WcfServiceReferenceMapping`, expected
  `Tier2Structural` in the synthetic fixture.
- Remoting: `legacy.remoting.registration.v1` from
  `RemotingServiceTypeRegistered` and `RemotingClientActivationDeclared`,
  expected `Tier3SyntaxOrTextual` or stronger.
- Legacy data: `legacy.data.dbml.v1` from `LegacyDataEntityDeclared` and
  `legacy.data.generated-link.v1` from `LegacyDataGeneratedCodeLinked`,
  expected `Tier2Structural`.

The test should not assert runtime behavior, service reachability, database
existence, or user action. It only proves deterministic static evidence can be
collected together.

Safe-value validation follows existing test patterns in
`LegacyWcfExtractorTests`, `LegacyRemotingExtractorTests`, and
`LegacyDataMetadataExtractorTests`: serialize emitted facts with
`JsonSerializer.Serialize(result.Facts)`, build Markdown with
`MarkdownReportWriter.Build(result)`, and assert raw URLs, object URIs,
connection strings, host names, passwords, and secret-looking values are absent.
The expected safe forms are existing hashes and safe local identifiers; this
phase must not invent a new redaction mechanism.
If referenced safe-value assertion patterns differ, prefer the most restrictive
pattern: broad raw-value exclusion across serialized facts and Markdown report
output.

Extractor identity means each asserted family fact has non-empty
`Evidence.ExtractorId`, `Evidence.ExtractorVersion`, `CommitSha`, rule ID,
evidence tier, and line span metadata.

## State Cleanup

Update only stale legacy state files:

| Spec directory | Cleanup |
| --- | --- |
| `.kiro/specs/legacy-wcf-service-reference-mapping/` | Mark implemented state and completed tasks. |
| `.kiro/specs/legacy-data-metadata-extraction/` | Mark MVP implemented and label unchecked breadth items as deferred follow-ups. |
| `.kiro/specs/legacy-flow-composition-reporting/` | Replace queued legacy-data wording with optional MVP-input wording. |

Do not erase useful historical review notes. Prefer concise current-state notes
over large rewrites.
Example cleanup: replace machine- or repo-specific process details with generic
phrasing such as "a separate worktree" or remove the detail if it does not help
future implementation.

## Validation

Required:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Run site validation only if site files change.

## Non-Goals

- New legacy extractor behavior.
- New public site claims.
- Runtime WCF, Remoting, database, or WebForms execution proof.
- New rule catalog entries.
- Refactoring unrelated specs.
