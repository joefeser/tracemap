# Guided Local Review Workflow Implementation State

Status: spec-ready-local
Issue: #666
Branch: `codex/spec-guided-local-workflow`
Base: `origin/dev`
Base SHA: `d8cac83e` (`Document Angular and .NET interaction mapping (#673)`)

## Current Scope

This is a specification-first slice. It defines the evidence gates for
distribution selection, installed version/readiness output, guided local
orchestration, output transactions, input-hash continuity, typed failure
readback, and cross-platform validation.

No product code, package metadata, release workflow, or public site content is
changed in this slice.

## Repository Findings

- `TraceMap.Cli` is a `net10.0` executable named `tracemap`; it is not currently
  configured as a .NET tool or versioned release archive.
- `tracemap scan` already emits the five required artifacts and a bounded,
  commit-bound `scan-receipt.json` when authoritative identity exists.
- `scan-execution-receipt.v1` is operational diagnostic evidence, not a fact or
  reducer input.
- `webforms-modernization` already composes one scan index into deterministic
  JSON and Markdown.
- `explorer generate` already consumes an explicit artifact directory and
  produces local-only static HTML with generated-output collision safety.
- The explorer does not yet support `webforms-modernization-packet.v1`; #667
  owns that compatibility reader.

## Decisions

- Do not choose .NET tool, archive, self-contained, or container distribution
  until synthetic package smoke evidence satisfies the matrix.
- Treat .NET tool as the leading hypothesis because it naturally supports a
  `tracemap` command and upgrade/uninstall, but not as an approved result.
- Keep orchestration thin and invoke shared producer services.
- Keep portable paths relative and terminal-only absolute paths ephemeral.
- Bind derivatives with input hashes and pre/post mutation checks.
- Keep distribution integrity separate from authenticity or publisher trust.
- Keep #667 as the only owner of Web Forms packet explorer rendering.

## Validation

Completed on 2026-08-13:

- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.
- Spec structure/readback review — passed; no blocking or P1/P2 contract
  finding remained.

Publication remains intentionally pending until GitHub confirms that #656 / PR
#674 is merged into `dev`, or the owner explicitly authorizes parallel review.

## Deferred Work

- Distribution probe implementation and selection.
- Installed version/readiness implementation.
- Guided command implementation.
- Cross-platform package smoke execution.
- #667 Web Forms static-explorer reader.
- Hosted execution, uploads, telemetry export, automatic restore, signing,
  self-update, and container publication.
