# Consolidated compiled-wrapper evidence handoff

## Later work-machine acceptance: use only after local final gates are recorded

Inputs (keep values and artifacts private on the work machine):

```text
EXPECTED_TOOL_COMMIT = <published validated branch commit>
PRIVATE_SOURCE_ROOT = <existing source checkout>
PRIVATE_ACCEPTANCE_ROOT = <existing retained notes, indexes and receipts>
COMPILED_INPUTS = <exact caller and wrapper assemblies, with available correspondence evidence>
ROOT_CLASS_AND_PROPERTY = <class.property under investigation>
PROCEDURE_IDENTITIES_AND_DDL = <owner-provided server/database/schema/procedure identities and scripts>
```

This is one consolidated read-only fit check, not permission to run the application,
SQL, containers, impersonation flows or requests. Preserve accepted baseline hashes.
First verify exact tool revision and CLI/Core/Reporting provenance, then read the
recorded local end-to-end gate in the validation document. If still pending, stop
before a private retry and report that prerequisite; do not repeat old diagnostic runs.

Inspect retained artifacts first. Map the chosen root's actual operations to the
new separate lifecycle route and record unsupported operations with file/line or
compiled-body evidence. Do not use the legacy object-path or source-reader routes
as substitutes. A source-backed Select predicate, ArrayList custom parameter object,
unavailable wrapper body, complex DDL, or unresolved deployment correspondence is
still a distinct boundary; synthetic passing cases do not remove it.

Only if the inputs and exact shape fit, perform a fresh offline scan into a new
private output directory. Keep source/index/binary correspondence explicit. Bind
every observed execution to the independently supplied identity, and every consumer
to its exact table, column and read site. The new comparison route currently admits
synthetic JSON result declarations only: do not relabel owner DDL or a manually
transcribed production schema as synthetic evidence. If no truthful admitted schema
route exists, report that gap rather than manufacture a passing comparison.

Return one sanitized matrix: input presence, exact revision/binary verification,
root/guard, execution/table/column linkage, SQL schema origin versus application value
origin, raw/projection consumers, refresh ordering, potential partial-failure states,
baseline preservation, refusal reason and smallest remaining capability. Preserve
unknowns and partial coverage. No private source, paths, SQL text or hashes in public
commits. No PR, merge, runtime/deployment or migration-readiness claim.

The historical foundation instructions below describe the unchanged older route;
the lifecycle additions above and current validation document supersede them only
for the separate lifecycle contract.

Read AGENTS, `.kiro/specs/sql-compiled-wrapper-evidence/`,
`docs/validation/SQL_COMPILED_WRAPPER_EVIDENCE.md`, the current IL rules and
source SQL contracts. Verify the exact branch/head and validation receipts first.
Use only the public corpus in `samples/sql-compiled-wrapper-evidence`.
Do not request another work-machine run to rediscover known unsupported shapes.

## Implemented bounded scope

IL extractor 0.2.3 supports a closed, dual-reader-agreed static object protocol:
unique compiled call bodies, positional substitution, scalar constructor fields
and getters, attached parameters, fresh DataSet Fill/return, and constant named
first-row consumers. Evidence remains Tier3, not SQL contract or runtime proof.

Preserve two independent matrices:

- Original: 20 roots across C#/VB, 10 static candidates and 10 unknowns, two local
  shape facts. Direct, Forwarded, FieldObject, PropertyObject and MergeFresh are
  positive; Mutated, Escaped, Branch, MergeExisting and MergeMutated are unknown.
- Named-table: 26 roots across C#/VB, four static candidates and 22 unknowns.
  First/Second each allocate a property-held fresh DataSet, add two distinct named
  tables, merge first tables from two different returned DataSets, and consume
  destination slot zero or one. Conditional, Mutated, UnknownHelper, RepeatedMerge,
  ClearMerge, Existing, Exceptional, DuplicateName, OutOfRange, DynamicSlot and
  ThirdTable remain refused.

Each named-table candidate binds the selected execution, command/parameters,
filled and destination DataSets and distinct table identities. IDs are root-local;
join them only with their root/body/binary/frame/offset context. The relation is
`fresh-empty-table-schema-union-candidate`, not result aliasing or snapshot copying.
The native smoke matrix schema is version 3; original `rows` and new `tableRows`
have separate summaries. Never change expectations just to obtain a pass.

## Deliberate boundaries

Unknown helper effects, writes, Clear/re-Merge, conditional and exceptional paths
refuse the entire candidate. In-memory lifecycle controls separately demonstrate
guarded/unmatched replacement, preserved schema versus replaced values, and
partial state at injected failure boundaries. These do not establish positive
extraction of mutations, exception rollback, session state or private behavior.

Remaining capabilities need separate bounded designs: custom parameters in
ArrayList, cleanup/fallback alternatives, Select predicates and value writes,
application-authored derived tables, session/refresh identity, complex DDL with
locals/joins/computed projections/ordering, and compiled-candidate-to-contract
comparison. No legacy binding can stand in for those missing proofs. Owner
server/database/schema/procedure identity and exact binary/deployment correspondence
remain separate inputs; manual declarations must retain their true origin.

## Verification and return

Reproduce focused object-path/lifecycle tests and the compile-only native matrix.
Pin missing/ambiguous accessor bodies, wrong framework identity, private-field
ownership and budget refusals. Preserve legacy source and IL contracts, hashes,
tiers and denominators. Run one frozen full Debug `-warnaserror` gate for shared
core changes, followed sequentially by retained smokes; document relevant pinned
checks or explicit justified deferrals. Update tasks and implementation state.

Do not invoke fixture methods, SQL providers, containers, a private application,
or private inputs. No PR, merge, ACK-policy change or review spend is authorized
by this prompt. Return exact head, each matrix, provenance, failures/reruns,
remaining gaps and the smallest next action. Passing synthetic candidates are
not private SQL acceptance, deployment readiness or a clean application verdict.
