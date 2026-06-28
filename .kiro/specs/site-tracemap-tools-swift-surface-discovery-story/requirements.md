# Site TraceMap Tools Swift Surface Discovery Story Requirements

Status: not-started
Readiness: backlog
Public claim level: shipped/demo

## Objective

Create a site story for Swift surface discovery across HTTP/API client surfaces,
SwiftUI/UIKit-ish surfaces, and package/dependency surfaces.

Proof anchor: PR #425, merge commit
`e8813daaf763e277e7c5d88a2c0b2ad0b570f25a`.

## Safe Public Claim

TraceMap can report conservative static Swift surface evidence for supported
HTTP/API, UI, package, and dependency patterns, with gaps for dynamic
composition.

## Requirements

- Public copy must describe this as shipped/demo depending on whether the final
  implementation links to checked-in public demo output.
- HTTP/API surfaces must not be described as runtime network reachability.
- UI surfaces must not be described as rendered UI, complete navigation, or user
  action proof.
- Dependency surfaces must not imply vulnerability, license, freshness, or build
  compatibility analysis.
- Dynamic composition gaps must be visible.

## Acceptance Criteria

- Future public copy names supported Swift surface families and their
  limitations.
- Claim level is upgraded to `shipped` only when the page is anchored solely to
  main-shipped capability copy; use `demo` when demo artifacts are presented.
- Site validation passes after implementation.
