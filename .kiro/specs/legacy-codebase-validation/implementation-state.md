# Implementation State

Status: implemented
Branch: codex/legacy-codebase-validation-impl
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

## Validation

- Opus spec review found one blocking traceability issue: safety tests did not
  cover the full redaction promise.
- Patched Task 6 and related design/requirements language to cover raw remotes,
  private repo names, raw SQL, connection strings, config values, secrets,
  source snippets, tracked `.tmp` artifacts, concrete bounds, UI event query
  probes, runtime caveats, and pre-publish checks.
- Sonnet re-review reported no blockers before the Opus tightening edits.

## Follow-Ups

- Decide whether missing SDK/runtime guidance belongs in core scan reports.
- Decide whether legacy UI event extraction deserves a dedicated scanner spec.

## Implementation Notes

- Added `scripts/validate-legacy-codebases.sh` as the public entry point.
- Added `scripts/legacy_codebase_validation.py` for manifest validation,
  bounded scan execution, legacy environment probes, UI event probes, redacted
  summary generation, and category-only redaction failure reporting.
- Added `scripts/tests/test_legacy_codebase_validation.py` for local-only
  manifest boundaries, output boundaries, redaction categories, tracked `.tmp`
  rejection, and deterministic summary shape.
- Added validation rule IDs and limitations to `rules/rule-catalog.yml`:
  `legacy.validation.summary.v1`, `legacy.validation.environment.v1`,
  `legacy.validation.ui-events.v1`, and `legacy.validation.bounds.v1`.
- Real legacy sample paths remain local-only in
  `.tmp/legacy-codebase-validation/repos.local.json`.

## Implemented Validation

- `python3 -m unittest scripts.tests.test_legacy_codebase_validation`
- `python3 -m py_compile scripts/legacy_codebase_validation.py scripts/tests/test_legacy_codebase_validation.py`
- `./scripts/validate-legacy-codebases.sh .tmp/legacy-codebase-validation/repos.local.json .tmp/legacy-codebase-validation/out --dry-run`
