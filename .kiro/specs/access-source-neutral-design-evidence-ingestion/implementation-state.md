# Source-Neutral Access Design-Evidence Ingestion Implementation State

Status: specification drafted; implementation not authorized

Issue: #550

Parent epic: #549

Dependent issues: #551, then #552

Branch: `codex/spec-access-design-evidence-ingestion`

Base: `origin/dev`

Base SHA: `09072293f146abcc7ccdbdba5dd0febca8f9fd1e`

Base merge-base with `origin/main`:
`1e30032cfac2fa89b088f7cc81bba7fb3467faf9`

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

## Deferred

- all product implementation;
- #551 screen-to-data composition;
- #552 copy/clone candidate evidence;
- any Windows exporter or probe;
- hidden-local raw identity rendering;
- richer COM/VBE/macro extraction;
- runtime/data analysis and customer-specific discovery.
