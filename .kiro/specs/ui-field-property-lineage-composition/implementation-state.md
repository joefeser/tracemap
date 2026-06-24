# UI Field Property Lineage Composition Implementation State

Status: ready-for-implementation
Readiness: ready-for-implementation
Spec branch: `codex/spec-ui-field-property-lineage-composition`
Target base: `dev`
Primary issue: `#165`
Public claim level: hidden

## Scope

This is a spec-only branch. It creates a reviewed Kiro spec for completing UI
field/property lineage composition from Angular and Razor/cshtml roots to
backend model/DTO/property evidence and existing route-flow/service/data context
where property-specific static evidence supports the trail.

No product code, generated outputs, site files, rule catalog entries, scanner
logic, reducer logic, or export code are changed by this branch.

## Source Material Reviewed

- GitHub issue #165.
- `.kiro/specs/ui-field-property-lineage/`
- `.kiro/specs/ui-field-property-lineage-next-slice/`
- `.kiro/specs/ui-field-property-lineage-continuation/`
- `.kiro/specs/route-flow-service-data-composition-next/`
- `.kiro/specs/route-flow-service-data-composition-final/` was present as
  untracked workspace context during review. The committed spec treats it as a
  possible successor that must be re-audited if it lands before implementation.
- `.kiro/specs/route-centered-endpoint-trace-completeness/`
- `rules/rule-catalog.yml` property-flow, Angular, Razor, and route-flow rule
  entries.
- `docs/VALIDATION.md` and `docs/ACCEPTANCE.md` property-flow/reporting notes.

## Scope Decisions

- This spec completes the composition contract; it is not another broad root
  extraction rewrite.
- Existing `tracemap property-flow` remains the command surface unless a future
  implementation review proves a versioned change is required.
- Route-flow rows can be attached only when the selected property trail reaches
  them through rule-backed property-specific evidence.
- Implementation must record which live route-flow contract it targets before
  product edits: `route-flow-service-data-composition-next`, a merged
  `route-flow-service-data-composition-final`, or another documented successor.
- Broad endpoint reachability, source proximity, same file/class, same method,
  same endpoint, or same property name is not sufficient to claim property
  lineage.
- Weak-but-present property-specific evidence becomes deterministic
  `NeedsReviewLineage`; absent, dynamic-only, incompatible, unsafe, or
  unavailable evidence becomes a deterministic gap and no hop across that
  boundary.
- Candidate new rule IDs are catalog-first only and must not be emitted until
  `rules/rule-catalog.yml` documents them.
- Safe output and export compatibility are part of the feature, including
  Markdown, JSON, vault, RAG/docs-export chunks, evidence-pack, explorer, and
  evidence graph consumers when row shapes change.
- Browser/computer-use/runtime-assisted ideas are future/out of scope for core
  scanner behavior and cannot upgrade static classifications.
- No LLM calls, embeddings, vector databases, or prompt-based classification
  belong in the scanner, reducer, report, or export implementation.

## Review Log

Planned commands:

