# Source-Neutral Access Design-Evidence Ingestion Requirements

## Goal

TraceMap shall be able to ingest a bounded, versioned, owner-controlled
Microsoft Access design-evidence bundle without requiring the production Access
COM reader to enumerate item identities, read VBA source, inspect macro bodies,
open or render surfaces, read rows, or execute application behavior.

This specification defines the input and evidence boundary only. It does not
select or implement an export mechanism, reconnect the existing projectors to
COM, compose screen-to-data flows, or classify copy/clone behavior.

## Requirements

### R1 — Explicit, versioned input

1. Design evidence SHALL be supplied only through an explicit future input
   option; TraceMap SHALL NOT discover exports or Access databases
   automatically.
2. The input SHALL contain a bounded JSON manifest and ordered NDJSON records
   using schema `tracemap.access-design-evidence.v1`.
3. The manifest SHALL declare:
   - schema version;
   - producer identifier and version;
   - closed export-mechanism category;
   - repository identity hash and full commit SHA;
   - base Access scan manifest hash and database identity hash;
   - source database-copy SHA-256;
   - copy-binding classification;
   - input content SHA-256 and record counts by kind;
   - coordinate and completeness capabilities;
   - identity-disclosure policy;
   - optional UTC export timestamp.
4. The importer SHALL require repository and commit SHA equality with the
   current scan. It SHALL require the referenced base Access scan to match the
   declared manifest and database identity.
5. Input paths, source database paths, database names, customer names, and
   machine identities SHALL NOT become provenance fields.
6. An export timestamp SHALL be informational only. It SHALL NOT affect fact
   identity, ordering, matching, or any conclusion and SHALL NOT enter standard
   facts by default.

### R2 — Source and database-copy provenance

1. The copy-binding classification SHALL be one of:
   - `hash-identical`;
   - `owner-attested-derived-copy`;
   - `unbound`.
2. `hash-identical` SHALL require exact SHA-256 equality with the base Access
   scan input.
3. `owner-attested-derived-copy` MAY preserve owner-declared lineage only as
   provenance; it SHALL emit a Tier4 gap and SHALL NOT prove byte identity,
   unchanged design, or completeness.
4. `unbound`, a hash mismatch, a missing base scan, or an incompatible commit
   SHALL prevent design conclusions and produce a classification-only gap.
5. Every accepted record SHALL preserve:
   - input bundle hash;
   - producer and producer version;
   - export-mechanism category;
   - database-copy binding classification;
   - record kind and deterministic record identity;
   - source coordinate status;
   - completeness status.
6. Every projected fact or gap SHALL preserve rule ID, evidence tier, coverage
   label, repository, commit SHA, extractor version, and supporting input record
   IDs.

### R3 — Supported record vocabulary

The v1 contract SHALL support only these bounded record kinds:

1. `catalog-object`
   - table, saved-query, form, report, module, and macro catalog roles;
   - owner-local raw identity or existing TraceMap stable identity;
   - optional table-field catalog roles required for binding resolution.
2. `ui-surface`
   - form or report role;
   - module-presence and bound-state observations;
   - record-source, filter, order, and allowlisted event-property inputs;
   - nested or separately keyed control records.
3. `ui-control`
   - owner surface, ordinal, allowlisted control-type family;
   - control-source, row-source, validation, and allowlisted event-property
     inputs.
4. `vba-module`
   - module role, protected module identity, bounded source text, source hash,
     line count, and coordinate basis.
5. `event-reference`
   - surface/control owner, allowlisted event role, and protected reference
     value for transient classification.
6. `macro-inventory`
   - macro category, owner role, ordinal, startup role, body status, and
     completeness;
   - no action, argument, condition, expression, SQL, or command-body field.
7. `source-gap`
   - producer-issued classification, affected safe record identity or scope,
     and closed coverage category.

Unknown record kinds and unknown fields SHALL be rejected or omitted with a
version/rule-backed gap. They SHALL NOT be interpreted heuristically.

### R4 — Reuse of existing projectors

1. The importer SHALL normalize supported input records into the existing pure
   `AccessUiProjector`, `AccessVbaProjector`, and `AccessMacroProjector`
   contracts wherever their semantics remain correct.
