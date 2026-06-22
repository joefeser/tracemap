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

- [x] Inventory current vault/docs-export safe-name, slug, alias, tag, and
      redaction helpers.
- [ ] Define closed input fields for generated display names.
- [ ] Add deterministic fallback names for unsafe, empty, long, ambiguous, or
      duplicate values.
- [ ] Preserve stable IDs in frontmatter/metadata/JSON even when the display
      name is shortened or hashed.
- [ ] Add tests for collision, truncation, unsafe token, Unicode/spacing, and
      case-insensitive filesystem behavior.
- [ ] Add tests proving unsafe preferred fields, such as route path keys, invoke
      deterministic fallback naming at the correct pipeline stage.
- [ ] Prove collision disambiguator hashes derive from stable evidence IDs, not
      display names.

## Implementation Slice 2: Vault Index And Cross-Link Polish

- [ ] Add or strengthen route/endpoint, symbol, data-surface, package, rule,
      gap, and limitation indexes when evidence families are present.
- [ ] Add route-flow/property-flow navigation entries when compatible report
      evidence is supplied.
- [ ] Ensure all cross-links use safe slugs or stable IDs.
- [ ] Add tests proving cross-links still resolve after a target entry receives
      a deterministic disambiguated slug.
- [ ] Render missing-neighbor states as absence/gap context rather than
      conclusions.
- [ ] Add tests distinguishing a wholly absent evidence family from a present
      family with a missing neighbor.
- [ ] Document and test per-surface absence behavior if vault and docs-export
      intentionally diverge for the same evidence family.
- [ ] Add deterministic output tests for shuffled input order.

## Implementation Slice 3: Docs-Export Chunk Navigation

- [ ] Add stable section anchors and backreferences where supported.
- [x] Keep claim/citation-first chunk sections near the top of each chunk.
- [ ] Add question-oriented chunk grouping for endpoint, route-flow, touched
      file/symbol, data surface, package surface, weak evidence, gap, and
      limitation contexts where inputs support them.
- [ ] Add property-flow chunk grouping where compatible property-flow report
      evidence is supplied.
- [ ] Emit `docs-export.gap.unsupported-question-family.v1` for unsupported
      additive views without duplicating canonical unsupported-family gaps.
- [x] Prove identical inputs yield identical chunk boundaries and chunk order
      under row-order changes.
- [ ] Enforce or document maximum chunk size or sectioning policy.
- [ ] Add a test that oversized generated chunk text is sectioned or rejected
      according to the documented size/sectioning policy.
- [ ] Add Markdown/JSONL parity tests for titles, anchors, citations,
      limitations, and related IDs.
- [ ] Add tests for hidden/local rejection or omission of absolute paths, raw
      remotes, raw URLs, hostnames, raw SQL, raw config values, connection
      strings, and source snippets.

## Implementation Slice 4: Compatibility And Documentation

- [x] Document additive fields, stable identity fields, and display/navigation
      helper fields.
- [ ] Document surface-specific safety behavior for vault and docs-export
      before emitting new navigation output.
- [ ] Document migration behavior for existing vault `graph.json` and
      docs-export JSONL consumers.
- [ ] If a schema version bump is introduced, add old-format tolerance tests
      where the previous format is parseable.
- [ ] If no schema version bump is introduced, record that old-format tolerance
      tests are intentionally deferred because the changed fields are
      optional/additive.
- [x] Preserve generated-file sentinel, content-hash, and `--force` behavior.
- [x] Update `docs/VAULT_EXPORT.md` and `docs/EVIDENCE_DOCS_EXPORT.md` only for
      changed product behavior.
- [ ] Add rule catalog entries before emitting any new finding/gap rule IDs.

## Validation Checklist For Implementation PRs

- [x] `dotnet test src/dotnet/TraceMap.sln --filter "VaultExport|EvidenceDocs"`
- [x] `dotnet build src/dotnet/TraceMap.sln`
- [x] `dotnet test src/dotnet/TraceMap.sln`
- [x] `./scripts/check-private-paths.sh`
- [x] `git diff --check`

## Deferred Follow-Ups

- External RAG import adapters.
- Vector database schemas.
- LLM-generated summaries.
- Hosted explorer or site publishing changes.
- Browser/computer-use runtime evidence.
- Source snippet rendering.
