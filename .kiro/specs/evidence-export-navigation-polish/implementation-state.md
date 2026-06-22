# Evidence Export Navigation Polish Implementation State

Status: spec-reviewed
Readiness: ready-for-implementation
Branch: codex/spec-evidence-export-navigation-polish
PR target: dev
Primary issue: #189
Public claim level: hidden until implemented and validated

## Scope

This is a spec-only branch for the next evidence export navigation polish
slice. It creates a follow-up specification over already shipped vault and
docs-export behavior. It does not implement product code.

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
- Do not add product code, site files, generated output, rule catalog entries,
  or docs outside this spec packet in the spec PR.

## Review State

Planned review commands:

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

## Validation

Planned for this spec-only PR:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Completed:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

## Follow-Ups For Implementation

- Implement the smallest coherent product slice first: deterministic
  safe-name/collision hardening and navigation indexes.
- Then add route-flow/property-flow navigation entries when compatible report
  inputs are present.
- Then polish docs-export chunk anchors/backreferences and RAG retrieval
  boundaries.
