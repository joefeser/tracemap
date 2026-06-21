# Legacy Data Model Reporting Integration Tasks

Current state: `implemented-slice-1-with-follow-ups`.

PR #236 merged the first implementation slice. Remaining unchecked items below
are follow-up slices and should not be read as blocking the merged descriptor
projection/reporting integration.

Recommended first implementation PR: tasks 1 through 4, limited to shared
descriptor projection and report/query readers that already consume
`legacy-data` surfaces. Keep extractor changes, persisted derived rows,
release-review scoring expansion, vault/RAG expansion, and explorer UI filters
for follow-up PRs unless review shows a narrower safe path.

- [x] 1. Add shared legacy data model descriptor projection helpers.
  - [x] Normalize current `LegacyData*` facts and near-term model identity
        fields into a report/export descriptor view.
  - [x] Exclude `AnalysisGap` facts from terminal surface projection.
  - [x] Preserve source rule IDs, evidence tiers, source labels, commit SHAs,
        extractor versions, file spans, supporting IDs, coverage, and
        limitations.
  - [x] Add safe label/hash selection and redaction metadata.
  - [x] Add duplicate stable identity detection.
  - [x] Escape Markdown-sensitive characters in safe display labels and hashes
        before rendering Markdown table cells.
  - [x] Accept a `ClaimLevelContext` or equivalent output-profile parameter in
        the projection helper and derive `displayClearance` deterministically;
        default to hash-only display when no context is provided.
  - [x] Add a test proving projection without claim-level context records
        `displayClearance = false` and renders hash-only display values.

- [ ] 2. Integrate descriptors into combined dependency reports and paths.
  - [x] Render safe `legacy-data` surface rows with model metadata where
        available.
  - [x] Fall back to current safe legacy data fields when model metadata is
        absent.
  - [ ] Emit scoped availability gaps for missing optional graph tables only
        when relevant.
  - [x] Prevent double projection when persisted derived rows exist.
  - [ ] Add a derived-surface discriminator check so double-projection tests can
        pass once persisted rows land.
  - [x] Add deterministic ordering and stable ID tests.

- [ ] 3. Integrate descriptors into reverse query support.
  - [x] Allow `legacy-data` surface selection where current selector contracts
        support it.
  - [ ] Add stable ID/hash selector support if safe descriptor labels are
        hidden.
  - [ ] Preserve source and reverse rule IDs on selected surfaces, roots, paths,
        and gaps.
  - [ ] Downgrade no-path conclusions under reduced coverage, missing optional
        graph tables, unsupported ORM gaps, or generated-code uncertainty.
  - [ ] Before emitting any no-path gap, confirm or register its rule ID in the
        rule catalog. Record the chosen rule ID here before implementation PR
        merge: `[TBD - must be filled in before PR merges]`.
  - [ ] If reusing an existing combined reverse rule for no-path gaps, verify
        its documented limitations explicitly cover the `legacy-data` surface
        kind and update the catalog entry if they do not.

- [ ] 4. Integrate descriptors into route-flow rendering.
  - [x] Render terminal `legacy-data` rows only when a credible static path
        reaches the surface.
  - [ ] Render supporting descriptor rows separately from terminal rows.
  - [ ] Cap classification by weakest evidence, ambiguity, high fan-out,
        generated-code uncertainty, and reduced coverage.
  - [ ] Add tests that route-flow wording avoids runtime database claims.

- [ ] 5. Extend diff, impact, and release-review consumers. Follow-up PR.
  - [ ] Compare legacy data model surfaces using stable descriptor identity.
  - [ ] Preserve source and workflow rule IDs on changed descriptor rows.
  - [ ] Render unsupported ORM and generated-code uncertainty gaps.
  - [ ] Add release-review checklist rows that reference safe descriptor
        categories, finding IDs, gap IDs, and rule IDs.
  - [ ] Add tests proving release-review checklist rows do not include raw SQL,
        config values, connection strings, hostnames, private routes, or private
        labels.
  - [ ] Keep deterministic review priority inputs closed and static.

- [ ] 6. Extend vault/RAG/evidence graph export. Follow-up PR.
  - [ ] Export `legacy-data` descriptor surfaces as graph nodes with safe
        labels or hashes.
  - [ ] Apply claim-level filtering to descriptor labels, endpoints, symbols,
        table/column names, and relationship names.
  - [ ] Emit exporter-specific gaps only under documented exporter rule IDs.
  - [ ] Keep RAG exports as deterministic static artifacts without embeddings,
        vector databases, prompt summaries, or AI classification.

- [ ] 7. Extend static HTML explorer rendering. Follow-up PR.
  - [ ] Add descriptor metadata to surface, evidence, gap, rule, and limitation
        views.
  - [ ] Add closed-vocabulary filters for metadata format, descriptor role,
        source artifact type, rule ID, evidence tier, coverage, and gap kind.
  - [ ] Ensure explorer search scans only safe rendered fields.
  - [ ] Validate generated HTML, JSON, CSS, and JavaScript for unsafe values.

- [ ] 8. Add focused compatibility and safety tests.
  - [x] Current facts without model fields.
  - [x] Near-term model fields with no relationship links.
  - [ ] Generated-code links absent, stale, ambiguous, or syntax-only.
  - [ ] Unsupported ORM represented only by `AnalysisGap`.
  - [ ] Missing optional combined tables.
  - [ ] Unknown future `legacy.data.*` rule or descriptor role.
  - [x] Duplicate stable identity.
  - [x] Public/demo redaction for descriptor labels and hashes.
  - [x] Safe display labels and hashes containing Markdown-sensitive characters
        are escaped in Markdown table cells.
  - [ ] Release-review checklist output is covered by public/demo safety tests.
  - [x] JSON output omits or encodes unsafe raw values rather than relying on
        Markdown escaping.
  - [x] Byte-stable Markdown and JSON where touched.

- [ ] 9. Update docs and rule catalog when implementation emits new rules.
  - [ ] Reuse existing source and workflow rules where possible.
  - [ ] Add catalog entries for any new projection, selector, exporter gap,
        validation failure, or limitation rule before emitting it.
  - [ ] Confirm unknown-vocabulary gaps reuse `legacy.data.model.surface.v1` or
        register a narrower rule before emitting them.
  - [ ] Document limitations for every new rule.
  - [ ] Update acceptance docs for new user-facing outputs.

- [ ] 10. Run implementation validation before merging product code.
  - [x] Focused .NET tests for touched reporting/export layers.
  - [x] CLI smoke with synthetic or public-safe artifacts.
  - [x] `git diff --check`.
  - [x] `./scripts/check-private-paths.sh`.
  - [ ] Relevant pinned smoke checks from `docs/VALIDATION.md` when language
        adapters or shared graph behavior change.
