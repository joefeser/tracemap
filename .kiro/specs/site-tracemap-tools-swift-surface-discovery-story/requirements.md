# Site TraceMap Tools Swift Surface Discovery Story Requirements

Status: implemented
Readiness: implemented
Public claim level: demo

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

- Public copy must describe this route as demo, with shipped capability context
  anchored to PR #425.
- HTTP/API surfaces must not be described as runtime network reachability.
- UI surfaces must not be described as rendered UI, complete navigation, or user
  action proof.
- Dependency surfaces must not imply vulnerability, license, freshness, or build
  compatibility analysis.
- Dynamic composition gaps must be visible.

## Acceptance Criteria

- Future public copy names supported Swift surface families and their
  limitations.
- Claim level stays `demo` for the story route because public-safe demo framing
  and generated summaries remain part of the public proof boundary.
- Site validation passes after implementation.
