# Implementation State

Status: implemented
Readiness: implemented
Last verified: 2026-06-26
Branch: codex/spec-site-route-flow-evidence-story-20260626002412
Worktree: dedicated temporary worktree; absolute path omitted from spec packet
Base: origin/dev
Target base: dev
Public claim level: concept

## Summary

This packet now includes a site implementation for a public-safe
`tracemap.tools` route-flow evidence story page. It does not implement
route-flow product behavior, scanner behavior, reducer behavior, generated
artifacts, or core route-flow changes.

The future page explains how a reader should inspect route-centered static
evidence from endpoint/root selection to selected static flow rows, service
or data context, dependency/value-origin rows, gaps, limitations, and owner
handoff. It stays concept-level unless a later implementation cites
current-branch public-safe evidence for narrower row-level statements.

## Bookkeeping Normalization

Current `origin/dev` evidence on 2026-06-26 proves the route-flow evidence
story site implementation is already present, so the stale task header and two
remaining non-applicable checklist items have been normalized to implemented
state.

Evidence checked in on `origin/dev`:

- Source route: `site/src/proof-paths/route-flow/index.html`.
- Focused validator: `site/scripts/route-flow-evidence-story.mjs`.
- Focused tests: `site/scripts/route-flow-evidence-story.test.mjs`.
- Aggregate validation wiring: `site/scripts/validate.mjs` imports and runs
  `validateRouteFlowEvidenceStoryDist`.
- Route metadata: `site/src/_site/pages.json` contains
  `/proof-paths/route-flow/`.
- Discovery metadata: `site/src/_site/discovery.json` contains
  `/proof-paths/route-flow/`.
- Inbound route evidence: `site/src/proof-paths/index.html` links to
  `/proof-paths/route-flow/`.
- Prior implementation validation and PR-loop findings are recorded below.
  The implementation commits are present in current `origin/dev`, including
  the PR #355 site-spec base and later route-flow story implementation
  history.

## Scope Decisions

- Scope is site-only implementation.
- Target base is `dev`.
- Public claim level remains `concept`.
- Implementation branch is
  `codex/impl-site-route-flow-evidence-story-20260626010602`.
- Worktree is a dedicated temporary worktree; absolute path omitted from this
  spec packet.
- Selected placement: `/proof-paths/route-flow/`.
- The route visibly includes `Public claim level: concept`.
- The route visibly includes `No public conclusion without evidence`.
- The route is not added to primary navigation.
- Standalone route metadata, sitemap metadata, Open Graph metadata, discovery
  metadata, and route validation use concept-level wording.
- Current route-flow specs, tests, rule catalog entries, implementation-state
  notes, and public site routes were audited before writing stronger
  route-flow statements. Public copy makes only concept-level reading-model
  claims unless checked-in public-safe code/rule/test evidence supports the
  narrower vocabulary statement.
- In-progress route-flow attachment precision must be labeled partial,
  in-progress, future-only, illustrative, or limited to cited sub-slices unless
  current-branch evidence proves the broader statement.
- Review artifact paths recorded in this file, including `.tmp/kiro-reviews/`
  references, are internal spec-packet material and must not be quoted or
  linked in public site copy, metadata, or discovery surfaces.

Rejected placement alternatives:

- `/route-flow/`: rejected because route-flow should not be treated as a
  first-class public concept page or public demo result before a later
  information-architecture review.
- Section on `/proof-paths/`: rejected because the required route-flow row,
  stop-condition, safe-wording, and adjacent-surface vocabulary is too large
  for a compact overview section.
- Section on `/evidence/`: rejected because the page is a route-centered proof
  path story, not only a field vocabulary reference.
- Section on `/capabilities/`: rejected because concept-level route-flow copy
  near capability rows could imply shipped public capability breadth.

Adjacent route decisions:

- `/proof-paths/`: present; linked as the broader proof-path overview and now
  links back to `/proof-paths/route-flow/`.
