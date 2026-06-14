# Snapshot Diff By SHA Implementation State

Status: mostly implemented with single-index precision follow-ups open.

Branch/PR:

- Implemented across snapshot-diff work already merged into `dev`.

Scope Implemented:

- `tracemap snapshot-diff --before <path> --after <path> --out <path>`.
- Single-index and combined-index input detection.
- Snapshot metadata, identity, commit SHA, coverage, schema, and extractor validation.
- Combined-index delegation to the combined diff engine for source, coverage, endpoint, surface, edge, graph, and opt-in path evidence.
- Deterministic Markdown and JSON output.
- Snapshot-specific rule catalog entries and limitations.
- Redaction for raw URLs, repository names, local roots, private paths, unsafe values, and delegated combined metadata.

Open Follow-Ups:

- Full single-index endpoint, contract-shape, dependency-surface, and graph projectors.
- Malformed metadata gap emission.
- Same-SHA changed-evidence warning.
- Adapter-specific validation if future projector work touches language outputs.

