# Implementation State

Status: implemented
Branch: codex/legacy-codebase-validation
Public claim level: hidden

## Summary

This spec defines a validation-first pass for very old or large .NET codebases.
It intentionally does not commit local sample paths, sample repository names, raw
scan artifacts, source snippets, raw SQL, config values, or private remotes.

The validation samples are represented only by neutral labels:

- `legacy-winforms-app`
- `large-public-dotnet-client`
- `legacy-unknown-dotnet-app`

Actual local path mappings must live under ignored
`.tmp/legacy-codebase-validation/`.

## Scope Decisions

- Validate current TraceMap behavior before adding scanner features.
- Treat build/project-load failure as expected legacy evidence, not a clean
  success.
- Probe legacy UI click/event evidence, but do not claim support unless facts
  and rule IDs exist.
- Keep raw validation outputs local-only.
- Generate only redacted summary candidates.
- Use 20 minutes per sample and 500 MB per sample output directory as default
  validation bounds unless overridden in the ignored local manifest.
- Treat the redaction step as the primary safety defense; private-path guard is
  a machine-specific backstop.
- Implemented the operator workflow as `scripts/validate-legacy-codebases.sh`
  backed by deterministic Node helpers instead of changing scanner behavior.
- The harness reads only `.tmp/legacy-codebase-validation/repos.local.json`,
  writes raw scan outputs under ignored `.tmp/legacy-codebase-validation/out/`,
  and writes redacted candidate summaries under ignored
  `.tmp/legacy-codebase-validation/summary/`.
- UI event validation currently probes existing fact fields for handler-like
  methods, call edges, event wiring tokens, and handler-linked dependency
  surfaces; when facts do not expose wiring, it records a
  `legacy-ui-event-surfaces` follow-up gap rather than claiming absence.
- Large sample handling is bounded by per-sample timeout and artifact-size
  limits and reports timeout or size excess as truncated output.
- Review-loop patch tightened process execution so child output streams to disk,
  spawn/log-write failures settle deterministically, failed or truncated samples
  produce a non-zero harness exit, and reduced-coverage scans are labeled
  `partial` rather than clean.
- Redacted summaries include commit SHA and a safe repository identity hash, and
  UI evidence examples retain line spans, snippet hashes, and safe relative path
  metadata or path hashes.
- Raw scan artifacts, including `scan-manifest.json` and `report.md`, are
  listed as local-only and not public-safe; only redacted summary candidates are
  intended for pre-publish review.
- Added `legacy.validation.summary.v1` to the rule catalog with limitations.

## Validation

- Opus spec review found one blocking traceability issue: safety tests did not
  cover the full redaction promise.
- Patched Task 6 and related design/requirements language to cover raw remotes,
  private repo names, raw SQL, connection strings, config values, secrets,
  source snippets, tracked `.tmp` artifacts, concrete bounds, UI event query
  probes, runtime caveats, and pre-publish checks.
- Sonnet re-review reported no blockers before the Opus tightening edits.
- `node --test scripts/legacy-codebase-validation.test.mjs` passed: 7 tests.
- Local harness smoke passed using ignored
  `.tmp/legacy-codebase-validation/repos.local.json` against a checked-in sample:
  `./scripts/validate-legacy-codebases.sh .tmp/legacy-codebase-validation/repos.local.json .tmp/legacy-codebase-validation/out`.
- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 244 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.

## Follow-Ups

- Decide whether missing SDK/runtime guidance belongs in core scan reports.
- Decide whether legacy UI event extraction deserves a dedicated scanner spec.
- Run the harness against real operator-local legacy and large public samples
  before promoting any public legacy-support claim.
