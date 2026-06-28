# Site TraceMap Tools Swift Surface Discovery Story Implementation State

Status: implemented
Readiness: implemented
Public claim level: demo

Last updated: 2026-06-28
Source of truth: implementation branch `codex/site-swift-public-stories`

## Implementation Summary

Implemented on `/swift/` as the `surface-discovery` matrix row, then expanded
with a dedicated story route at `/swift/surface-discovery/`. The pages describe
supported static HTTP/API client, SwiftUI/UIKit-ish, package, and dependency
surface evidence.

## Claim Boundary

The dedicated story route keeps the public claim level at `demo`: shipped
capability copy is anchored to PR #425, while demo proof still requires
checked-in public-safe generated output before stronger demo rows are presented.
It does not claim runtime
network reachability, rendered UI, complete navigation, user action, dependency
vulnerability/license/freshness, or build compatibility analysis.

## Validation

- `node --test scripts/swift-evidence-lane.test.mjs scripts/validate.test.mjs`
- `node --test scripts/swift-story-pages.test.mjs`
- `npm test`
- `npm run validate`
- `npm run build`
- `git diff --check`
