# Site TraceMap Tools Swift Symbol And Call Evidence Story Implementation State

Status: implemented
Readiness: implemented
Public claim level: shipped

Last updated: 2026-06-28
Source of truth: implementation branch `codex/site-swift-public-stories`

## Implementation Summary

Implemented on `/swift/` as the `symbol-call-evidence` matrix row and evidence
reading copy. The page describes SwiftSyntax-backed declarations, call
candidates, construction candidates, and relationship evidence.

## Claim Boundary

The copy keeps syntax-backed candidates separate from compiler semantic
resolution, runtime dispatch, protocol witness proof, dependency injection
proof, and SwiftUI runtime reachability.

## Validation

- `node --test scripts/swift-evidence-lane.test.mjs scripts/validate.test.mjs`
- `npm test`
- `npm run validate`
- `npm run build`
- `git diff --check`
