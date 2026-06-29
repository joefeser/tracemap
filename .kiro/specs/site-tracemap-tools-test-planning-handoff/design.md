# Site TraceMap Tools Test Planning Handoff Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This design describes a future public-site concept surface that helps readers
turn TraceMap static evidence into test-planning questions. The goal is to make
proof paths and limitations useful in conversations with test owners while
preserving TraceMap's deterministic evidence boundary.

The implementation is public-site only. It must not add scanner behavior,
reducer behavior, generated tests, test execution, runtime monitoring,
production traffic analysis, endpoint performance analysis, release approval,
QA replacement, AI impact analysis, LLM analysis, or prompt-based
classification.

## Placement Options

The implementation should choose one of these placements after inspecting the
live neighboring routes:

- Standalone route: `/test-planning/`
- Nested route: `/reviewer-quickstart/test-planning/`
- Embedded section: `/reviewer-quickstart/`
- Embedded section: `/packets/assembly/`

The default preference is a standalone `/test-planning/` route if it can be
made discoverable without duplicating reviewer quickstart or packet assembly
content. Use `/reviewer-quickstart/test-planning/` if the content reads like a
reviewer sub-workflow. Embed the section only if the implementation finds that
the content is short enough and strongly dependent on the host page.
Completeness of the required sections, field labels, neighbor distinctions,
and non-claims takes priority over fitting an embedded-section word-count
ceiling; if the complete surface does not fit cleanly, choose a standalone or
nested route.

The selected placement and rationale must be recorded in
`implementation-state.md` before site source changes begin.

## Content Structure

1. Header: name the test-planning handoff surface, show `Public claim level:
   concept`, and state `No public conclusion without evidence`.
2. Static evidence input: explain that inputs are bounded TraceMap summaries,
   proof paths, rule IDs or rule families, evidence tiers, coverage labels,
   changed surfaces, and limitations.
3. Test-planning questions: show how evidence fields become human-owned
   questions for unit, integration, contract, regression, manual QA, telemetry,
   source-review, database, release, or service owners.
4. Coverage caveats: explain reduced analysis, syntax-only evidence,
   Tier4Unknown gaps, demo-only examples, and concept-only copy.
5. Safe handoff language: include short examples that preserve claim label,
   proof path, limitation, suggested test question, next owner, validation
   evidence, and non-claim together.
6. Stop conditions: list cases where a reader should stop short of a public
   conclusion. Stop conditions must be rendered as individually identifiable
   items, and validation should confirm all seven are present:
   - missing proof path;
   - private-only evidence;
   - reduced coverage;
   - concept-only or demo-only evidence;
   - no validation evidence;
   - uncertain owner;
   - a question requiring runtime observability.
7. Test owner handoff: map the next answer to a human owner without blame
   language.
8. Non-claims: explicitly state that TraceMap does not generate tests, prove
   runtime behavior, establish test sufficiency, approve releases, replace QA,
   replace source review, replace runtime observability or telemetry, replace
   service-owner judgment, database-owner judgment, or release-owner judgment,
   replace security or compliance review, or replace human judgment.
9. Neighbor links: send readers to the selected placement's related pages with
   bounded anchor text.

## Required Fields

The surface should render these labels exactly:

- `claim label`
- `proof path`
- `rule ID/family`
- `evidence tier`
- `coverage label`
- `changed surface`
- `limitation`
- `suggested test question`
- `next owner`
- `validation evidence`
- `non-claim`

The fields should usually appear in a compact table, checklist, or repeated
definition list. The implementation should favor scan-friendly content over
large prose blocks because the target reader is preparing a conversation, not
reading a general product page.

## Example Language Pattern

Safe handoff examples should follow this shape:

`Claim label: Static evidence connects this changed surface to this proof path.
Limitation: the evidence does not prove runtime behavior or test sufficiency.
Suggested test question: should the test owner add or review a targeted test
for this behavior? Next owner: named owner role. Validation evidence: human-run
test, review, or telemetry evidence. Non-claim: TraceMap did not generate or
run the test.`

