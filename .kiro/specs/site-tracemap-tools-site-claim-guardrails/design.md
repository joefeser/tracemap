# Site TraceMap Tools Site Claim Guardrails Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Overview

This design describes a future concept-level guardrails surface for
`tracemap.tools` site copy. The page or section should help contributors,
agents, reviewers, and maintainers keep public wording attached to
deterministic static evidence, documented limitations, and public-safe proof
paths.

The surface is a copy-governance rulebook. It does not create a new scanner
capability, reducer capability, runtime proof, release approval workflow,
operational safety claim, AI/LLM analysis feature, or replacement for human
review.

## Placement

Candidate placements:

- `/site-claim-guardrails/`: recommended default for a public standalone
  governance page. It gives the rules enough space without crowding the claim
  checklist.
- `/docs/site-claim-guardrails/`: allowed when the site has a docs route
  family and the content should sit with contributor-facing documentation.
- Section on `/review-claim-checklist/`: allowed only if the guardrails can
  fit without crowding the canonical claim-review ritual.
- Contributor-facing docs page linked from `/docs/`: allowed as public-facing
  concept guidance because `/docs/` is public in this repository. A strictly
  hidden contributor-only page must not be linked from public `/docs/` and
  must be excluded from public sitemap, discovery output, and navigation.

Public-facing implementations should use `Public claim level: concept` because
the surface explains claim discipline rather than proving product behavior. If
the implementation is public and standalone, use site metadata patterns for
title, description, canonical URL, sitemap metadata, discovery metadata, and
Open Graph fields. If folded into another page, scope the visible claim-level
copy to the guardrails section and record metadata reconciliation in
`implementation-state.md`. If any required anchor would collide with host
content, prefix the folded-section anchors with `site-claim-guardrails-` and
record the scoped anchor map in `implementation-state.md`.

## Content Structure

1. Intro: name the guardrails, show `Public claim level: concept` for public
   output, and state `No public conclusion without evidence`.
2. Public claim levels: define `shipped`, `demo`, `concept`, and `hidden`.
3. Proof-path requirements: list the fields required before public wording can
   be shown or strengthened.
4. Allowed evidence references: describe public-safe evidence categories and
   how to cite them without exposing raw material.
5. Forbidden raw material: list raw and private material categories that must
   not appear in public output.
6. Non-claim patterns: provide bounded ways to say what TraceMap does not
   establish.
7. Downgrade and hidden rules: present the required guardrail table.
8. Validation expectations: describe future route, link, metadata, forbidden
   wording, private/raw material, word-count, and browser-sanity validation.
9. Review handoff: define bounded next states (`repeat with proof`,
   `downgrade before repeating`, `owner follow-up needed`, `do not repeat`,
   `internal only`, and `hidden`) and owner handoff language.

Required anchors:

- `#public-claim-levels`
- `#proof-path-requirements`
- `#allowed-evidence-references`
- `#forbidden-raw-material`
- `#non-claim-patterns`
- `#downgrade-and-hidden-rules`
- `#validation-expectations`
- `#review-handoff`

## Guardrail Table

The implemented table must carry all six required fields as columns or
machine-checkable sub-rows for every scenario: condition, allowed public
wording or action, required proof path, downgrade or hidden trigger, forbidden
implication, and review handoff.

The required table should include these row scenarios:

| Row | Condition | Allowed public wording or action | Required proof path | Downgrade or hidden trigger | Forbidden implication | Review handoff |
| --- | --- | --- | --- | --- | --- | --- |
| shipped | Main-true behavior or source-document guidance exists. | Repeat only with proof and limitations. | Public-safe source, rule or rule family, coverage label, and limitation. | Missing proof field or reduced evidence. | Runtime, release, safety, complete coverage, or replacement of review. | Ask owner for missing public-safe proof or downgrade. |
| demo | Checked-in public demo proof or public-safe demo summary exists. | Repeat as demo with proof. | Public demo proof, evidence boundary, coverage label, and limitation. | Demo proof is absent, private, raw, or stale. | Shipped, production, runtime, or release wording. | Ask owner for public demo summary or downgrade. |
| concept | Future-facing, guidance, dev-only, or not-yet-backed wording. | Keep concept wording. | Public-safe concept source and limitation. | Wording implies shipped or demo-backed behavior. | Public availability, operational readiness, or release timing. | Rewrite to concept or hide if details are not public-safe. |
| hidden | Details are private, unreleased, or unsafe to disclose. | Omit or abstract. | None public; use hidden rationale internally. | Any public detail would expose private material. | Hidden names, counts, cadence, sequencing, or in-flight status. | Keep hidden or request a public-safe aggregate. |
| raw artifact reference | A claim cites raw or private material. | Replace with a public-safe summary. | Public-safe summary or owner-approved public artifact. | Only raw/private proof exists. | Direct raw facts, SQLite, logs, snippets, SQL, config, paths, remotes, command output, or hidden validation details. | Route to owner follow-up or internal only. |
| dev-only feature | Evidence exists only off public/main surfaces. | Treat as concept or hidden. | Public-safe dev/context note if publishable. | Wording implies availability. | Shipped, demo, timing, or production use. | Downgrade or hide until public proof exists. |
| reduced coverage | Evidence is partial, reduced, unknown, unavailable, or gap-labeled. | Keep reduced coverage visible. | Public-safe artifact with coverage label and limitation. | Claim depends on missing coverage. | Clean, complete, safe, or no-impact wording. | Narrow the claim or request owner follow-up. |
| runtime/release wording | Copy implies runtime behavior or release approval. | Remove or rewrite. | Not supported by TraceMap static evidence. | Any runtime or release assertion appears. | Production traffic, endpoint performance, outage cause, release safety, or operational safety. | Stop publication until rewritten. |
| AI/LLM wording | Copy says core scanner/reducer uses AI/LLM analysis. | Remove or rewrite. | Deterministic static evidence only. | AI/LLM impact-analysis wording appears. | Embeddings, vector search, prompt classification, or AI impact analysis. | Rewrite to deterministic static evidence or remove. |
| private-only support | Support exists only in private or local evidence. | Internal only, hidden, or owner follow-up. | Public-safe summary required before publication. | No public-safe summary exists. | Public proof, capability, availability, or customer-specific implication. | Ask owner for public-safe summary or keep internal. |

