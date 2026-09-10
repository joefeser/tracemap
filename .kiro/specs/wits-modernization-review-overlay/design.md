# WITS modernization review overlay design

`wits-modernization-review.v1` is a private immutable overlay keyed to one exact
`webforms-batch-inspection.v1` file. Its `overlayId` is the first 20 hexadecimal
characters of the inspection SHA-256, prefixed with `review-`. This identifies
the evidence input, not a reviewer or mutable review revision.

The exporter copies only bounded identifiers and provenance already present in
the inspection. Decisions start as `unreviewed` with no comment, correction,
reviewer, or timestamp. The validator compares every reference against the exact
inspection and rejects rather than repairs mismatches.

A decision is human review metadata, not an emitted scanner finding. Its exact
case, handler-fact, binding-fact, and surface references are normalized foreign
keys into the inspection identified by the overlay-level scan, commit, rule, and
inspection digest. Consumers join those references to the immutable inspection
for tier and location detail; duplicating that fact metadata on the mutable
decision would create a second, drift-prone evidence record.

The overlay is private because surface IDs, handler fact IDs, binding fact IDs,
comments, and corrections can disclose application structure. A separately
governed downstream consumer may store it or derive another bounded projection,
but WITS persistence, BRD/planning synthesis, and code generation are not tasks
in this public overlay spec. The TraceMap scanner and reducer never ingest the
overlay as evidence.

Completed overlays require a reviewer identifier, RFC 3339 UTC timestamp, and no
`unreviewed` decisions. Draft overlays may be partially edited. Both modes retain
the same static-analysis limitations.

Initial implementation uses bounded PowerShell entry points beside the focused
Web Forms operator guide. This avoids adding a runtime/schema-validation package
to the core scanner while keeping the JSON Schema authoritative and the local
validation behavior independently tested.
