# Source-Neutral Access Design-Evidence Ingestion Implementation State

Status: specification approved; bounded reader foundation implemented

Issue: #550

Parent epic: #549

Dependent issues: #551, then #552

Branch: `codex/implement-access-design-evidence-ingestion`

Base: `origin/dev`

Base SHA: `50fb96258d63b43cbc5d88984215ac197174f262`

Base merge-base with `origin/main` at implementation start:
`736c9f1e3ad692ecce8bea2a668fd52f0d8e2f20`

## Scope decision

Specify a protected, source-neutral local artifact boundary that can feed
TraceMap's existing pure Access UI, VBA, event, navigation, and macro
projectors. Do not implement the importer or an exporter. Do not change the
production count-only COM reader. Do not work on screen-to-data flow or
copy/clone classification.

## Non-duplication decision

- `microsoft-access-adapter-v0-runway` built and validated the pure projectors
  while deliberately leaving them disconnected from the product reader.
- `access-design-review-composition` consumes shipped schema/query/external and
  count-only coverage evidence; it explicitly does not acquire item-level
  UI/VBA/macro evidence.
- `access-local-review-bundle` packages already-shipped evidence and adds no
  extraction path.
- `access-parallels-source-runner` orchestrates source builds and unchanged
  synthetic validation in an isolated VM; it adds no extraction.
- Completed issues #488 through #491 remain the authoritative Windows
  count-only and representative validation record. This spec does not reopen
  them.

Issues #549 through #552 therefore define new ordered work rather than
duplicating an active spec. #551 depends on the approved contract from #550;
#552 depends on #550 and #551.

## Repository inventory

Existing reusable product code:

- `AccessUiTextParser`;
- `AccessUiProjector`;
- `AccessVbaProjector`;
- `AccessMacroProjector`;
- `AccessFactBuilder`;
- `AccessSafeValues`;
- Access projection records in `AccessModels`;
- downstream standard/combined indexes, evidence docs, vault, release review,
  and hidden-local review bundle.

Existing rules:

- `legacy.access.ui-surface.v1`;
- `legacy.access.binding.v1`;
- `legacy.access.vba.v1`;
- `legacy.access.event-binding.v1`;
- `legacy.access.macro-gap.v1`;
- `legacy.access.coverage-gap.v1`.

Existing limits cover object/child collections, design/VBA source bytes and
lines, procedures, calls, facts, gaps, worker projection bytes, and artifacts.

Existing Mac tests cover:

- UI design parsing, limits, malformed input, bindings, events, determinism,
  standard/combined persistence, and protected-value suppression;
- count-only UI inventory behavior;
- VBA count-only behavior, projector declarations/calls/navigation/events,
  ambiguity/dynamic/limit gaps, real line spans, deterministic projection, and
  no source persistence;
- macro count-only behavior, deterministic inventory projection, owner/body
  gaps, downstream reporting, and protected-name suppression;
- Access rule catalog, safe identities, query parsing, standard artifact leak
  boundaries, limits, working copies, worker frames, and process ownership;
- release-review and local-review composition.

## Required future contract adjustments

The pure projectors are reusable, but implementation needs two narrow
cross-cutting changes:

1. An explicit hash-only identity-disclosure policy. The existing identifier
   character allowlist is not a customer-confidentiality policy.
2. Optional validated source-coordinate provenance for UI/macro records. The
   current fact builder uses the owning binary database span at `1:1` outside
   VBA evidence.

These changes belong to the source-neutral importer/projection path and do not
require or authorize COM changes.

## Exact-head review corrections

The first exact-head Codex/Qodo review identified four specification
contradictions:

- envelope record caps could retain an input-order-dependent prefix;
- no record carried the protected UI design document consumed by
  `AccessUiTextParser`;
- provenance wording allowed safe-token producer-local record IDs to persist;
- requirements conflated the manifest and record-envelope schemas and called
  otherwise order-independent NDJSON records ordered.

The specification now rejects envelope-wide byte/record overflow before any
design conclusion, applies narrower caps after canonical ordering, defines an
explicit bounded `ui-design-document`, persists only TraceMap-derived canonical
record IDs, and distinguishes both schemas while making NDJSON line order
semantically irrelevant.

## Export-mechanism decision

No real exporter is selected.

- Synthetic hand-authored bundles are sufficient for Mac contract validation
  but prove nothing about Access extraction.
