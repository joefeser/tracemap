# Guided Local Review Workflow Implementation State

Status: implementation-in-progress
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

The first implementation slice also completed a macOS arm64 candidate probe:

- focused `TraceMapVersionTests`: 7/7 passed;
- full .NET suite: 1,516/1,516 passed;
- changed-file format verification, private-path guard, and `git diff --check`:
  passed;
- conditional .NET-tool pack/install/version/scan/uninstall: passed outside the
source checkout;
- clean-source installed package `version --json` reported the exact
  `b9f58968...` build and `sourceState: clean`;
- installed package guided scan plus ordinary explorer completed outside the
  checkout, then uninstall passed;
- tool payload comparison: byte-identical across two builds;
- outer NuGet package comparison: not byte-identical because NuGet generated
  different relationship/core-property identifiers;
- Windows, Linux, upgrade, and remaining distribution candidates: pending.

The guided workflow implementation now provides:

- `tracemap local-review run` over the existing scan, Web Forms packet, and
  ordinary explorer producers;
- `local-review-result.v1`, relative artifact hashes, source/commit/scan/
  snapshot identity, closed stage outcomes, limitations, and bounded next
  actions;
- sibling staging, canonical output authorization, empty-output support,
  collision refusal, and input-mutation failure;
- preservation of verified scan artifacts and a typed result after downstream
  failure;
- standalone-versus-guided packet/explorer byte-parity coverage;
- stable workflow identity over repeated immutable-source runs;
- package refusal when the source tree is dirty and an explicit compiled
  `sourceState` in version output.

Focused guided workflow plus version tests: 16/16 passed. Full .NET suite:
1,525/1,525 passed. Clean-source package smoke passed on macOS arm64, Windows
11 arm64, and isolated Linux arm64. The Windows run exposed SQLite pooled
handle retention in the scan writer, Web Forms packet reader, and dependency
path reader; after the bounded non-pooled fixes, all 9/9 guided workflow tests
passed on Windows and the full distribution packet completed. Linux tool,
framework-dependent, self-contained, and a network-disabled/read-only
container version probe passed. The dirty-tree pack attempt failed as designed
with `TraceMapToolPackageRequiresCleanSource`.

Post-fix validation remained green: full .NET 1,525/1,525, focused
local-review/path/Web Forms suites 59/59 on macOS, guided workflow 9/9 on
Windows, targeted formatting, private-path guard, and diff check.

## Deferred Work

- Distribution probe implementation and selection.
- Installed version/readiness implementation.
- Guided command implementation.
- x64 CI receipts and final distribution selection/publication.
- #667 Web Forms static-explorer reader.
- Hosted execution, uploads, telemetry export, automatic restore, signing,
  self-update, and container publication.
