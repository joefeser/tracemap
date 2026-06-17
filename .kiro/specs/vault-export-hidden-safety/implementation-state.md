# Vault Export Hidden Safety Implementation State

Status: spec-drafted
Branch: codex/spec-vault-export-hidden-safety
Issue: #171
Public claim level: hidden

## Scope Decisions

- This branch is spec-only. It does not implement product code, tests, docs, or
  rule catalog changes outside `.kiro/specs/vault-export-hidden-safety/`.
- The spec treats this as a corrective follow-up to the existing
  `evidence-graph-vault-export` work.
- Public-safe and demo-safe validation remain strict. Hidden/local exports get
  deterministic context-aware handling only for safe contexts.
- Hard-fail categories remain hard failures in every mode: raw secrets,
  connection strings, local absolute paths, raw remotes, raw URLs, raw SQL,
  source snippets, captured credentials, private sample identifiers, and
  production data.
- No LLMs, embeddings, vector databases, browser execution, runtime proof, or
  prompt classification are allowed.
- No site files or site specs are in scope.

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

## Validation Plan For Spec PR

Spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

If a spec validation script is added or discovered, run it before commit.

Product validation such as `dotnet test` is deferred because this branch does
not change product code.

## Follow-Ups For Implementation PR

- Update `docs/VAULT_EXPORT.md`.
- Update `rules/rule-catalog.yml` for any new `vault-export.*.v1` rule IDs.
- Add focused vault export tests for hidden/local safe secret-like names,
  public/demo strictness, raw secret rejection, local absolute path rejection,
  determinism, and generated file collisions.
- Run focused and broader validation as described in `tasks.md`.
