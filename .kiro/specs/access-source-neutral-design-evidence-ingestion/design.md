# Source-Neutral Access Design-Evidence Ingestion Design

## Scope

This design introduces a protected local input boundary between an
owner-controlled Access design export and TraceMap's already-shipped pure Access
projectors.

It does not change the production `AccessComReader`. The production reader
continues to emit schema, relationships, saved-query shape, external
boundaries, and count-only UI/VBA/macro coverage with explicit unavailable
gaps.

It does not implement #551 screen-to-data flow composition or #552 copy/clone
candidate evidence. Those specifications depend on an approved version of this
contract.

## Existing evidence inventory

### Pure projector contracts

| Existing component | Accepted protected input | Safe projection | Current important gaps |
| --- | --- | --- | --- |
| `AccessUiTextParser` | bounded form/report design text | raw UI surface/control records plus design hash | malformed design, text/line limit, unsupported surface/property shape, unavailable control identity |
| `AccessUiProjector` | UI surface/control/event records and known object/field catalogs | surface/control identities, direct/static binding candidates, event categories, design hashes | ambiguous/unresolved binding, protected expression, unsupported/dynamic event |
| `AccessVbaProjector` | bounded module identity/kind/source and event references | module/procedure declarations, allowlisted calls/navigation candidates, exact event binding candidates | dynamic dispatch, unresolved/ambiguous targets, missing terminators, continuation/collection/call limits |
| `AccessMacroProjector` | macro name/category/owner/ordinal/startup role | deterministic inventory identity with body omitted | owner unavailable, body omitted by category, collection limit |
| `AccessFactBuilder` | normalized projections plus matched Access scan input | standard rule-backed facts and Tier4 gaps | currently uses the owning database span for UI/macro evidence and conditionally persists safe display names |

### Fact and rule reuse

The design reuses:

- `AccessFormDeclared`, `AccessReportDeclared`, `AccessControlDeclared` under
  `legacy.access.ui-surface.v1`;
- `AccessBindingDeclared` under `legacy.access.binding.v1`;
- `AccessVbaModuleDeclared`, `AccessVbaProcedureDeclared`, and
  `AccessNavigationCandidate` under `legacy.access.vba.v1`;
- `AccessEventBindingCandidate` under
  `legacy.access.event-binding.v1`;
- `AccessMacroDeclared` and protected-body gaps under
  `legacy.access.macro-gap.v1`;
- `AnalysisGap` under the supporting rule or
  `legacy.access.coverage-gap.v1`.

The current catalog accurately states that the v0 product reader acquires no
item-level UI/VBA/macro evidence. Implementation of this contract must amend
possible tiers and provenance wording only where source-neutral textual or
structured input adds a real, separately identified evidence source.

### Intentionally disconnected paths

`AccessComReader` exposes count-only UI, VBA, and macro inventory methods. It
does not feed item identities, design text, VBA source, or macro bodies to the
pure projectors. Completed Windows probes and representative validation remain
the authority for this boundary.

The future design importer is a Mac-capable, in-process artifact reader. It
does not call `AccessComReader`, `AccessWorkerHost`, COM, DAO, VBE, Access
automation, or the Windows smoke harness.

## Proposed files

An owner supplies a directory or immutable archive containing:

```text
access-design-manifest.json
access-design-records.ndjson
```

The manifest is small, versioned, canonical JSON. Records are one bounded JSON
object per line. An archive format is not required for v1; if later added, it
must reject links, traversal, duplicate entries, decompression bombs, and
non-allowlisted members.

The importer computes the content hash itself. The manifest's declared content
hash covers the exact record file bytes. Neither local path enters evidence.

## Manifest shape

Conceptual v1:

