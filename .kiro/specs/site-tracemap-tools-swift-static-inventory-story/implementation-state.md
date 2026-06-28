# Site TraceMap Tools Swift Static Inventory Story Implementation State

Status: implemented
Readiness: implemented
Public claim level: shipped

Last updated: 2026-06-28
Source of truth: implementation branch `codex/site-swift-public-stories`

## Implementation Summary

Implemented on `/swift/` as the `static-inventory` matrix row and overview copy,
then expanded with a dedicated story route at `/swift/static-inventory/`.
The pages describe Swift package/project inventory, source file selection,
module-ish metadata, and reduced-coverage labels.

## Claim Boundary

The public copy says static inventory is shipped, but it does not imply Xcode
build proof, SwiftPM restore proof, simulator/device execution, or complete
module understanding.

## Validation

- `node --test scripts/swift-evidence-lane.test.mjs scripts/validate.test.mjs`
- `node --test scripts/swift-story-pages.test.mjs`
- `npm test`
- `npm run validate`
- `npm run build`
- `git diff --check`