- Preexisting owner-produced exports may be compatible inputs if their
  provenance and completeness can be bound independently.
- `SaveAsText`-style automation, COM exporters, VBE export, DAO/system-table
  reads, and Access Documenter remain unproven or outside the established
  boundary.
- MapMyApp is excluded.

Any Windows mechanism requires a separate threat design and explicit owner
authorization. Contract conformance does not authorize generation.

## Privacy decision

The proposed input is protected caller-owned material. Raw names, design text,
VBA source, event values, and expressions may exist only transiently in the
single importing process. They do not cross Access worker IPC and do not
persist.

Standard artifacts are hash-only for customer identities. A hidden-local
identity sidecar is deferred to a separate privacy review.

## Non-claims

The spec does not establish runtime reachability, event firing, user
navigation, branch feasibility, execution, row/data state, connectivity,
permissions, business intent, completeness, correctness, production use,
release approval, or exporter safety.

## Validation

Completed on macOS:

- all five required specification files exist and are nonempty;
- required scope, provenance, privacy, determinism, gaps, mechanism comparison,
  validation, review, and implementation-state sections were inspected;
- `dotnet restore src/dotnet/TraceMap.sln --locked-mode`: passed;
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj
  --no-restore --filter 'FullyQualifiedName~Access'`: 72/72 passed;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with 0 errors;
- specification whitespace scan: passed;
- `./scripts/check-private-paths.sh`: passed;
- `git diff --check`: passed.

Windows, Access, COM, a real database, and representative validation are not
required or authorized.

The repository's existing `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 advisory remains
separate dependency work and shall not be duplicated here.

## Owner decisions required before implementation

1. Accept or reject local ingestion of protected raw design/VBA input.
2. Confirm permanent hash-only standard artifacts, or separately authorize
   design of a hidden-local identity sidecar.
3. Accept `owner-attested-derived-copy` as useful partial provenance with a
   mandatory Tier4 gap, or require exact byte identity.
4. Accept or revise the JSON manifest + NDJSON record container.
5. Select no exporter, or nominate one candidate for a separate Windows threat
   review.
6. Keep timestamps only in protected input, or allow them in a separate local
   provenance receipt.

## Owner decisions recorded for implementation

Joe explicitly authorized continuation of the Access runway after merging the
specification PR. The first implementation slice uses the most conservative
choices already defined by the merged contract:

1. Protected design/VBA input is accepted only as explicit local caller-owned
   input. This is necessary for source-neutral ingestion and does not authorize
   an exporter.
2. Standard artifacts remain permanently hash-only. No hidden-local identity
   sidecar is included.
3. `owner-attested-derived-copy` is accepted only as partial provenance with a
   mandatory `AccessDesignInputCopyOwnerAttested` Tier4 gap. It does not prove
   byte identity or unchanged design.
4. The merged JSON manifest plus NDJSON record container is accepted for v1.
5. No exporter is selected. Synthetic hand-authored bundles are the only
   producer used by this Mac validation.
6. Export timestamps remain protected input metadata and are neither returned
   by the reader nor allowed to affect canonical identity or ordering.

These decisions were approved as conservative consequences of the merged
specification and the explicit instruction to continue the runway. They do not
widen Access COM, select a Windows mechanism, or authorize #551/#552.

## Implemented foundation slice

The implementation adds a production-usable, Mac-capable pure
`AccessDesignEvidenceReader` API and disposable protected in-memory models. The
reader:

- accepts exactly `access-design-manifest.json` and
  `access-design-records.ndjson` from an explicit local directory;
- snapshots both files under the 1 MiB/64 MiB ceilings before hashing or
  parsing, preventing an observed-byte/hash mismatch;
- rejects reparse-point input members, malformed UTF-8/JSON, duplicate JSON
  properties, unsupported schemas/enums, invalid counts, and envelope-wide
  hash/limit failures using closed classifications only;
- rejects scoped records with deterministic canonical-ID gaps for unknown
  envelope/payload fields, protected-text/hash/coordinate failures, and
  per-record limits, then rejects descendants whose parent evidence was
  unavailable;
- validates repository, commit, base-scan, database identity, and source-copy
  binding before exposing records for projection;
- derives TraceMap canonical record IDs from role-separated protected-value
  hashes and resolved canonical parents, never producer-local IDs;
