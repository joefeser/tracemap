# Site TraceMap Tools Swift Static Inventory Story Requirements

Status: implemented
Readiness: implemented
Public claim level: shipped

## Objective

Create a site story for Swift static inventory evidence: packages/projects,
source files, module-ish metadata, and reduced-coverage labels.

Proof anchor: PR #425, merge commit
`e8813daaf763e277e7c5d88a2c0b2ad0b570f25a`.

## Safe Public Claim

TraceMap inventories Swift projects and source surfaces without claiming Swift
builds, simulator/device execution, or runtime behavior.

## Requirements

- The story must describe static Swift inventory as shipped on `main`.
- It must name inventory evidence as static discovery, not build proof.
- It must explain reduced coverage when Swift project metadata, toolchain
  availability, generated code, conditional compilation, or unsupported surfaces
  limit precision.
- It must point readers toward the evidence model: rule IDs, tiers, commit SHA,
  coverage labels, and limitations.

## Acceptance Criteria

- Future public copy distinguishes inventory evidence from build success.
- The copy avoids saying TraceMap fully understands Swift modules or Xcode build
  behavior.
- Site validation passes after implementation.
