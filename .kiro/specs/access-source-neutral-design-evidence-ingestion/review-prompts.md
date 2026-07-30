# Source-Neutral Access Design-Evidence Ingestion Review Prompts

## Boundary and threat review

Review this specification adversarially against the shipped count-only Access
reader and completed Windows probes.

Confirm:

- no requirement reconnects UI/VBA/macro projectors to `AccessComReader`;
- no real Access database, Windows run, COM, DAO, VBE, surface load, row read,
  query, VBA, macro, form, or report execution is required;
- protected input remains local and in-process and never crosses worker IPC;
- input conformance is not represented as exporter safety;
- no candidate export mechanism is selected by implication;
- MapMyApp, LLMs, embeddings, vector databases, and prompt classification are
  absent;
- #551 and #552 remain dependent future specifications.

Report as P1 any path that could execute Access behavior, widen COM, move
protected material across IPC/network, persist customer material, or treat an
owner assertion as proof.

## Contract and determinism review

Attempt to make two semantically equivalent inputs produce different facts by
changing:

- NDJSON line order;
- JSON property order;
- producer-local IDs;
- timestamps;
- local paths;
- case, Unicode, whitespace, ordinals, or parent ordering;
- duplicate order;
- base scan or database-copy binding.

Attempt to make conflicting duplicates, unknown fields, malformed coordinates,
unsupported enum values, partial catalogs, and limit hits produce a positive
conclusion.

Confirm that stable identities use role-separated hashes and that timestamps,
paths, process IDs, and raw producer sequence never affect output.
Confirm that envelope-wide record/byte overflow emits no design conclusions in
either forward or reverse line order, while narrower collection/output caps are
applied only after canonical ordering.

## Provenance, tier, and rule review

For every supported conclusion, identify:

- exact input record IDs;
- bundle and document hashes;
- repository and commit SHA;
- base scan and database identity binding;
- producer and version;
- copy-binding classification;
- coordinate status and span;
- rule ID, evidence tier, coverage, extractor version, and limitations.

Challenge every Tier2 classification. Require structured, validated, complete
evidence and exact binding. Require textual parsing to remain Tier3 and every
missing/ambiguous/dynamic/mismatched/capped condition to remain Tier4.

Confirm an ingestion-provenance rule cannot replace the conclusion's supporting
Access rule.

## Privacy and leak review

Plant unique markers in:

- database, customer, form, report, control, module, procedure, macro, table,
  field, and query names;
- local/UNC/drive paths;
- connection strings, DSNs, URLs, hosts, users, catalogs, credentials, and
  tokens;
- form/report design text, captions, labels, status text, filter/order/
  validation expressions, and event values;
- VBA declarations, comments, literals, SQL, shell/command strings, and error
  messages;
- macro actions, arguments, conditions, expressions, SQL, and bodies;
- JSON errors and producer diagnostics.

Confirm none enters manifests, facts, SQLite, reports, logs, combined outputs,
evidence docs, vault, release review, or local review bundles. Confirm macro
body fields are structurally impossible or rejected rather than merely
ignored.

## Projector-reuse review

Compare the proposed normalized records with:

- `AccessUiTextParser`;
- `AccessUiProjector`;
- `AccessVbaProjector`;
- `AccessMacroProjector`;
- `AccessFactBuilder`;
- `AccessSafeValues`;
- `AccessModels`;
- existing Access projection and reporting tests.

Identify any semantic mismatch hidden by similar field names. Pay special
attention to:

- current optional display-name persistence;
- the protected `ui-design-document` input required by `AccessUiTextParser`;
- UI/macro database-container spans;
- VBA module-relative coordinates;
- event-reference ownership;
- catalog stable-key matching;
- macro rule naming and protected-body gaps;
- current count-only product-reader tests.

Do not recommend a second parser or duplicate fact vocabulary unless existing
semantics demonstrably cannot represent the evidence.

## Export-mechanism decision review

For each candidate producer, require evidence about:

- whether Access launches;
- whether a surface becomes loaded/open/rendered;
- whether startup/event/query/VBA/macro behavior can execute;
- whether trust policy changes;
- whether rows, system tables, VBE, or macro bodies are read;
- what protected material crosses process boundaries;
- how original/copy integrity, canaries, determinism, cleanup, and network
  isolation are proven;
- how completeness and source coordinates are established.

Schema conformance alone is insufficient. If repository evidence cannot answer
these questions, preserve the mechanism as deferred and require a separate
owner-authorized Windows gate.

## Owner decision prompt

Before implementation, ask the owner to decide:

1. Whether v1 should accept protected raw design/VBA input locally at all.
2. Whether standard artifacts must be permanently hash-only or a separately
   reviewed hidden-local identity sidecar is eventually required.
3. Whether `owner-attested-derived-copy` is useful with a mandatory Tier4 gap,
   or whether v1 should require exact byte identity.
4. Whether the manifest + NDJSON container is acceptable.
5. Which, if any, export mechanism deserves a separate Windows threat review.
6. Whether optional export timestamps should be retained only in the protected
   input or copied to a local provenance receipt.

Implementation remains blocked until these decisions and an adversarial
specification review are recorded.
