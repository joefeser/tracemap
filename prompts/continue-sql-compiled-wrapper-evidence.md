# Consolidated compiled-wrapper evidence handoff

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
