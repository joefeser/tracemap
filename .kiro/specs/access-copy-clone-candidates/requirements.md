# Access Copy/Clone Static Candidate Requirements

## Goal

Compose already-persisted Microsoft Access query-shape, dependency, and
screen-to-data-flow evidence into conservative copy/clone workflow candidates
without inferring intent from names or treating ordinary data mutation as a
proven clone.

## Requirements

### R1 — Existing evidence only

1. Composition SHALL read a completed standard Access `index.sqlite`.
2. It SHALL add no Access COM, DAO, VBE, database, row, query, form, report,
   macro, or VBA read/execution path.
3. It SHALL not add raw SQL to the source-neutral v1 input contract.
4. Missing role-specific evidence SHALL be a gap, not a guessed projection.

### R2 — Candidate vocabulary

1. `append` SHALL produce `Candidate` / `bulk-append-shape`.
2. `make-table` SHALL produce `Candidate` / `table-creation-shape`.
3. `update`, `bulk`, and `compound` SHALL produce `NeedsReview` mutation
   shapes.
4. Select, crosstab, delete, DDL, pass-through, union, unknown, and name-only
   evidence SHALL NOT produce a copy/clone candidate in v1.
5. No output SHALL use a definite clone or copy classification.

### R3 — Dependencies and flow

1. Persisted query dependencies SHALL appear as opaque participants with role
   `dependency-role-unknown`.
2. Candidate records SHALL reference bounded #551 flow paths that contain the
   candidate query.
3. A candidate without such a path SHALL remain valid static query evidence
   and SHALL emit `AccessCopyCloneFlowPathUnavailable`.
4. Source/target direction and field correspondence SHALL remain explicit
   Tier4 gaps.
5. Multiple candidates SHALL NOT be ordered into a parent/child sequence;
   `AccessCopyCloneParentChildSequenceUnavailable` SHALL record that gap.

### R4 — Gaps

The report SHALL represent at least:

- candidate evidence unavailable;
- source/target role direction unavailable;
- field correspondence unavailable;
- flow path unavailable;
- partial query dependency;
- dependency fan-out, external participants, and flow cycles;
- upstream dynamic/query/VBA/macro evidence gaps;
- parent/child sequence unavailable; and
- candidate/gap bounds.

An empty report SHALL never prove that no copy, clone, or mutation behavior
exists.

### R5 — Provenance

Every candidate SHALL retain exact supporting fact IDs, rule IDs, evidence
tiers, commit SHA, repository-relative file span, extractor ID/version,
coverage labels, and safe limitations. Composition gaps SHALL use
`legacy.access.copy-clone-candidate.v1`.

### R6 — Determinism and bounds

1. Facts, candidates, participants, paths, evidence, and gaps SHALL use stable
   ordinal ordering and opaque hash-derived IDs.
2. Candidate, flow-path, and gap counts SHALL be independently positive and
   bounded to 10,000.
3. Bound truncation SHALL be explicit and label coverage partial.

### R7 — Privacy

Only opaque Access stable identities and closed categorical properties SHALL
be rendered. Raw/copyable SQL, object names, VBA, expressions, literals,
values, row counts, macro bodies, connections, credentials, customer identity,
and local paths SHALL be excluded.

### R8 — Non-claims

The output SHALL NOT claim business intent, copying, cloning, semantic row
equivalence, source-to-target direction, transactionality, generated-key
correctness, parent/child sequencing, execution, runtime reachability,
completeness, correctness, production use, migration safety, release approval,
or safety to run.

### R9 — Validation

Mac-only synthetic tests SHALL cover a supported append candidate, make-table
candidate, multiple-candidate parent/child gap, an ordinary select/name-only
false positive, dynamic evidence, deterministic outputs, exact provenance,
bounds, standard-index immutability, and planted protected markers.
