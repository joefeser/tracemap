# Evidence Export Navigation Polish Tasks

## Spec Delivery

- [x] Read AGENTS and current execution report.
- [x] Review issue #189 and adjacent export specs.
- [x] Inspect current vault/docs-export docs, rule catalog, code seams, and
      tests enough to avoid duplicating shipped work.
- [x] Create this spec packet.
- [x] Run Opus Kiro spec review and record result.
- [x] Run Sonnet Kiro spec review and record result.
- [x] Patch Medium+ merge-readiness findings.
- [x] Run at most two Kiro re-review cycles if needed.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Commit, push, and open a ready PR to `dev`.
- [x] Run the PR loop.

## Implementation Slice 1: Name And Navigation Model

- [ ] Inventory current vault/docs-export safe-name, slug, alias, tag, and
      redaction helpers.
- [ ] Define closed input fields for generated display names.
- [ ] Add deterministic fallback names for unsafe, empty, long, ambiguous, or
      duplicate values.
- [ ] Preserve stable IDs in frontmatter/metadata/JSON even when the display
      name is shortened or hashed.
- [ ] Add tests for collision, truncation, unsafe token, Unicode/spacing, and
      case-insensitive filesystem behavior.
- [ ] Prove collision disambiguator hashes derive from stable evidence IDs, not
      display names.

## Implementation Slice 2: Vault Index And Cross-Link Polish

- [ ] Add or strengthen route/endpoint, symbol, data-surface, package, rule,
      gap, and limitation indexes when evidence families are present.
- [ ] Add route-flow/property-flow navigation entries when compatible report
      evidence is supplied.
- [ ] Ensure all cross-links use safe slugs or stable IDs.
- [ ] Render missing-neighbor states as absence/gap context rather than
      conclusions.
- [ ] Add tests distinguishing a wholly absent evidence family from a present
      family with a missing neighbor.
- [ ] Add deterministic output tests for shuffled input order.

## Implementation Slice 3: Docs-Export Chunk Navigation

- [ ] Add stable section anchors and backreferences where supported.
- [ ] Keep claim/citation-first chunk sections near the top of each chunk.
- [ ] Add question-oriented chunk grouping for endpoint, route-flow, touched
      file/symbol, data surface, package surface, weak evidence, gap, and
      limitation contexts where inputs support them.
- [ ] Add property-flow chunk grouping where compatible property-flow report
      evidence is supplied.
- [ ] Emit `docs-export.gap.unsupported-question-family.v1` for unsupported
      additive views without duplicating canonical unsupported-family gaps.
- [ ] Prove identical inputs yield identical chunk boundaries and chunk order
      under row-order changes.
- [ ] Enforce or document maximum chunk size or sectioning policy.
- [ ] Add Markdown/JSONL parity tests for titles, anchors, citations,
      limitations, and related IDs.
- [ ] Add tests for hidden/local rejection or omission of absolute paths, raw
      remotes, raw URLs, hostnames, connection strings, and source snippets.

## Implementation Slice 4: Compatibility And Documentation

- [ ] Document additive fields, stable identity fields, and display/navigation
      helper fields.
- [ ] Document surface-specific safety behavior for vault and docs-export
      before emitting new navigation output.
- [ ] Document migration behavior for existing vault `graph.json` and
      docs-export JSONL consumers.
- [ ] If a schema version bump is introduced, add old-format tolerance tests
      where the previous format is parseable.
- [ ] Preserve generated-file sentinel, content-hash, and `--force` behavior.
- [ ] Update `docs/VAULT_EXPORT.md` and `docs/EVIDENCE_DOCS_EXPORT.md` only for
      changed product behavior.
- [ ] Add rule catalog entries before emitting any new finding/gap rule IDs.

## Validation Checklist For Implementation PRs

- [ ] `dotnet test src/dotnet/TraceMap.sln --filter "VaultExport|EvidenceDocs"`
- [ ] `dotnet build src/dotnet/TraceMap.sln`
- [ ] `dotnet test src/dotnet/TraceMap.sln`
- [ ] `./scripts/check-private-paths.sh`
- [ ] `git diff --check`

## Deferred Follow-Ups

- External RAG import adapters.
- Vector database schemas.
- LLM-generated summaries.
- Hosted explorer or site publishing changes.
- Browser/computer-use runtime evidence.
- Source snippet rendering.
