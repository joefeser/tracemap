# Evidence Export Navigation Polish Implementation State

Status: implemented
Readiness: ready-for-pr-loop
Branch: codex/implement-evidence-export-navigation-polish
PR target: dev
PR: https://github.com/joefeser/tracemap/pull/295
Primary issue: #189
Public claim level: hidden/local export behavior only; generated navigation remains presentation metadata over deterministic evidence

## Scope

This implementation branch delivers the first product slice for the reviewed
evidence export navigation polish spec. The slice is intentionally narrow:
docs-export now emits deterministic family index pages and per-chunk navigation
links in both JSONL and Markdown. It does not alter vault graph identity,
claim-level promotion, route/property-flow projection, or external RAG/vector
systems.

## Repository Grounding

Reviewed:

- `AGENTS.md`
- `docs/NEXT_EXECUTION_REPORT.md`
- GitHub issue #189
- `.kiro/specs/evidence-graph-vault-export/`
- `.kiro/specs/evidence-export-usability-polish/`
- `.kiro/specs/vault-export-hidden-safety/`
- `.kiro/specs/rag-import-evidence-docs/`
- `.kiro/specs/static-html-evidence-explorer/`
- `docs/VAULT_EXPORT.md`
- `docs/EVIDENCE_DOCS_EXPORT.md`
- `rules/rule-catalog.yml`
- `src/dotnet/TraceMap.Reporting/VaultExport.cs`
- `src/dotnet/TraceMap.Reporting/EvidenceDocsExport.cs`
- `src/dotnet/tests/TraceMap.Tests/VaultExportTests.cs`
- `src/dotnet/tests/TraceMap.Tests/EvidenceDocsExportTests.cs`

## Scope Decisions

- Treat this as a follow-up to `evidence-export-usability-polish`, not a
  duplicate first slice.
- Focus on safer naming, route/entity navigation, cross-links, navigation
  manifests, chunk boundaries, and compatibility.
- Keep stable IDs authoritative. Display titles, aliases, tags, and slugs are
  navigation aids.
- Preserve public/demo strictness and hidden/local safety labeling.
- Keep RAG/vector systems as consumers only; their output is not TraceMap
  evidence.
- Implementation slice chosen for this branch: docs-export chunk navigation.
  The exporter now populates the existing additive `links` field, writes
  `chunks/<family>/index.md` files, and renders chunk navigation sections.
- No schema version bump was introduced. Existing JSONL consumers keep the same
  chunk schema and receive non-empty `links`.
- No new rule IDs are emitted in this slice.
- Vault safe-name and collision hardening remains a follow-up from this spec.

## Review State

Spec review history:

```bash
node scripts/kiro-review.mjs --phase evidence-export-navigation-polish --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase evidence-export-navigation-polish --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Review results:

- Opus initial spec review completed with full coverage. Medium findings
  patched: unsupported additive navigation views reuse
  `docs-export.gap.unsupported-question-family.v1` rather than inventing a
  third unsupported-input rule, and property-flow navigation is now traced from
  requirements through design/tasks. Low/Medium polish patched: collision
  disambiguation applies to every colliding entry, shared vault/docs-export
  safety modes are explicit, absent evidence-family behavior must be chosen per
  surface, and docs-export chunk boundaries must be deterministic and bounded.
- Sonnet initial spec review completed with full coverage. Medium findings
  patched: hidden/local evidence locations are repo-relative only and still
  exclude absolute paths, remotes, URLs, hostnames, connection strings, and
  snippets; all-fields-absent naming falls back to `unknown-<hash>`; and tasks
  now require tests that distinguish wholly absent evidence families from
  present families with missing neighbors. Low cleanup patched: duplicate auth
  wording removed, schema-version bump triggers documented, and
  surface-specific safety docs are an explicit task.
- Sonnet re-review completed with full coverage and called the spec merge-ready
  for the spec PR scope. Pre-implementation Medium clarifications patched:
  requirements now explicitly reject or omit absolute paths, raw remotes, raw
  URLs, hostnames, connection strings, and snippets even in hidden/local mode;
  collision disambiguators are derived from stable evidence IDs rather than
  display names; and chunk size/sectioning is now a SHALL. Implementation notes
  patched into tasks/design for hidden/local rejection tests and old-format
  tolerance tests if a schema version bump is introduced.

Planned implementation review:

```bash
node scripts/kiro-review.mjs --phase evidence-export-navigation-polish --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Implementation review results:

- Sonnet implementation review completed with full coverage and returned
  Medium/Low spec-traceability findings. Patched the Medium chunk-size test
  obligation and clarified hidden classifier, slug derivation, and `--force`
  semantics.
- Sonnet re-review completed with full coverage and returned one Medium
  spec-traceability finding about absent-family behavior across vault and
  docs-export. Patched requirements/design/tasks to document per-surface
  divergence and testing obligations. Low findings around aliases, migration
  note location, route/property-flow testability, raw SQL/config tests, and
  candidate rule triggers were also patched.
- Final Sonnet re-review completed with full coverage. It reported no blockers
  to this implementation slice, but named residual Low/Medium future-slice
  spec polish. Patched the practical Medium migration-doc ordering note and
  low wording clarifications for RAG citation boundaries, absent-family
  limitations, unsafe path-key fallback, speculative linking wording, stale-link
  tests, and old-format tolerance deferral. No further re-review was run after
  that patch because the two re-review cycle cap was reached and the remaining
  work was spec clarification, not a product-code blocker.

## Validation

Planned for this spec-only PR:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Completed:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `dotnet test src/dotnet/TraceMap.sln --filter EvidenceDocsExportTests`:
  passed, 9 tests. Existing NU1903 warnings for
  `SQLitePCLRaw.lib.e_sqlite3` were reported during restore and are unrelated
  to this slice.
- `dotnet test src/dotnet/TraceMap.sln --filter "VaultExport|EvidenceDocs"`:
  passed, 42 tests. Existing NU1903 warnings for
  `SQLitePCLRaw.lib.e_sqlite3` were reported during restore and are unrelated
  to this slice.
- `dotnet build src/dotnet/TraceMap.sln`: passed with existing NU1903 warnings
  for `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 606 tests. Existing NU1903
  warnings for `SQLitePCLRaw.lib.e_sqlite3` were reported during restore and
  are unrelated to this slice.
- Final post-review `git diff --check`: passed.
- Final post-review `./scripts/check-private-paths.sh`: passed.

## Spec PR History

- PR loop found actionable Qodo/Codex findings on the first reviewed heads.
  Patched status vocabulary to `spec-ready`, confirmed the PR-open task was
  already checked on the latest head, and aligned the design `safetyProfile`
  vocabulary with existing exporter claim levels: `hidden`, `demo-safe`, and
  `public-safe`.
- Final PR loop returned `merge_ready` for head
  `6d0ed7e2f4b592ac5dd857648ee24589a7cfea51` after recording an
  evidence-backed disposition for the stale Codex safety-profile thread. The
  only residual risk was medium stale-review freshness under the configured
  `dev` quorum policy; no unresolved threads, pending checks, failed checks, or
  actionable bot findings remained at that head.

## Implementation PR State

- Implementation branch pushed to
  `origin/codex/implement-evidence-export-navigation-polish`.
- Ready implementation PR opened to `dev`:
  https://github.com/joefeser/tracemap/pull/295.
- PR loop pending.

## Follow-Ups For Implementation

- Vault safe-name/collision hardening remains open.
- Then add route-flow/property-flow navigation entries when compatible report
  inputs are present.
- Then add stable section anchors and richer RAG retrieval boundaries if still
  useful after this docs-export navigation slice.