```json
{
  "schema": "tracemap.access-design-evidence.v1",
  "producer": {
    "id": "owner-controlled-export",
    "version": "1.0.0",
    "mechanism": "preexisting-text-export"
  },
  "repository": {
    "identityHash": "<sha256>",
    "commitSha": "<40-or-64-hex>"
  },
  "baseScan": {
    "manifestSha256": "<sha256>",
    "databaseIdentityHash": "<sha256>"
  },
  "sourceCopy": {
    "sha256": "<sha256>",
    "binding": "hash-identical"
  },
  "records": {
    "sha256": "<sha256>",
    "count": 0,
    "countsByKind": {}
  },
  "capabilities": {
    "coordinates": "mixed",
    "catalogCompleteness": "declared-partial",
    "identityDisclosure": "hash-only"
  },
  "exportedAtUtc": "optional informational value"
}
```

All string enums are closed. Producer IDs and versions are allowlisted,
bounded metadata, not trust claims. The producer does not gain authority to
define rule IDs, evidence tiers, or conclusions.

## Record envelope

Every NDJSON record contains:

```json
{
  "schema": "tracemap.access-design-evidence.record.v1",
  "kind": "ui-surface",
  "recordId": "<producer-local-id>",
  "parentRecordId": null,
  "source": {
    "documentRole": "form-design-export",
    "documentSha256": "<sha256>",
    "coordinateStatus": "exact-lines",
    "startLine": 1,
    "endLine": 20
  },
  "completeness": "complete",
  "payload": {}
}
```

`recordId` is for referential integrity only. TraceMap derives its own stable
record key and never trusts producer IDs as global identities. Source document
roles are categorical; filenames and paths are prohibited.

Payload schemas are strict and kind-specific:

- catalog objects carry object role, protected local identity, optional
  existing TraceMap stable key, parent role, and ordinal;
- UI design documents carry bounded protected form/report design text,
  document hash, line count, and completeness so `AccessUiTextParser` can parse
  them transiently and coordinate claims can be validated independently;
- UI surfaces and controls carry the raw fields already supported by
  `AccessUiProjector`;
- VBA modules carry bounded source because the existing parser requires it;
- event references carry only roles and a transient protected value;
- macro inventory has no field capable of holding a body or action;
- producer gaps use a closed classification namespace and cannot assert
  conclusions.

Unknown fields are not ignored silently. They produce a deterministic
unsupported-field gap or reject the record according to whether the known
projection remains independently valid.

## Import pipeline

```text
explicit local input
  -> path/reparse/size preflight
  -> bounded manifest parse
  -> repository/commit/base-scan binding
  -> record-file hash verification
  -> streaming strict record validation
  -> canonical record IDs and duplicate analysis
  -> transient protected catalog
  -> existing pure projectors
  -> provenance/tier/span adapter
  -> existing AccessFactBuilder
  -> standard artifacts and downstream consumers
```

The transient protected catalog is disposed after projection. It never crosses
the Access worker protocol. Implementation should avoid writing temporary
normalized files; if a future parser requires scratch, that is a new privacy
decision.

## Required narrow contract changes

### Identity disclosure

`AccessSafeValues.Identity` currently may retain a display name when an
identifier passes its character policy. That policy prevents obviously unsafe
values but does not establish that a customer object name is non-sensitive.

The source-neutral path therefore needs an explicit disclosure argument:

```text
AccessIdentityDisclosurePolicy.HashOnly
```

The policy sets every projected `DisplayName` to null while retaining
role-separated `NameHash` and `StableKey`. Existing count-only/database scan
behavior remains unchanged unless separately approved.

### Source coordinates

Current UI and macro projections do not carry source coordinates, and
`AccessFactBuilder` anchors them at the database container span. The v1
normalizer needs an optional validated coordinate record associated with each
projection and gap.

Exact lines are allowed only when:

- the producer declares a line-addressable document role;
- the record contains its document hash;
- the importer validates positive ordered bounds;
- nested records fall inside their declared parent/document bounds.

Otherwise evidence uses `container-only` at the repository-relative database
span `1:1` and emits a scoped coordinate gap when the contract claimed but
failed to prove exact coordinates.