- `/proof-paths/tour/`: present; linked as the guided reading flow.
- `/evidence/`: present; linked as the broader evidence vocabulary surface.
- `/limitations/`: present; linked as the canonical boundary and non-claim
  surface.
- `/static-vs-runtime/`: present; linked as the static-versus-runtime boundary.
- `/review-claim-checklist/`: present; linked as the repeat, downgrade, hold,
  keep-internal, or do-not-repeat ritual.
- `/glossary/`: present; linked as the canonical term index.
- Additional public-safe adjacent links used: `/proof-path-stories/`,
  `/review-room/`, `/capabilities/`, and `/demo/evidence-trail/`.
- Missing/deferred links: none for the required adjacent routes.

## Current Route-Flow Alignment Notes

Reviewed the route-flow core spec state on the spec base:

- `.kiro/specs/route-flow-service-data-composition-final/requirements.md`
  defines route-flow service/data composition as deterministic static evidence
  and rejects runtime, production, release, outage, business-impact, and
  AI/LLM claims.
- `.kiro/specs/route-flow-service-data-composition-final/design.md` describes
  the selected endpoint/root to service/data/dependency/value-origin story and
  the required static classifications, gaps, supporting IDs, limitations, and
  non-goals.
- `.kiro/specs/route-flow-service-data-composition-final/tasks.md` records
  that route-flow has shipped and in-progress slices. Task 7 attachment
  precision is still not globally complete on the spec base; several
  sub-slices are recorded as covered, while broader taxonomy remains open.
- `.kiro/specs/route-flow-service-data-composition-final/implementation-state.md`
  records recent route-flow task slices and validation evidence, but the public
  site story must not cite private branch names, private worktree paths, raw
  generated outputs, or private sample material.

Current-branch evidence statements:

- Backed by current checked-in public-safe evidence: route-flow report names,
  route-flow JSON/Markdown report shape, route-flow rule families, static
  route-flow classification names, selected row/context/gap vocabulary, and
  route-flow redaction/report-envelope concepts are present in
  `CombinedRouteFlowReport`, `rules/rule-catalog.yml`, `CombinedRouteFlowTests`,
  and route-flow implementation-state notes.
- Backed only as in-progress or sub-slice evidence: attachment precision for
  service/data/query/dependency/value-origin families. Public copy labels this
  evidence-conditioned and does not state broad completion.
- Concept-level or illustrative only: safe wording examples, route-flow row
  examples, and review outcomes on the public page.
- Deferred/private-only: real route values, local generated outputs, raw scan
  internals, private repository labels, private sample names, raw source, raw
  SQL/config, raw remotes, command output, and hidden validation detail.

## Review Results

Opus spec review command:

```text
node scripts/kiro-review.mjs --phase site-tracemap-tools-route-flow-evidence-story --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

Result on 2026-06-26: completed with reduced coverage because Kiro reported
denied tool access for write operations after reading all five spec files.

Artifacts:

- Prompt:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T052838-393Z-spec-claude-opus-4.8.prompt.md`
- Raw:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T052838-393Z-spec-claude-opus-4.8.raw.md`
- Clean:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T052838-393Z-spec-claude-opus-4.8.clean.md`
- Meta:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T052838-393Z-spec-claude-opus-4.8.meta.json`

Opus findings and dispositions:

- M1 (Medium): Requirement 8 validation did not enforce the Requirement 5
  availability-verb/current-branch evidence control. Disposition: patched.
  Requirement 8 now validates that `shows`, `renders`, `emits`, `preserves`,
  and `attaches` claims cite checked-in public-safe evidence or are framed as
  concept-level, illustrative, future-only, or deferred.
- M2 (Medium): Validation did not require authored illustrative diagrams,
  rows, or examples to be labeled illustrative and distinguished from real
  TraceMap findings. Disposition: patched in Requirement 8 and the design
  validation list.
- L1 (Low): Host-section anchor stability is required but not validated.
  Disposition: deferred; host-section anchor checks can be folded into the
  future metadata-reconciliation validation.
- L2 (Low): Internal spec paths/spec names are not explicitly classified as
  public-safe or non-public for public copy. Disposition: deferred; future
  implementation should treat raw internal spec paths as non-public copy and
  prefer public-safe artifact references.
- L3 (Low): Accessibility acceptance for new page content is only implicit.
  Disposition: deferred; existing site accessibility patterns and browser
  sanity checks remain the implementation gate.
- L4 (Low): No distinct spec-review task to record findings/dispositions.
  Disposition: deferred; this section records the dispositions directly.

Sonnet spec review command:

```text
node scripts/kiro-review.mjs --phase site-tracemap-tools-route-flow-evidence-story --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Result on 2026-06-26: completed with reduced coverage because Kiro reported
denied tool access for write operations after reading all five spec files.

