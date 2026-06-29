# Site TraceMap Tools Public Claim Review Drill Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This design describes a future concept-level public drill for checking whether
a claim may be repeated with its evidence boundary intact. The surface gives a
reader a small set of public-safe sample claims, asks them to inspect proof
path fields, then provides an answer key with next actions.

The drill is not an automated grader. It does not make impact, runtime,
release, safety, operational, AI/LLM, or absence-of-impact conclusions. It
teaches the reader to keep public claims attached to proof paths, rule IDs or
rule families, evidence tiers, coverage labels, limitations, and non-claims.

## Route and Placement

Candidate placements:

- `/review-claim-checklist/drill/`: recommended default. The drill is a
  practice companion to the claim checklist and should not crowd the canonical
  checklist route.
- `/claims/review-drill/`: allowed if a future implementation creates a claims
  route family.
- Section on `/review-claim-checklist/`: allowed if the drill is compact and
  the host page remains readable.
- Section on `/proof-paths/tour/`: allowed if implementation frames the drill
  as proof-path reading practice rather than claim-repeatability practice.

If implemented as a standalone route, the recommended source file is
`site/src/review-claim-checklist/drill/index.html`. The implementation should
register sitemap and discovery metadata using concept-level wording, avoid
primary navigation, and add bounded inbound links from existing checklist or
proof-path-family surfaces when present.

If implemented as a section, use stable anchors that cannot collide with the
host route, such as `claim-drill-setup`, `claim-drill-answer-key`, and
`claim-drill-non-claims`, and record host metadata reconciliation in
`implementation-state.md`.

## Content Structure

1. Hero or intro: name the drill, show `Public claim level: concept`, and
   state `No public conclusion without evidence`.
2. Drill setup: explain that the reader is practicing evidence review by
   comparing sample claims with proof-path fields. State that this is not an
   automated grader or approval workflow.
3. Sample public-safe claims: show the seven required rows with concise sample
   claim text and row fields.
4. Evidence checklist: list the fields the reader should inspect before
   choosing an outcome.
5. Answer key: show the correct outcome and next action for every sample row.
6. Unsafe answer examples: show rejected answer shapes that overclaim runtime,
   release, safety, operational, absence-of-impact, complete-coverage, AI/LLM,
   or human-review replacement conclusions.
7. Stop conditions: name the conditions where the reader stops, downgrades,
   keeps the claim internal, or asks an owner for a public-safe proof path.
8. Non-claims: close with page-level boundaries that travel with the drill.

## Required Drill Rows

Each row must include these fields exactly or with equivalent machine-checkable
labels:

- claim text
- expected claim level
- proof path needed
- evidence fields to check
- limitation or non-claim
- correct outcome
- next action

Required row scenarios:

- Supported demo-level claim: a public-safe demo result exists or is cited by
  route, with evidence tier, coverage label, limitation, and non-claim
  preserved. Correct outcome: `repeat with proof`.
- Concept-only claim: future-facing guidance or explanatory concept. The
  sample row uses draft wording that implies shipped or demo behavior.
  Correct outcome: `downgrade before repeating`. Teaching note, not an
  alternative outcome: if a different sample row were already concept-bounded,
  it could only be repeated with proof of the concept-level source; that note
  does not change this row's correct outcome.
- Reduced-coverage claim: evidence exists but coverage is reduced, partial,
  unknown, unavailable, future-only, or gap-labeled. Correct outcome:
  `downgrade before repeating` or `owner follow-up needed`.
- Unsafe runtime claim: wording implies runtime behavior, production traffic,
  endpoint performance, or outage cause. Correct outcome: `do not repeat`.
- Unsafe release claim: wording implies release approval, release safety,
  operational safety, or replacement of release controls. Correct outcome:
  `do not repeat`.
- Private-evidence-only claim: evidence exists only in private or raw
  material. Correct outcome: `internal only` or `owner follow-up needed`.
- Missing-proof claim: no public-safe proof path, rule ID or rule family,
  evidence tier, or coverage label is available. Correct outcome:
  `do not repeat` or `owner follow-up needed`.

## Sample Row Safety