VBA already preserves real module line coordinates. The importer must translate
module-relative lines into the declared source-document coordinate basis
without inventing offsets.

### Provenance

Facts need supporting input record IDs and bundle provenance. Prefer bounded,
safe properties such as:

- `designInputSchema`;
- `designInputHash`;
- `designProducerId`;
- `designProducerVersion`;
- `designMechanism`;
- `copyBinding`;
- `sourceCanonicalRecordIds`;
- `coordinateStatus`;
- `coverageLabel`.

Do not store exported timestamps, local paths, producer exception messages, or
raw producer record IDs. Producer-local IDs are transient referential aliases
only; even safe-token producer IDs SHALL NOT enter facts, gaps, manifests,
indexes, reports, or artifact hashes.

## Deterministic identity and ordering

TraceMap canonicalizes closed enums, integer values, object keys, and protected
strings before deriving identities. Protected strings are role-hashed before
they can enter keys.

Stable record identity includes:

```text
schema
database identity seed
record kind
parent stable key
semantic role
ordinal when meaningful
role-separated protected identity hash
```

Input line order and producer-local IDs do not affect output ordering or
provenance. Producer parent references are resolved transiently and then
replaced with TraceMap-derived canonical parent/record keys. Records are sorted
by kind, parent stable key, semantic role, ordinal, and stable record key before
projection.

Equivalent duplicates collapse. Conflicting duplicates poison only the
conflicted stable identity and emit a Tier4 gap; the importer does not choose
first or last. Facts cite only the resulting canonical record keys.

## Tier and coverage decisions

| Input condition | Maximum tier | Coverage |
| --- | --- | --- |
| Validated structured catalog/design record, exact base binding, complete producer capability | Tier2Structural | `structured-design-observed` |
| Bounded design text, VBA text, expression, or call shape | Tier3SyntaxOrTextual | `bounded-textual-design` |
| Owner-attested derived copy with otherwise supported evidence | underlying tier plus required Tier4 lineage gap | `copy-lineage-owner-attested` |
| Missing catalog category or producer-declared partial category | Tier4Unknown gap | `source-declared-partial` |
| Ambiguous/unresolved target, dynamic dispatch, unsupported SQL/expression, malformed or capped input | Tier4Unknown gap | classification-specific partial |
| Unbound/mismatched repository, commit, base scan, database, or record hash | no design conclusion | `input-unbound` |

The importer never upgrades evidence based on an exporter's self-description.

## Persistence matrix

| Data | Input only | In-process projection | Standard artifacts | Logs/checkpoints |
| --- | --- | --- | --- | --- |
| Raw names and customer identities | yes | transient role-hashed matching | no | no |
| Form/report design and expressions | yes | transient bounded parsing | hashes/lengths/categories only | no |
| VBA source/comments/literals | yes | transient bounded parsing | declaration/call candidates, hashes, validated spans only | no |
| Macro actions/arguments/bodies | prohibited in v1 | no | no | no |
| Database/input paths | caller only | validation only | no | no |
| Connection/credential/private-host values | prohibited/protected leak fixture | no semantic projection | no | no |
| Rule/tier/coverage/limitations | derived | yes | yes | categorical only |
| Bundle/record/document hashes | yes | yes | bounded provenance | categorical status only |

Input conformance does not sanitize the caller-owned bundle. Operators must
still protect and dispose of it according to their policy.

## Bounds and failure behavior

The importer checks manifest and record-file byte declarations before parsing
and hashes only a file within the byte ceiling. It then streams and validates
the complete bounded NDJSON set without projecting facts. If the declared or
observed record count exceeds the envelope cap, the entire design input is
invalid and produces no design conclusions; a prefix is never retained.

After a complete bounded set passes envelope validation, the importer resolves
producer-local references, derives canonical record keys, detects conflicts,
sorts canonically, and only then invokes projectors. Per-record text violations
reject the scoped record. Per-collection and output limits select only from the
canonical ordering and emit aggregate partial-coverage gaps. Each supported
collection uses the existing `AccessLimits` ceiling or a narrower
ingestion-specific ceiling.