Artifacts:

- Prompt:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053147-582Z-spec-claude-sonnet-4.6.prompt.md`
- Raw:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053147-582Z-spec-claude-sonnet-4.6.raw.md`
- Clean:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053147-582Z-spec-claude-sonnet-4.6.clean.md`
- Meta:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053147-582Z-spec-claude-sonnet-4.6.meta.json`

Sonnet findings and dispositions:

- M1 (Medium): `tasks.md` had no explicit task to record Sonnet findings and
  dispositions before marking the readiness gate complete. Disposition:
  patched with a spec-review task requiring Sonnet disposition recording.
- M2 (Medium): Review artifact paths under `.tmp/kiro-reviews/` were recorded
  in this implementation-state file without explicitly classifying those
  generated artifact paths as internal-only and non-public-copy. Disposition:
  patched in Scope Decisions and Requirement 7 metadata/private-material
  requirements.
- L1 (Low): `git diff --check` and diff-scope validation remained pending.
  Disposition: no spec edit; tracked in validation tasks and the Validation
  section below.
- L2 (Low): Unsafe example sentences needed explicit callout-framing
  instruction. Disposition: patched in the design unsafe-copy section.
- L3 (Low): Requirement 4 stop states omitted unrecognized or unlisted
  classification labels. Disposition: patched in Requirement 4.
- L4 (Low): Requirement 5 used `future requirement`, which was less precise
  than the Requirement 8 vocabulary. Disposition: patched to
  concept-level, illustrative, future-only, or deferred.

Bounded Sonnet re-review command:

```text
node scripts/kiro-review.mjs --phase site-tracemap-tools-route-flow-evidence-story --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Result on 2026-06-26: completed with reduced coverage because Kiro reported
denied tool access for write operations after reading all five spec files.

Artifacts:

- Prompt:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053444-416Z-spec-claude-sonnet-4.6.prompt.md`
- Raw:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053444-416Z-spec-claude-sonnet-4.6.raw.md`
- Clean:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053444-416Z-spec-claude-sonnet-4.6.clean.md`
- Meta:
  `.tmp/kiro-reviews/site-tracemap-tools-route-flow-evidence-story/2026-06-26T053444-416Z-spec-claude-sonnet-4.6.meta.json`

Bounded re-review findings and dispositions:

- M1 (Medium): `tasks.md` had marked the patched-and-re-reviewed task complete
  before the re-review artifact and outcome were recorded in this file.
  Disposition: patched. The task now explicitly requires recording the
  re-review outcome before it can be marked complete, and this section records
  the re-review outcome.
- M2 (Medium): The readiness gate task was generic enough that prior Opus and
  Sonnet dispositions alone could appear to satisfy it without the bounded
  re-review being recorded. Disposition: patched. The readiness gate now
  requires the bounded re-review record, local validation, and patched or
  dispositioned Medium or higher findings.
- L1 (Low): Validation checks were pending without saying whether they gate
  readiness or merge. Disposition: patched in the Validation section below.
- L2 (Low): Requirement 3's `no additional limitation for this row` escape
  hatch is under-specified. Disposition: deferred as acceptable at concept
  stage because Requirement 3 still requires every public row to carry a
  limitation or a documented closed-set equivalent.