2. Existing query, table, relationship, and external-boundary stable identities
   SHALL come from the matched base Access scan rather than a second extraction
   path.
3. Existing fact types and rules SHALL be reused for:
   - UI surfaces and controls;
   - static design bindings;
   - VBA declarations and bounded call/navigation candidates;
   - exact same-module event-binding candidates;
   - macro inventory and protected-body gaps.
4. A new ingestion-provenance rule MAY be added for bundle acceptance and
   contract gaps. It SHALL NOT replace the evidence rule supporting a projected
   conclusion.
5. Projector semantics SHALL NOT be widened through fallback parsing,
   name-based intent classification, prompt classification, or arbitrary target
   selection.

### R5 — Identity, coordinates, and evidence tiers

1. Source-neutral design ingestion SHALL use `hash-only` identity disclosure in
   all standard artifacts by default, even when a customer object name would
   pass the existing safe-identifier character allowlist.
2. A future explicit hidden-local identity sidecar MAY be considered only under
   a separate privacy design. It is not part of v1.
3. Raw identities MAY exist transiently in the importing process solely for
   deterministic matching and role-separated hashing. They SHALL NOT cross
   worker IPC or persist in standard artifacts, logs, checkpoints, or review
   bundles.
4. Every record SHALL declare one coordinate status:
   - `exact-lines`;
   - `container-only`;
   - `unavailable`.
5. Exact source lines SHALL be preserved when the producer supplies a bounded
   line-addressable source document and the importer validates its hash and
   coordinate range.
6. Container-only evidence SHALL use the repository-relative owning Access
   database span at `1:1` and explicitly state that the span anchors the binary
   container rather than source lines.
7. Unavailable, malformed, conflicting, or out-of-range coordinates SHALL emit
   a Tier4 gap. TraceMap SHALL NOT manufacture line numbers.
8. Evidence tiers SHALL be:
   - Tier2Structural only for a documented structured record whose identity,
     base-scan binding, and completeness are validated;
   - Tier3SyntaxOrTextual for bounded text parsing, VBA declaration/call
     candidates, expressions, and textual design evidence;
   - Tier4Unknown for missing, ambiguous, incomplete, dynamic, mismatched,
     unsupported, or limit-truncated evidence.
9. Tier propagation and rule-catalog `possibleEvidenceTiers` SHALL be updated
   during implementation where current entries do not describe source-neutral
   textual evidence.

### R6 — Privacy and persistence

1. The input bundle SHALL be treated as protected material and read locally
   from an owner-selected path.
2. The importer SHALL NOT copy the input bundle into the scan output or send it
   to the Access COM worker, another process, telemetry, a network service, an
   LLM, an embedding service, or a vector database.
3. The following MAY exist only transiently in the importer:
   - raw object, module, procedure, macro, form, report, and control names;
   - form/report design text;
   - VBA source;
   - event values;
   - record/control/row sources;
   - filters, ordering, validation, and expression text.
4. Standard artifacts MAY persist only allowlisted safe projections:
   - role-separated hashes and stable keys;
   - counts, lengths, ordinals, categorical kinds, and coverage labels;
   - validated safe target stable keys;
   - source hashes and validated relative spans;
   - rule IDs, evidence tiers, extractor versions, commit SHA, limitations, and
     supporting record IDs.
5. Standard artifacts, logs, reports, indexes, checkpoints, and review bundles
   SHALL NOT persist:
   - raw source or design text;
   - raw/copyable SQL;
   - literals, comments, captions, labels, messages, or customer identities;
   - macro actions, arguments, conditions, expressions, or bodies;
   - credentials, tokens, connection strings, DSNs, URLs, UNC/drive paths,
     private hosts, users, catalogs, or filenames;
   - local input/output paths or exception text.
6. Logs and failures SHALL be classification-only.
7. Input retention and deletion remain owner-controlled. TraceMap SHALL not
   delete caller-owned input.

### R7 — Determinism and bounds

1. Record identity SHALL be derived from schema version, database identity seed,
   record kind, role, parent stable identity, ordinal where applicable, and
   role-separated protected-value hashes.
2. Input order, JSON property order, timestamps, local paths, process IDs, and
   producer record sequence SHALL NOT affect facts or gaps.