- makes NDJSON line order, JSON property order, producer IDs, and timestamps
  irrelevant;
- collapses equivalent canonical duplicates and poisons conflicting canonical
  identities without selecting first or last;
- rejects an envelope record overflow before returning any accepted record;
- structurally rejects macro body/action fields;
- keeps raw payload values only in disposable in-process `JsonDocument`
  storage.

Exact-line claims are accepted only when they resolve to an accepted,
hash-validated UI design document or VBA module and fit both that document and
any exact-line parent bounds. Manifest projection also retains the declared
catalog-completeness capability so later projection can distinguish complete,
partial, and unavailable catalogs.

Structural parent and identity prerequisites are also enforced before records
can qualify as evidence: every UI control resolves to a UI surface, and every
catalog object supplies either a protected local identity or a validated
existing stable key. Initial file-size probe failures are translated into the
same classification-only read boundary as later filesystem races.

The manifest projection retains the declared coordinate capability. Event
references resolve only to UI surfaces or controls, required protected identity
fields reject null or blank values, and control types use the exact closed
vocabulary supported by `AccessUiTextParser`. Directory member enumeration and
attribute races are classification-only as well.

The public reader API can be consumed by the next composition slice without
duplicating validation. Callers must dispose the returned bundle; its raw
payload is protected material and exists only while the bundle is alive.

This slice deliberately does not add a CLI option or project records into
facts. A partially wired input must not be exposed as product evidence.

## Integration seam requiring follow-up design

The v1 manifest binds to a base Access scan manifest SHA-256, while the current
`tracemap-access scan` command creates its scan manifest only after COM
projection and fact building. There is no stable pre-projection base-manifest
hash available to validate an optional design bundle during that same scan.

The proposed follow-up is a separate Mac-only composition story:

```text
existing completed Access base scan + explicit design bundle
  -> validate immutable base-scan/database binding
  -> project source-neutral design evidence
  -> write a new enriched output directory
```

That story must define immutable input/output ownership, safe base-scan
reconstruction, manifest identity, and downstream provenance before adding a
CLI command. It must not rerun COM, mutate the base scan, invent a circular
hash, weaken binding, or make design input silently optional. No alternative
same-scan wiring is selected in this slice.

## Foundation validation

Mac-only synthetic tests cover:

- canonical equality across record order, JSON property order,
  producer-local IDs, and timestamps;
- exact binding and classification-only mismatch behavior;
- owner-attested copy acceptance with an explicit lineage gap;
- equivalent/conflicting identity handling;
- whole-envelope record caps in forward and reverse order;
- protected source hash and coordinate validation;
- unknown fields, duplicate JSON properties, and macro-body rejection;
- scoped source-object rejection without aborting otherwise-valid records;
- validated document and parent bounds for exact-line child records;
- accepted duplicate parents remaining available when another equivalent
  producer record is rejected;
- retained manifest catalog-completeness capability;
- required UI-surface ownership for UI controls;
- required protected identity or validated stable key for catalog objects;
- classification-only initial file-size probe failures;
- retained manifest coordinate capability;
- required UI owner for event references;
- nonblank required protected identities;
- exact supported Access UI control-type vocabulary;
- classification-only directory enumeration and member-attribute failures;
- suppression of protected markers from canonical record IDs and failure
  classifications.

No Windows VM, Microsoft Access, COM, customer database, exporter, row read, or
execution is used.

Validation on the implementation head:

- locked solution restore: passed, with the separately tracked
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 advisory only;
- focused strict reader tests: 42/42 passed after review fixes;
- all Access-focused tests: 114/114 passed after review fixes;
- full solution build: passed with 0 errors;
- full solution tests: 981/981 passed after review fixes;
- targeted rerun of one initially flaky restore-diagnostic test: passed; the
  unchanged full suite then passed;
- focused `dotnet format --verify-no-changes`: passed;
- `./scripts/check-private-paths.sh`: passed;
- specification/source whitespace scan: passed;
- `git diff --check`: passed.

## Deferred

- CLI/base-scan integration and all fact projection;
- hash-only projector wiring and validated span propagation;
- ingestion provenance rules and downstream persistence;
- #551 screen-to-data composition;
- #552 copy/clone candidate evidence;
- any Windows exporter or probe;
- hidden-local raw identity rendering;
- richer COM/VBE/macro extraction;
- runtime/data analysis and customer-specific discovery.