```bash
node scripts/kiro-review.mjs --phase ui-field-property-lineage-composition --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase ui-field-property-lineage-composition --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Review results will be recorded here with artifact paths, coverage, exact
timeout/unavailable evidence if applicable, findings, and dispositions. Status
will move to `ready-for-implementation` only after Medium+ actionable findings
are patched and a final bounded re-review is recorded.

Initial review runs:

The `.tmp/kiro-reviews/...` artifact paths below are local-only review
evidence from this workstation and are not committed. The durable in-repo record
is the findings/disposition summary in this section.

- Opus spec review:
  `.tmp/kiro-reviews/ui-field-property-lineage-composition/2026-06-24T030736-494Z-spec-claude-opus-4.8.clean.md`
  completed with full wrapper coverage. Findings patched in this branch:
  route-flow contract sequencing, closed gap-code vocabulary, deterministic
  cap-versus-gap behavior, catalog-first candidate rule IDs, closed generic
  name set, and additional validation tasks for uncatalogued rules,
  gap-code closure, and compatibility consumers.
- Sonnet spec review:
  `.tmp/kiro-reviews/ui-field-property-lineage-composition/2026-06-24T030736-566Z-spec-claude-sonnet-4.6.clean.md`
  completed with full wrapper coverage. Findings patched in this branch:
  route-flow successor/schema definition, alias evidence catalog requirement,
  model/dto selector scope, candidate-rule catalog-first wording, schema
  compatibility/version note, committed audit artifact requirement, successor
  route-flow spec note, and additional tests for server-only model-binding
  roots, alias-without-rule, and older consumer compatibility.
- Workspace note: the initial review commands were run while Git reported
  branch `codex/spec-static-dispatch-candidate-bridges`, but the reviewed files
  were this spec folder and the work was moved back to
  `codex/spec-ui-field-property-lineage-composition` before final validation,
  commit, push, and PR.
- First Sonnet re-review after Medium+ patches:
  `.tmp/kiro-reviews/ui-field-property-lineage-composition/2026-06-24T031341-206Z-re-review-claude-sonnet-4.6.clean.md`
  completed with reduced coverage because Kiro reported denied shell tool
  access. It reported no remaining blockers and suggested narrow non-blocking
  cleanups, which were patched.
- Final Sonnet re-review after the narrow cleanup patch:
  `.tmp/kiro-reviews/ui-field-property-lineage-composition/2026-06-24T031508-438Z-re-review-claude-sonnet-4.6.clean.md`
  completed with reduced coverage because Kiro reported denied shell tool
  access. It reported no content blockers; remaining findings were process
  bookkeeping and validation recording.
- Branch verification before validation: final re-review metadata records
  `gitBranch` as `codex/spec-ui-field-property-lineage-composition`.

## Validation Log

Spec-only validation passed:

```bash
git diff --cached --check
./scripts/check-private-paths.sh
git diff --cached --name-only
```

Results:

- `git diff --cached --check`: passed after staging intended spec files.
- `./scripts/check-private-paths.sh`: passed.
- Staged diff scope is limited to:
  - `.kiro/specs/ui-field-property-lineage-composition/design.md`
  - `.kiro/specs/ui-field-property-lineage-composition/implementation-state.md`
  - `.kiro/specs/ui-field-property-lineage-composition/requirements.md`
  - `.kiro/specs/ui-field-property-lineage-composition/review-prompts.md`
  - `.kiro/specs/ui-field-property-lineage-composition/tasks.md`
- Dedicated spec lint/check discovery: no separate repo spec lint command was
  found. Search found `scripts/kiro-review.mjs`,
  `scripts/check-private-paths.sh`, and unrelated validation helpers; this
  spec used the Kiro review wrapper, private-path guard, and whitespace check.

## PR State

PR: https://github.com/joefeser/tracemap/pull/309

ACK PR loop:

- Initial run posted the required Codex review request and then returned
  `environment_blocked` / `GITHUB_STATE_UNAVAILABLE` because GitHub GraphQL
  returned a TLS handshake timeout.
- Rerun returned `actionable_findings` / `UNRESOLVED_REVIEW_THREADS` while
  Codex review was still pending; patching was not yet authorized by the
  required-review batch state.
- After Codex returned, ACK reported three unresolved review threads and
  authorized patching:
  - Qodo: `tasks.md` still showed "Open a ready PR into dev" unchecked.
  - Codex: dynamic Angular/Razor UI boundaries could be read as allowing a
    `NeedsReviewLineage` hop without static model identity.
  - Codex: `ObservedDemoContext` was missing from the allowed classification
    vocabulary even though observed evidence metadata remains non-upgrading.
- Patch disposition: checked the PR-open task, changed dynamic UI boundaries to
  emit a gap and no hop across that dynamic boundary, and added
  `ObservedDemoContext` for observed evidence metadata rows only with explicit
  non-upgrading semantics.

## Follow-Ups For Implementation

1. Audit current property-flow and route-flow report contracts and harden
   explicit gaps before adding broader composition rows.
2. Compose Angular and Razor/cshtml UI roots to backend properties only through
   property-specific static evidence.
3. Reuse route-flow/service/data/dependency context only as supporting context
   when the selected property trail reaches it.
4. Validate safe output for reports, vault/RAG/docs-export chunks, evidence
   graph exports, evidence-pack, and static explorer consumers when touched.
