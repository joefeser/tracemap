# WITS modernization review overlay requirements

## Goal

Define a bounded, deterministic private overlay that records human review of a
TraceMap Web Forms batch inspection without modifying or reclassifying evidence.

## Requirements

1. The overlay SHALL identify its source inspection by schema version, SHA-256,
   scan ID, commit SHA, and diagnostic rule ID.
2. Every decision SHALL retain the exact case ID, handler fact ID, binding fact
   IDs, and surface IDs from the source inspection.
3. Human verdict, comment, correction, reviewer, review timestamp, and migration
   disposition SHALL be explicitly labeled review metadata.
4. Export SHALL be deterministic for identical inspection bytes and SHALL create
   a new file rather than overwrite an existing review.
5. Validation SHALL reject unknown fields, unknown closed codes, duplicate or
   missing cases, altered evidence references, provenance mismatch, invalid
   completion metadata, and oversized input.
6. Export and validation SHALL leave the source inspection byte-unchanged and
   SHALL NOT read application source, execute an application, or connect to a
   database.
7. The contract SHALL contain no source text, SQL, secrets, configuration values,
   or automatic business-intent conclusions.
8. A human verdict SHALL NOT become a TraceMap fact or change an evidence tier.

## Out of scope

- WITS persistence or identity-provider integration.
- Shareable projection of private review comments.
- Business-requirements generation or code generation.
- Runtime reachability, migration correctness, or approval claims.