Row copy should use synthetic, already public-safe, or category-level examples
only.

## Neighbor Route Relationship

- `/review-claim-checklist/`: canonical ritual for checking a specific claim.
  The guardrails explain the site copy rules that make a checklist outcome
  publishable or not.
- `/proof-source-catalog/`: route-to-source evidence mapping. The guardrails
  explain which categories are safe to reference and which must be hidden.
- `/roadmap/`: status and maturity gates. The guardrails must not add release
  timing, cadence, sequencing, or roadmap promises.
- `/limitations/`: broader limitation reference. The guardrails explain when a
  limitation forces downgrade, hiding, or owner follow-up.
- `/questions/objections/`: stakeholder Q&A. The guardrails provide authoring
  rules instead of objection responses.
- `/language/change-risk/`: wording patterns for static evidence. The
  guardrails define claim-level and raw-material publication boundaries.

Future implementation should link to present adjacent routes with bounded
anchor text. Missing routes, substitutions, and deferred links must be
recorded in `implementation-state.md`.

## Public Safety

The surface must not publish raw facts, raw SQLite databases, analyzer logs,
source snippets, raw SQL, config values, secrets, local paths, remotes,
generated scan directories, private sample names, command output, hidden
validation details, or credential-like values.

Forbidden categories may appear only inside explicit boundary, rejected
example, validation, or non-claim contexts. Examples must not include real
private sample names, hidden capability names, private repository identities,
customer context, internal routes, counts, cadence, sequencing, source
snippets, SQL, config values, command output, or credential-like values.

The surface must avoid blame language. It should describe missing evidence as
a gap, limitation, reduced-coverage state, private-only state, hidden state,
or owner handoff.

## Validation Design

Future implementation should add focused validation that checks:

- route or section output exists;
- visible `Public claim level: concept` for public-facing output;
- visible `No public conclusion without evidence`;
- recorded hidden contributor-only rationale when hidden placement is chosen;
- all required sections and stable anchors;
- all required guardrail rows;
- row fields for condition, allowed wording or action, required proof path,
  downgrade or hidden trigger, forbidden implication, and review handoff;
- stable machine-distinguishable markers for rejected-example, limitation,
  boundary, and non-claim zones that may contain otherwise-forbidden wording
  or forbidden-material category names;
- adjacent links resolve when present and substitutions or deferrals are
  recorded;
- standalone public route metadata, discovery metadata, sitemap metadata,
  canonical URL, title, description, and Open Graph fields remain
  concept-level;
- contributor-only hidden output is absent from public sitemap and discovery
  metadata;
- hidden contributor-only output is not linked from public `/docs/`;
- the page or section is absent from primary navigation, or an
  information-architecture note justifying inclusion is recorded in
  `implementation-state.md`;
- forbidden product capability, runtime proof, release approval, release
  safety, operational safety, complete coverage, AI/LLM analysis, and
  replacement-of-human-review claims are absent outside explicit rejected,
  boundary, limitation, or non-claim contexts;
- forbidden private/raw material is absent across rendered text, decoded HTML
  attributes, raw HTML, metadata, sitemap or discovery output, examples,
  fixtures, tests, validation messages, and review-packet references;
- no blame language appears;
- rendered body word count stays between 700 and 2200 words, measured on the
  guardrails section subtree when the implementation is folded into a host
  page;
- desktop and mobile browser sanity checks pass when layout or interaction
  changes.

Validation should be wired into the aggregate site validation workflow where
the existing site pattern supports it. Future implementation should run
`npm test`, `npm run validate`, and `npm run build` from `site/`, then
`git diff --check` and `./scripts/check-private-paths.sh` from the repository
root.
