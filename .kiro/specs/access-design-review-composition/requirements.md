# Access Design-Review Composition Requirements

## Purpose

Compose already-shipped Microsoft Access evidence into release review so a
reviewer can understand the statically observed database design and the exact
coverage gaps without reopening the Access extraction boundary.

## Requirements

### 1. Dedicated release-review section

1. Release review SHALL expose an `AccessEvidence` section rendered as
   `Access Design Evidence` in Markdown and as a structured JSON section.
2. The section SHALL use `ReleaseReviewStatuses` and SHALL represent missing or
   reduced evidence as `ReleaseReviewGap` rows, never as invented status values.
3. `--scope access-evidence` SHALL select the section; the default `all` scope
   SHALL include it.

### 2. Reuse shipped evidence only

1. The composition SHALL read existing `legacy.access.*` and Access fact rows
   from single or combined indexes.
2. The implementation SHALL add no Access COM calls, catalog reads, source
   extraction, VBA access, macro inspection, row reads, or execution behavior.
3. The section SHALL use the after snapshot as bounded design context and SHALL
   make no before/after change claim.

### 3. Safe design projection

1. The section MAY project categorical database inventory, table/field/index
   design, declared relationships, saved-query shapes/dependencies, external
   boundary categories, opaque stable design keys, and count-only
   form/report/VBA/macro metadata.
2. UI, VBA, event, navigation, and macro item facts SHALL NOT be upgraded into
   supported product-reader evidence; their unavailable coverage remains a
   structured gap.
3. Output SHALL NOT render raw SQL, query hashes, connection strings, external
   source hashes, credentials, source text, VBA, macro bodies, captions,
   expressions, private object names, local paths, or private infrastructure.

### 4. Provenance and limitations

1. Findings SHALL preserve upstream rule ID, evidence tier, commit SHA,
   extractor ID/version, repository-relative file span, coverage label,
   supporting fact ID, and upstream limitations where available.
2. Missing extractor provenance SHALL make the section unavailable with a
   structured compatibility gap rather than silently projecting incomplete
   provenance.
3. The section SHALL state that it does not prove row contents, runtime use,
   query execution, UI reachability, VBA/macro execution, effective
   permissions, external connectivity, production state, release approval, or
   migration safety.

### 5. Determinism and validation

1. Findings and gaps SHALL have deterministic identities and ordering.
2. Tests SHALL cover single and combined indexes, count-only coverage, safe
   schema/query/relationship/external projections, missing provenance, scope
   selection, and protected-value suppression.
3. Focused and full solution validation, private-path checking, and
   `git diff --check` SHALL pass before the PR is handed off.
