# Implementation State

Status: ready-for-implementation
Branch: codex/legacy-codebase-validation-spec
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

- Implement the validation script and summary generator.
- Decide whether missing SDK/runtime guidance belongs in core scan reports.
- Decide whether legacy UI event extraction deserves a dedicated scanner spec.