- L3 (Low): Design unsafe-copy callout framing is not cross-referenced to
  Requirement 6. Disposition: deferred because Requirement 6 is authoritative
  and the design already carries the rendered-page framing rule.

Readiness decision:

- Medium or higher Opus findings: patched.
- Medium or higher Sonnet findings: patched.
- Medium or higher bounded re-review findings: patched.
- Remaining Low findings: either patched, already tracked by validation, or
  deferred with rationale above.
- Readiness advanced to `ready-for-implementation` after local validation
  passed on 2026-06-26.
- PR-loop review findings on PR #355: patched. Qodo found a stale readiness
  sentence that still said readiness remained `spec-review`; Codex found that
  Requirement 8 validation omitted several forbidden overclaim categories from
  the claim-boundary list. The patch updated the readiness sentence and added
  runtime dependency-injection target selection, branch feasibility, SQL
  execution, database state, and data contents to validation coverage.

Required review commands:

```text
node scripts/kiro-review.mjs --phase site-tracemap-tools-route-flow-evidence-story --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-route-flow-evidence-story --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Readiness has advanced to `ready-for-implementation` because the required
reviews and bounded re-review are recorded above, Medium or higher findings
are patched or explicitly dispositioned, and spec validation passed.

## Validation

Spec validation run on 2026-06-26:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Diff scope: passed; `git status --short` showed only
  `.kiro/specs/site-tracemap-tools-route-flow-evidence-story/` changed.

Site implementation validation status:

- `npm test` from `site/`: passed.
- `npm run validate` from `site/`: passed; generated site validation included
  the new route, discovery metadata, sitemap entry, internal links, focused
  route-flow evidence-story guard, and existing aggregate site guards.
- `npm run build` from `site/`: passed.
- `git diff --check`: passed after implementation edits.
- `./scripts/check-private-paths.sh`: passed after implementation edits.
- Browser sanity checks: passed against the generated site output served from a
  temporary local static server. Desktop viewport `1280x900` loaded
  `/proof-paths/route-flow/`, confirmed title, route path, required anchors,
  23 internal main links, and no horizontal overflow. Mobile viewport
  `390x844` loaded the same route, confirmed concept label, required anchors,
  card width within viewport, and no horizontal overflow.
- ACK patch validation after PR review findings: `npm test` from `site/`
  passed; `npm run validate` followed by `npm run build` from `site/` passed;
  `git diff --check` passed; `./scripts/check-private-paths.sh` passed.

## Future Implementation Notes

- The implemented page is standalone at `/proof-paths/route-flow/`.
- Metadata and discovery use concept-level wording.
- Host-route metadata reconciliation is not applicable because the selected
  placement is standalone.
- Focused validation has been added through
  `site/scripts/route-flow-evidence-story.mjs` and
  `site/scripts/route-flow-evidence-story.test.mjs`.

## Oddities

- This site packet intentionally references route-flow core concepts without
  making a shipped site claim.
- The route-flow core implementation state contains active in-progress work;
  future site copy must not flatten those partial states into complete
  coverage.
- The spec-local implementation-state note was updated after the first site
  source patch rather than before site code changed, so the corresponding
  task remains unchecked.

## Review Loop Notes

- Initial ACK on PR #359 returned `actionable_findings` with three unresolved
  review threads. Qodo flagged the public `impacted` token in rejected-pattern
  copy and a boundary-section bypass in the focused validator. Codex flagged
  that root-aware validation did not fail when this spec-local
  `implementation-state.md` file was missing.
- Patch response removed the public `impacted` token by changing the visible
  rejected pattern to unqualified impact wording, constrained
  `data-tm-boundary` stripping to exact route-flow boundary sections, added a
  focused unsupported-boundary test, and made missing implementation-state
  fail when validation is given a repo/site root.

## Follow-Ups

- Commit, push, open a ready PR into `dev`, and run ACK without merging.
