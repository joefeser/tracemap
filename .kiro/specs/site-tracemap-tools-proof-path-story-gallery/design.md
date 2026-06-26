# Site TraceMap Tools Proof Path Story Gallery Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Overview

This design describes a future concept-level `tracemap.tools` story gallery
that makes deterministic proof paths easier to understand. The gallery should
show short public-safe cards and walkthroughs that start with a static
question, follow evidence from a source/root surface to endpoint, service,
data, package, config, artifact, or stop-condition surfaces, and keep rule
IDs, evidence tiers, coverage labels, supporting IDs, limitations, and stop
conditions visible.

The gallery is an explanation layer over deterministic static evidence. It
does not create scanner capability, reducer capability, runtime proof,
production traffic proof, performance evidence, AI/LLM impact analysis, release
approval, operational safety, or automated owner decisions.

## Placement

Candidate placements:

- `/proof-path-stories/`: recommended default for a standalone public concept
  page because the surface is broader than the public demo and can explain the
  story contract without implying demo coverage.
- `/demo/proof-path-stories/`: allowed only when every initial story is backed
  by checked-in public demo summaries and the page can safely use demo-level
  card labels without implying shipped behavior.
- Section on `/demo/proof-upgrades/`: allowed only if the gallery remains a
  compact companion to existing demo proof rows and does not crowd the
  evidence ledger.
- Section on a future proof-source/catalog route: allowed only if that route
  already exists and the gallery can remain a story-oriented interpretation
  aid rather than the catalog source of truth.

Public-facing implementations should default to `Public claim level: concept`.
If implementation proves a stricter demo-only scope, record the checked-in
public-safe generated summaries and card-level claim decisions in
`implementation-state.md`.

Standalone public routes should use the site's existing metadata, sitemap, and
discovery patterns. Folded sections must record host metadata reconciliation
and stable anchor mapping in `implementation-state.md`.

## Information Architecture

Recommended page structure:

1. Intro: name the proof-path story gallery, show `Public claim level:
   concept`, and state `No public conclusion without evidence`.
2. Story contract: list the required fields for every story card.
3. Proof path anatomy: show the ordered shape from static question to source
   surface, rule-backed steps, destination or stop surface, limitation, and
   next question.
4. Evidence packet references: explain public-safe packet/report-family
   references and the fields each reference must carry.
5. Coverage and limitations: explain semantic, structural, syntax/textual,
   unknown, mixed, partial, and reduced-coverage states.
6. Stop conditions and routing: list public-safe stop conditions and
   next-owner/next-question routes.
7. Non-claims and forbidden wording: show what the gallery cannot say.
8. Gallery validation: describe future implementation validation.

Required anchors:

- `#story-contract`
- `#proof-path-anatomy`
- `#evidence-packet-references`
- `#coverage-and-limitations`
- `#stop-conditions-and-routing`
- `#non-claims-and-forbidden-wording`
- `#gallery-validation`

## Story Card Model

Each card should be a compact public-safe evidence orientation unit with these
machine-checkable fields:

- Static question.
- Story category.
- Claim level.
- Coverage label.
- Proof path steps.
- Evidence packet references.
- Rule IDs or rule families.
- Evidence tiers.
- Supporting IDs when public-safe.
- Limitation or non-claim.
- Stop condition.
- Next owner or next question.

Proof path steps should use category labels such as `source surface`, `route
surface`, `service surface`, `data surface`, `package surface`, `config
surface`, `project surface`, `public-safe report family`, and `static evidence
stop`. They should not use raw code, raw SQL, config values, local paths,
repository remotes, private sample names, private labels, hidden capability
names, command output, generated local artifact paths, or credential-like
values.

## Walkthrough Model

Walkthroughs should be short and repeatable:

1. Ask one static question.
2. Name the starting surface with a public-safe category label.
3. Follow ordered evidence steps with rule IDs or rule-family labels.
4. Show evidence tiers and coverage labels at the point they matter.
5. Attach public-safe supporting IDs when available.
6. Stop at a destination surface or static-evidence boundary.
7. State the limitation or non-claim.
8. Route the next owner or next question.

Allowed endings are:

- `evidence-backed static path`
- `reduced coverage`
- `needs owner follow-up`
- `internal only`
- `hidden`
- `stop: no public-safe evidence`

## Story Categories

The gallery should support these categories:

- Endpoint/service orientation: static evidence between a source/root surface
  and an endpoint-shaped or service-shaped surface.
- Data/config orientation: static evidence that reaches a data, SQL, or config
  category without showing raw SQL or raw config.
