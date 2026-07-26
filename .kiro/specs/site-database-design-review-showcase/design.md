# Design

## Approach

Reuse TraceMap's existing static HTML, discovery registry, sitemap builder, and
feature-validator patterns. This slice adds no scanner or reporter behavior.

The manager route explains the review question and the evidence families. The
proof-packet route renders one synthetic public-safe projection and links to its
checked-in JSON representation.

The JSON asset is a site-owned public projection:

- `schemaVersion` and `derivedFromContract` identify the projection and shipped
  `database-design-review/1.0` contract.
- `publicClaimLevel` remains `demo`.
- `modes` contains single-index and combined-index examples.
- Each nested `packet` uses the shipped packet field names and bounded evidence
  reference shapes.
- Only allowlisted public fields and metadata keys are accepted by validation.

## Claim boundary

The pages describe deterministic static repository evidence. They do not imply
live catalog inspection, execution, reachability, correctness, compatibility,
rollback, approval, or operational safety. Gaps remain evidence rows rather
than inferred absence.

## Validation

A focused validator checks required copy, canonical/social metadata, the asset
schema and allowlists, single/combined behavior, provenance, limitations,
discovery registration, sitemap membership, inbound links, and forbidden
private or executable material. It participates in the full site validator and
has focused regression tests.