3. Duplicate deterministic record identities SHALL be rejected unless records
   are byte-equivalent after canonicalization. Conflicting duplicates SHALL
   emit a Tier4 gap and no conclusion for the conflicted identity.
4. Initial implementation limits SHALL be explicit and no wider than the
   existing Access limits:
   - manifest: 1 MiB;
   - total bundle: 64 MiB;
   - records: 100,000;
   - children per object: 10,000;
   - design or VBA source per record: 4 MiB and 100,000 lines;
   - VBA procedures per module: 10,000;
   - calls per procedure: 10,000;
   - emitted facts: 100,000;
   - emitted gaps: 10,000.
5. Limit hits SHALL retain already-supported evidence, label coverage partial,
   and emit deterministic aggregate gaps.
6. Malformed UTF-8, invalid JSON, hash mismatch, unsupported schema, invalid
   coordinates, and invalid closed-enum values SHALL fail closed without
   exception text.

### R8 — Required gaps and non-claims

The contract SHALL preserve existing projector gaps and define closed ingestion
gaps for at least:

- unsupported schema or producer contract;
- manifest/content hash mismatch;
- repository, commit, base-scan, or database identity mismatch;
- owner-attested or unbound copy lineage;
- missing or partial catalog;
- unmatched or ambiguous identities;
- missing/unavailable coordinates;
- conflicting duplicates;
- rejected protected macro-body fields;
- malformed or truncated input;
- dynamic expressions, variables, concatenation, callbacks, `Eval`, `Run`,
  reflection-like dispatch, unresolved modules/procedures, and unsupported
  dynamic SQL;
- every collection, byte, string, line, fact, and gap limit.

The output SHALL NOT claim:

- runtime reachability, event firing, branch feasibility, or user navigation;
- query, VBA, macro, form, report, or external action execution;
- row access, values, database state, effective permissions, or connectivity;
- business intent, correctness, completeness, production use, or release
  approval;
- that a copy is unchanged when lineage is owner-attested;
- that absence from an export proves absence in the Access application;
- that an exporter is safe merely because its output conforms to this contract.

### R9 — Export mechanism neutrality

1. The specification SHALL compare candidate owner-controlled mechanisms but
   SHALL NOT authorize one without evidence that it fits the established
   non-execution boundary.
2. The contract SHALL be independent of Access COM, VBE, DAO system-table
   reads, `SaveAsText`, source-control exports, or a third-party product.
3. Synthetic hand-authored bundles SHALL be valid for Mac validation only and
   SHALL NOT prove a Windows export mechanism.
4. Any mechanism that opens/renders a surface, reads VBE source, reads
   undocumented/system rows, exports a macro body, changes trust policy,
   crosses protected material over IPC, or risks startup/event/query execution
   SHALL require a separate owner-authorized threat review and Windows-local
   probe.
5. MapMyApp SHALL NOT be a dependency.

### R10 — Validation

1. Mac-only tests SHALL use synthetic source-neutral bundles and existing pure
   projectors; they SHALL not require Windows, Access, COM, or a real database.
2. Required fixtures SHALL cover:
   - a complete small UI/VBA/macro inventory;
   - structured and textual evidence tier selection;
   - exact and unavailable coordinates;
   - hash-identical and owner-attested copy binding;
   - input-order and JSON-property-order determinism;
   - duplicate and conflicting records;
   - missing catalog targets;
   - dynamic event/call/query targets;
   - every relevant limit;
   - malformed, unsupported, and hash-mismatched bundles.
3. Planted leak fixtures SHALL include customer-like names, local paths,
   credentials, tokens, connection strings, private hosts, raw SQL, VBA
   literals/comments, event expressions, form/report text, and macro command
   bodies.
4. Leak tests SHALL scan `facts.ndjson`, `index.sqlite`, reports, logs,
   manifests, combined indexes, evidence docs, vault output, release review,
   and local review bundles where applicable.
5. Implementation validation SHALL include focused Access tests, full solution
   build/test, adapter artifact validation, private-path guard, and
   `git diff --check`.
6. Windows validation remains deferred until an export mechanism receives
   separate owner authorization.