- Package/dependency orientation: static package or dependency evidence
  without compatibility, vulnerability, or production dependency claims.
- Generated artifact orientation: public-safe report-family or evidence
  packet orientation without local generated paths.
- Reduced-coverage orientation: proof paths that stop because of semantic,
  build, adapter, extractor, syntax-only, or unknown-evidence gaps.

Each category needs a visible limitation and a stop-condition rule. Initial
implementations may omit categories only when the omission is recorded in
`implementation-state.md`.

## Evidence Reference Design

Evidence packet references should be public-safe handles, not raw artifact
paths. A reference should include:

- packet or report-family label;
- rule ID or rule family;
- evidence tier;
- coverage label;
- supporting ID when public-safe;
- source context such as concept, public demo, hidden, local-only, or
  future-only;
- limitation; and
- stop condition when applicable.

References should distinguish semantic, structural, syntax/textual, unknown,
mixed, partial, and reduced-coverage evidence. Reduced coverage is visible
evidence, not a clean result.

A supporting ID is a stable, opaque identifier from a generated artifact, such
as a fact ID, edge ID, or evidence row key. An ID is public-safe only when it
appears in a checked-in public demo summary or public-safe report family and
does not encode a private sample name, local path, private label, or
credential-like value. Validation should check that claimed public-safe IDs do
not contain path separators, known private label prefixes, or credential
patterns.

## Stop Conditions And Routing

Stop conditions should be explicit and public-safe:

| Stop condition | Meaning | Public-safe route |
| --- | --- | --- |
| `no public-safe evidence` | The path cannot be published with current public proof. | Do not publish until a public-safe summary exists. |
| `reduced coverage` | The scan or path is partial. | Ask the reviewer to narrow the claim or keep the coverage label visible. |
| `semantic gap` | Compiler-resolved evidence is unavailable or incomplete. | Route to reviewer or code owner for follow-up evidence. |
| `syntax-only fallback` | The path uses syntax/textual fallback. | Keep the lower tier visible and avoid stronger conclusions. |
| `private-only evidence` | Evidence exists only in private/local material. | Keep internal or request a public-safe aggregate. |
| `hidden detail` | Naming the detail would expose private or unreleased material. | Abstract or omit. |
| `missing rule ID` | A conclusion lacks a rule-backed reference. | Add a rule ID/rule family or remove the conclusion. |
| `requires reducer evidence` | Impact wording would need reducer output. | Avoid impact wording until reducer evidence is public-safe. |

Routes may use generic owners such as reviewer, code owner, product owner,
security owner, data owner, or package owner. They must not reveal real
internal owner names, private teams, customers, sample identities, branches,
remotes, or unpublished artifact labels.

## Public Safety

The gallery must avoid product claims stronger than the evidence. It must not
publish raw facts, raw SQLite contents, analyzer logs, source snippets, raw
SQL, config values, secrets, local paths, remotes, generated scan directories,
private sample names, private labels, hidden validation details, or
credential-like values.

Forbidden terms may appear only in explicit boundary, rejected-example,
limitation, validation, or non-claim contexts marked with stable
machine-distinguishable wrappers so validators can exclude them without
excluding normal body copy.

Acceptable wrapper forms include:

- a `data-boundary="rejected-example"` attribute on the containing element;
- a class name containing `boundary-example`, `rejected-example`, or
  `non-claim-context`; or
- a section whose heading anchor matches `#non-claims-and-forbidden-wording`.

Validation must exclude matches inside these wrappers when checking forbidden
wording and must flag forbidden terms outside them.

## Validation Design

Future implementation should add focused validation that checks:

- visible claim level and shared principle;
- required anchors and sections;
- required story-card fields;
- required walkthrough fields and allowed endings;
- required evidence reference fields;
- required stop conditions and public-safe owner routing;
- story categories or recorded omissions;
- standalone public metadata, discovery metadata, sitemap metadata, canonical
  URL, title, description, and Open Graph fields;
- forbidden AI/LLM, runtime, production, performance, release-safety,
  operational-safety, complete-coverage, and automated approval claims outside
  marked boundary contexts;
- forbidden private/raw material across rendered text, decoded HTML
  attributes, raw HTML, metadata, sitemap or discovery output, examples,
  fixtures, tests, validation messages, and review-packet references; and
- desktop and mobile browser sanity when layout or interaction changes.

Future implementation should run `npm test`, `npm run validate`, and
`npm run build` from `site/`, then `git diff --check` and
`./scripts/check-private-paths.sh` from the repository root.