The future page may shorten this pattern for readability, but every safe
handoff example must preserve proof path, limitation, next owner, validation
evidence, and non-claim together.

## Neighbor Route Relationship

- `/reviewer-quickstart/`: general reviewer orientation for reading TraceMap
  evidence. The test-planning handoff is narrower: it translates evidence into
  test-owner questions.
- `/packets/assembly/`: packet assembly guidance. The test-planning handoff
  uses assembled evidence as input rather than teaching packet assembly.
- `/review-claim-checklist/`: public claim checking. The test-planning handoff
  asks what humans should test or review next.
- `/validation/`: validation proof and quality signals. The test-planning
  handoff does not claim validation has happened; it asks what validation
  evidence a test owner should seek.
- `/proof-paths/tour/`: proof-path walkthrough. The test-planning handoff
  applies proof paths to test conversations.
- `/questions/objections/`: objection handling. The test-planning handoff
  gives practical language for the next test-owner question.

Cross-links should never imply generated tests, automated QA, complete
coverage, runtime proof, production traffic proof, endpoint performance proof,
release approval, or release safety.

## Public Safety

The surface and metadata must not publish raw facts, SQLite content, analyzer
logs, source snippets, SQL, config values, secrets, local paths, raw remotes,
generated scan directories, private sample names, command output, hidden
validation details, credential-like values, connection strings, tokens, keys,
or private repository identifiers.

The page may mention artifact families and validation evidence categories only
as public-safe concepts. It must not expose raw scanner output or private
validation material.

## Validation Design

The future implementation should add a focused validator or extend an existing
site validator using neighboring page patterns. Validation should check:

- selected placement renders;
- required claim-level label and shared principle render;
- required section labels render;
- required field labels render exactly;
- all required neighbor-distinction statements render, one for each neighbor
  listed in Requirement 2: `/reviewer-quickstart/`, `/packets/assembly/`,
  `/review-claim-checklist/`, `/validation/`, `/proof-paths/tour/`, and
  `/questions/objections/`; links alone do not satisfy the distinction
  requirement;
- all seven stop conditions render as individually identifiable items: missing
  proof path, private-only evidence, reduced coverage, concept-only or
  demo-only evidence, no validation evidence, uncertain owner, and a question
  requiring runtime observability;
- required links exist and resolve;
- standalone route metadata includes title, description, canonical URL, Open
  Graph fields, concept claim level, sitemap entry, and discovery metadata;
- embedded section metadata keeps the section discoverable through the host
  route's existing metadata shape;
- rendered copy does not contain forbidden generated-test, test-sufficiency,
  runtime-proof, production-traffic, endpoint-performance, release-safety,
  release-approval, complete-coverage, AI/LLM, or QA-replacement claims;
- rendered text, decoded attributes, public metadata, and raw HTML do not
  expose private/raw material;
- visible-body word count is within the selected placement bounds;
- desktop and mobile browser sanity checks show no text overlap, horizontal
  overflow, broken layout, inaccessible link targets, or basic accessibility
  regressions in heading order, descriptive link text, alt text, or color
  contrast.

Validation should treat boundary statements as allowed only when they are
clearly non-claims. The validator must use phrase-scoped denylist patterns for
unsupported positive claims, while allowing boundary vocabulary such as
`public-safe`, `safe handoff language`, and `safe to share`, and allowing the
required non-claim section to state what TraceMap does not do.

## Implementation Notes

Implementation should be site-only and should reuse existing layout,
navigation, route metadata, sitemap, discovery, and validation patterns. If the
future implementation chooses a standalone route, add it to site discovery and
sitemap outputs. If it chooses an embedded section, avoid top-navigation churn
unless a separate navigation spec requires it.

The implementation should update this spec's `tasks.md` checkboxes and
`implementation-state.md` as work lands.

During spec review, run `git diff --check` and
`./scripts/check-private-paths.sh` on the spec files themselves to confirm no
private material leaked into the spec directory.