Sample rows should be authored examples, not disguised internal findings. Use
generic nouns such as `demo route`, `sample endpoint`, `review packet`, and
`public-safe summary`. Do not use real private repository names, sample names,
local paths, remotes, service names, customer names, ticket IDs, hidden
capability names, source snippets, SQL, config values, command output, or
credential-like values.

If examples use placeholder rule IDs, commit-like values, extractor versions,
or dates, label them illustrative. Prefer not to include commit-like values or
dates unless a validation need requires them.

## Neighbor Route Relationship

- `/review-claim-checklist/`: canonical checklist for deciding whether a real
  claim can be repeated, downgraded, held for owner follow-up, or kept
  internal.
- `/proof-paths/tour/`: guided walk through reading a proof path.
- `/proof-paths/faq/`: explanatory FAQ about proof-path questions and edge
  cases.
- `/questions/objections/`: stakeholder concern handling rather than a drill.
- `/packets/examples/`: concrete packet examples when available, not a
  seven-row practice set.
- `/language/change-risk/`: wording and change-risk language guidance rather
  than proof-path support review.

Cross-links should use role-specific anchor text and must not imply TraceMap
proves runtime behavior, production traffic, endpoint performance, release
safety, operational safety, absence of impact, complete coverage, or automated
review replacement.

## Public Safety

The page and metadata must not publish raw fact streams, raw SQLite databases,
analyzer logs, source snippets, raw SQL, config values, secrets, local paths,
raw remotes, generated scan directories, private sample names, command output,
hidden validation details, credential-like values, or local-only evidence.

The page may name these categories only inside boundary copy that explains
they are not public proof. Public proof paths must route through public-safe
summaries, checked-in docs, public pages, sanctioned demo artifacts, or
review-packet surfaces.

## Validation Design

Future implementation should add a focused validator following neighboring
concept-page patterns. It should check:

- route or section output exists;
- visible `Public claim level: concept`;
- visible `No public conclusion without evidence`;
- required sections exist: drill setup, sample public-safe claims, evidence
  checklist, answer key, unsafe answer examples, stop conditions, and
  non-claims;
- exactly the seven required row scenarios are present;
- every row includes claim text, expected claim level, proof path needed,
  evidence fields to check, limitation or non-claim, correct outcome, and next
  action;
- the `evidence fields to check` value enumerates discrete fields rather than
  collapsing rule ID or rule family, evidence tier, coverage label,
  limitation, non-claim, source context, and public/private status into vague
  combined text;
- expected claim levels are only `shipped`, `demo`, `concept`, or `hidden`;
- answer-key outcomes are only `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, or
  `internal only`;
- any row whose correct outcome is `repeat with proof` exposes a discrete rule
  ID or rule family, evidence tier, and coverage label;
- each required row's correct outcome falls within that scenario's allowed
  answer-key outcomes: demo maps to `repeat with proof`; concept-only maps to
  `downgrade before repeating`; reduced coverage maps to
  `downgrade before repeating` or `owner follow-up needed`; unsafe runtime and
  unsafe release map to `do not repeat`; private-evidence-only maps to
  `internal only` or `owner follow-up needed`; and missing proof maps to
  `do not repeat` or `owner follow-up needed`;
- each required adjacent route link resolves when present, or the
  substitution/deferral is recorded in `implementation-state.md`;
- standalone route metadata, sitemap metadata, discovery metadata, canonical
  URL, title, description, and Open Graph fields remain concept-level;
- forbidden public claims are absent outside explicit unsafe, limitation,
  boundary, or non-claim regions;
- private/raw material is absent outside explicit boundary regions;
- no blame language appears in rendered copy, metadata, examples, fixtures, or
  validation messages;
- rendered body word count stays between 500 and 1800 words without removing
  mandatory rows or sections;
- desktop and mobile browser sanity checks pass when the page or section
  changes layout or interaction.

Validation should be wired into the aggregate site validation workflow. The
future implementation should run `npm test`, `npm run validate`, and
`npm run build` from `site/`, then `git diff --check` and
`./scripts/check-private-paths.sh` from the repository root.
