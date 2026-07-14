# TraceMap adapter artifact contract

These files are the machine-readable minimum shared by every language adapter.
They define the manifest and fact shapes, the minimum SQLite tables, and the
cross-adapter unsafe-value corpus. Adapter-specific facts and additive SQLite
tables remain allowed.

Run the deterministic conformance check against a generated scan directory:

```bash
python3 scripts/validate-adapter-artifacts.py <scan-output>
```

The validator checks required artifacts, manifest/fact provenance, registered
rule IDs, evidence spans and extractor provenance, repo-relative evidence
paths, unsafe values, SQLite columns, row counts, and JSONL-to-SQLite field
parity. It does not execute scanned code or prove runtime behavior, complete
coverage, production state, or release safety.

Fact IDs must be deterministic within an adapter. The current v1 contract does
not require all languages to use one identity formula; changing that boundary
requires an explicit compatibility decision because existing fact IDs are
persisted provenance.