Failures are closed classifications, for example:

- `AccessDesignInputSchemaUnsupported`;
- `AccessDesignInputManifestInvalid`;
- `AccessDesignInputHashMismatch`;
- `AccessDesignInputCommitMismatch`;
- `AccessDesignInputBaseScanMismatch`;
- `AccessDesignInputDatabaseUnbound`;
- `AccessDesignInputCopyOwnerAttested`;
- `AccessDesignInputRecordUnsupported`;
- `AccessDesignInputFieldRejected`;
- `AccessDesignInputCoordinateUnavailable`;
- `AccessDesignInputDuplicateConflict`;
- `AccessDesignInputLimitReached`.

No exception, JSON fragment, raw value, or path accompanies a gap.

## Candidate export mechanisms

No candidate is selected by this specification.

| Candidate | Mac ingestion | Evidence potential | Boundary concerns | Disposition |
| --- | --- | --- | --- | --- |
| Preexisting owner-controlled text/structured exports | yes | can supply forms/reports, modules, events, and identities | export provenance and completeness vary; protected source must remain local | compatible input candidate if independently produced and manifest-bound; no producer selected |
| Hand-authored synthetic bundle | yes | exercises full contract deterministically | not evidence about a real Access database or exporter | approved for Mac validation only |
| Access `SaveAsText`-style form/report export on a controlled copy | output could be ingested on Mac | aligns with existing UI text parser | creating/opening/saving/exporting surfaces may load design or trigger Access behavior; provenance/canaries required | future Windows threat review; not authorized |
| Automated Access COM exporter | output could be ingested on Mac | potentially structured and repeatable | widens production COM and worker protected-material boundary | rejected for v1 implementation unless separately authorized |
| VBE/VBComponents export | output could be ingested on Mac | module identity and exact VBA source | requires VBE access/trust surface and protected source extraction | deferred to separate security-reviewed mechanism |
| DAO/system-table/catalog row reads | output could be ingested on Mac | may expose identities and internal metadata | row-read boundary, undocumented structures, and surface-load behavior | not authorized |
| Access Documenter/manual report | possible | human-visible inventory | unbounded private content, weak deterministic schema, formatting variance | not selected |
| Third-party MapMyApp or equivalent | possible | unknown proprietary evidence | dependency, privacy, provenance, and licensing coupling | explicitly out of scope |

An exporter passing the v1 schema proves only schema conformance. It does not
prove non-execution, safe generation, correct lineage, or completeness.

## Validation design

Mac tests construct synthetic manifests and NDJSON records directly. They call
the future importer and existing pure projectors without Windows or COM.

Validation must:

- reverse record and property order and compare every standard artifact;
- exceed the record envelope cap in forward and reverse order and prove both
  inputs emit no design conclusions and the same categorical limit gap;
- mutate hashes, commit, base scan, coordinates, enum values, and duplicate
  payloads;
- plant protected markers in every accepted transient field and every
  prohibited field;
- exercise structured versus textual tier selection;
- prove standard and combined indexes preserve provenance and gaps;
- prove reports, evidence docs, vault, release review, and local review bundles
  contain no planted marker;
- prove the production `AccessComReader` remains count-only through existing
  reflection/fake dynamic catalog tests.

No Windows run is required until a producer/export mechanism is proposed.

## Deferred dependent work

- #551 defines roots, edges, traversal, gaps, and presentation for
  screen-to-data composition after this contract is approved.
- #552 defines conservative mutation/copy/clone candidates after #550 and #551.
- Any hidden-local identity sidecar requires a separate privacy review.
- Any Windows exporter requires a separate threat design, explicit owner
  authorization, canaries, protected-output scanning, unchanged-original proof,
  process cleanup, and retained sanitized checkpoint.
