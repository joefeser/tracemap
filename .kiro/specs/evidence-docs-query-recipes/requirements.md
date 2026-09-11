# Requirements: Evidence docs query recipes

1. Docs export shall emit a versioned deterministic catalog of read-only retrieval recipes over supported TraceMap SQLite index schemas.
2. Each recipe shall declare its stable ID, purpose, supported input kinds, required parameters, bounded result fields, evidence requirements, rule ID, evidence tier, and limitations.
3. Documentation chunks shall carry additive typed retrieval hints that identify applicable recipes and already-known parameter values without changing the underlying evidence claim.
4. Recipe SQL shall be parameterized, single-statement, read-only, bounded, and limited to documented TraceMap-owned tables.
5. Query recipes and hints shall preserve the requirement that useful results include stable evidence IDs, rule IDs, evidence tiers, coverage or source context, commit identity, and file spans when available.
6. The catalog shall not contain application SQL, source snippets, configuration values, credentials, local absolute paths, company terminology, private workflow prompts, business intent, target architecture, or code-generation instructions.
7. Equivalent evidence input ordering shall produce identical hints, catalog bytes, and manifest hashes.
8. Combined-index recipes and hints shall require the owning source-index identity and shall not return same-shaped evidence from another source.
