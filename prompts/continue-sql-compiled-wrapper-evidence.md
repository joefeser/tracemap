# Compiled SQL lifecycle: consolidated work-machine fit check

## Fill in these inputs locally

```text
EXPECTED_TOOL_COMMIT = <published branch head with the recorded validated implementation ancestor>
PRIVATE_SOURCE_ROOT = <existing source checkout>
PRIVATE_ACCEPTANCE_ROOT = <existing retained notes, indexes and receipts>
COMPILED_INPUTS = <exact caller and wrapper assemblies, plus available source/deployment correspondence evidence>
ROOT_CLASS_AND_PROPERTY = <class.property under investigation>
PROCEDURE_IDENTITIES_AND_DDL = <owner-provided server/database/schema/procedure identities and scripts>
```

Read AGENTS, `docs/validation/SQL_COMPILED_WRAPPER_EVIDENCE.md`, the lifecycle rule
limitations, and `.kiro/specs/sql-compiled-wrapper-evidence/end-to-end-acceptance.md`.
Verify the requested revision and its recorded validation before doing anything else.
If final local gates are absent or pending, report that prerequisite and stop.

## Authority and privacy

This is a read-only, offline capability-fit check. Do not execute the application,
SQL, provider methods, containers, requests or impersonation flows. Do not modify
application code, accepted baselines or existing outputs. Keep all private source,
paths, SQL, identities and hashes on this machine. No PR or merge is authorized.

## Inspect retained evidence first; avoid another unnecessary scan

1. Verify the exact tool revision and CLI/Core/Reporting byte provenance. Preserve
   the accepted baseline hashes before and after the check.
2. Resolve the exact root class/property, caller/wrapper bodies and their binary
   identity. Separate source correspondence from deployed-binary correspondence;
   neither is implied by a successful tool build.
3. Map the actual flow against the separate `il-sql-lifecycle/1` route: two
   independent executions, property-held DataSet, named Merge destinations,
   application-built auxiliary table, guarded helper writes, raw and projection
   reads, reload/re-Merge ordering and potential partial failure boundaries.
4. Record every indispensable unsupported operation with private file/line or
   compiled-body evidence. Known boundaries include custom ArrayList parameter
   protocols, unsupported Select predicates, unavailable wrapper bodies, complex
   production DDL, catch/fallback execution and unresolved deployment identity.
   Do not substitute a legacy object-path or source-reader route, invent a body,
   simplify the actual application flow or call an all-unknown result acceptance.
5. The current comparison route admits independently authored **synthetic JSON**
   result declarations only. Do not relabel owner DDL, production schema or a
   manual transcription as synthetic evidence. If the real schema has no truthful
   admitted route, record that capability gap and stop before a fresh scan.

If a known indispensable gap remains, return the consolidated fit matrix now.
Do not repeat an old diagnostic run merely to reproduce the same unsupported result.

## Fresh offline validation only when the actual inputs and shape fit

Scan into a new private directory, retaining repo/revision, exact body/binary links,
rule IDs, tiers, extractor versions and generator/bounded-input hashes. Bind each
execution to an independently supported identity and each consumer to its exact
table, column and body/binary/offset/invocation. Use
`sqlserver-compiled-lifecycle compare` with the documented declarations, schema,
bindings and persisted facts; never repurpose source-reader bindings.

Check SQL schema identity separately from current value origin. Guarded application
overwrites must not be attributed to SQL values. Retained schema after Clear/re-Merge
must not prove that the new execution supplied an old column. Retain both guard
alternatives, raw/projection read sites and potential partial-failure snapshots.
Those snapshots do not prove catch continuation, rollback or final runtime state.

## Return once, sanitized

Provide one matrix containing input presence, exact tool/binary verification,
root/guard, execution/table/column linkage, schema versus value origin,
raw/projection consumers, refresh ordering, partial-failure evidence, baseline
preservation, each refusal and the smallest remaining capability or input.

Separate manual source observations from native TraceMap findings. Mark partial
coverage and unknowns explicitly. Public synthetic success is not private SQL
acceptance, application-build, session/framework execution, runtime correctness,
deployment or migration readiness. Do not include private source, paths, SQL text
or hashes in the sanitized return or public commits.
