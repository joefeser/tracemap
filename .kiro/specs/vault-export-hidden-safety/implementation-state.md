# Vault Export Hidden Safety Implementation State

Status: review-blocked
Branch: codex/implement-vault-export-hidden-safety
Issue: #171
Public claim level: hidden

## Scope Decisions

- This branch implements the hidden/local vault export safety fix for issue
  #171. Site files and site specs remain out of scope.
- Public-safe and demo-safe validation remain strict. Hidden/local exports get
  deterministic context-aware handling for safe repo-relative evidence
  locations that contain sensitive words.
- Hard-fail categories remain hard failures in every mode: raw credentials,
  connection strings, local absolute paths, raw remotes, raw URLs, raw SQL,
  source snippets, captured credentials, private sample identifiers, and
  production data.
- The exporter now renders evidence locations in Markdown notes and validates
  generated Markdown plus `graph.json` through a claim-level/context-aware
  classifier before checking output collisions or writing files.
- The new hidden safe-context limitation reuses the `vault-export.*.v1`
  namespace and is documented in `rules/rule-catalog.yml`.
- No LLMs, embeddings, vector databases, browser execution, runtime proof, or
  prompt classification are allowed.
- No site files or site specs are in scope.

## Implementation Notes

- Product code changed in `src/dotnet/TraceMap.Reporting/VaultExport.cs`.
- Focused tests changed in `src/dotnet/tests/TraceMap.Tests/VaultExportTests.cs`.
- Documentation changed in `docs/VAULT_EXPORT.md`.
- Rule catalog changed in `rules/rule-catalog.yml`.
- Hidden safe-context values are allowed only for normalized safe
  repo-relative evidence-location file paths. Public/demo output continues to
  reject the same sensitive-word values.
- Raw unsafe categories reject with sanitized `UnsafeValueRejected` diagnostics
  that include category and output location only.
- Combined-index fixtures may sanitize some absolute source path forms before
  vault export sees them; focused vault tests cover the hard-fail variants that
  reach the exporter through checked-in synthetic evidence.

## Review State

Initial spec files drafted for Kiro Opus and Sonnet review:

- `.kiro/specs/vault-export-hidden-safety/requirements.md`
- `.kiro/specs/vault-export-hidden-safety/design.md`
- `.kiro/specs/vault-export-hidden-safety/tasks.md`
- `.kiro/specs/vault-export-hidden-safety/implementation-state.md`

Review commands to run for spec delivery:

```bash
node scripts/kiro-review.mjs --phase vault-export-hidden-safety --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase vault-export-hidden-safety --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000
```

Review outcomes:

- Opus spec review completed with reduced coverage because Kiro reported denied
  shell access after reading files. Blocking finding patched: the design now
  requires final generated-output validation to be claim-level and
  context-aware, and it requires safe rendered category labels so the
  exporter's own safety labels do not self-trigger validation. Important
  findings patched: stronger hard-fail detector scope, explicit rule-ID reuse
  preference for existing vault-export validation/symbol rules, SQL action-word
  display-name handling, normative hidden-mode default representation, and
  focused tests for validator integration, label safety, hard-fail
  subcategories, partial marking, and gap distinction.
- Sonnet spec review completed with full coverage. Blocking findings patched:
  explicit `StableTraceMapId` classifier outcome, closed-vocabulary
  pre-validation before emission, `--force` as stale-file replacement only
  after new content passes safety validation, deterministic display-name text
  rules instead of subjective log-like wording, and SQL action-word tests for
  repo-relative paths. Medium findings patched: hash limitation wording, gap
  sort order, expanded hard-fail fixture guidance, claim-level filtering before
  hidden/local safety transforms, and rule-catalog timing cross-reference.
- Sonnet re-review completed with full coverage and found one remaining
  important stable-ID wording ambiguity plus polish suggestions. Patched:
  semantic-context validation before stable ID construction, recommended hash
  truncation range, category/category-label terminology, and clearer criteria
  for adding new gap rule IDs.
- Final allowed Sonnet re-review completed with full coverage and found one
  remaining stable-ID construction sequence issue plus non-blocking polish. The
  spec was patched after that review to define component validation before ID
  construction, node/edge omission plus safety gap on rejected ID components,
  sanitized diagnostic format, rule reuse priority criteria, exact recommended
  hash lengths, and additional tests for ID-component rejection, empty display
  names, normalization stability, claim-level transitions, and gap
  determinism. No third Kiro re-review was run because the requested maximum was
  two re-review cycles.
- PR review loop found four unresolved actionable threads from Gemini and
  Codex. Patched after initial PR: hidden fallback now excludes every hard-fail
  category, requirement/design category labels are aligned, the symbol display
  section no longer repeats path/evidence-location behavior, and tasks now
  explicitly include stable ID construction/transformation from validated
  components.

Patch Medium+ actionable review findings, with at most two re-review cycles.

Implementation review:

- Sonnet implementation review completed with full coverage. It reported broad
  spec-completeness blockers around full classifier outcomes, stable-ID
  component omission, display-name transforms, closed-vocabulary prevalidation,
  and missing tests for omitted-ID/display-name edge cases.
- Patched in this branch after review: safe diagnostic category vocabulary,
  closed-vocabulary prevalidation, explicit hidden-safe hash/display bounds,
  bounded display-name normalization with hashed fallback labels, and
  evidence-location safety gap key hashing using the documented 32-hex
  truncation length.
- Remaining review scope not yet implemented in this branch: node/edge omission
  for rejected stable-ID components and partial marking for safety omissions
  that remove graph elements. This branch does not currently omit graph
  elements for the issue #171 safe evidence-location path; it preserves
  validated local navigation evidence and records a hidden safe-context
  limitation gap.
- Sonnet implementation re-review completed with reduced coverage because Kiro
  reported denied shell access. It still returned `NOT READY TO MERGE` with
  blocking findings for complete classifier outcomes, display-name context
  transforms, stable-ID component validation/omission, fuller context
  resolution, and a rule-reuse decision. No second re-review cycle has been run.

## Validation State

Completed:

```bash
dotnet test src/dotnet/TraceMap.sln --filter VaultExport
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

## Follow-Ups

- Consider a later shared safety helper across vault export and evidence-pack
  validation if another exporter needs the same context model.
- Optional future work may transform more source-derived display-name contexts
  to category/hash fields, but this branch keeps the issue #171 fix scoped to
  evidence locations and generated-output validation.
