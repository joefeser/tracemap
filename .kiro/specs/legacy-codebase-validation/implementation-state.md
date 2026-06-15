# Implementation State

Status: ready-for-review
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

## Validation

Pending. This branch only adds the reviewed spec and ignore boundary.

## Follow-Ups

- Implement the validation script and summary generator.
- Decide whether missing SDK/runtime guidance belongs in core scan reports.
- Decide whether legacy UI event extraction deserves a dedicated scanner spec.

