# Site TraceMap Tools Proof Path Story Gallery Requirements

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

Define a future `tracemap.tools` proof-path story gallery: a public-safe set of
short story cards and walkthroughs that begin with a static question and follow
deterministic evidence from a source or root surface toward endpoint, service,
data, package, config, or other exposed surfaces.

This is a spec-only site phase. It does not implement site code, scanner
behavior, reducer behavior, generated artifacts, runtime observation, public
demo content, validation scripts, or existing specs.

The future gallery is for making proof paths understandable. It is not a new
product capability claim, impact-analysis claim, runtime proof, production
traffic claim, performance claim, AI/LLM analysis feature, release approval
workflow, operational safety assertion, or replacement for human review.

## Shared Site Principle

No public conclusion without evidence.

## Claim Level Rationale

Use `Public claim level: concept` because this packet specifies a future
public-facing explanation surface. It does not rely on checked-in public-safe
generated summaries for particular stories, and it does not prove a stricter
demo-level gallery exists today.

A future implementation may keep individual cards concept-level until each
card has public-safe checked-in evidence packet references. A future card may
only be labeled `demo` when its story is backed by checked-in public demo
evidence or public-safe generated summaries, and that stricter card-level
decision must be recorded in this spec's `implementation-state.md`.

## Hard Boundaries

- Do not add AI/LLM impact-analysis claims, embeddings, vector search,
  prompt-based classification, or model-assisted conclusions.
- Do not claim runtime reachability, production traffic, endpoint performance,
  production ownership, outage cause, release approval, release safety,
  operational safety, complete coverage, or automated approval.
- Do not publish raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repository remotes, private sample names, private labels,
  generated scan directories, raw `facts.ndjson`, raw SQLite contents,
  analyzer logs, command output, hidden validation details, or
  credential-like values.
- Do not expose generated local artifacts or private artifact labels.
- Do not say TraceMap found an impact unless the wording is explicitly tied to
  reducer output, evidence, status, and limitations. The gallery should prefer
  "static path", "evidence path", "candidate handoff", and "next question"
  wording.
- Missing, reduced, private-only, or stopped evidence is a coverage label,
  limitation, stop condition, hidden state, or owner handoff, not a person or
  team failure.

## Requirements

### Requirement 1: Choose placement and public framing

The future implementation shall choose a placement for the proof-path story
gallery and preserve concept-level public framing unless stricter demo-level
evidence is proven for each card.

Acceptance criteria:

- The implementation evaluates candidate placements:
  `/proof-path-stories/`, `/demo/proof-path-stories/`, a section on
  `/demo/proof-upgrades/`, or a section on a future proof-source/catalog route.
- The implementation records the selected placement, rejected alternatives,
  and rationale in this spec's `implementation-state.md`.
- Public-facing output visibly renders `Public claim level: concept` unless
  every shown story card is backed by checked-in public-safe demo evidence and
  `implementation-state.md` records the stricter demo rationale.
- Public-facing output visibly renders `No public conclusion without evidence`.
- The page or section is not added to primary navigation unless an
  information-architecture note records why the existing site pattern supports
  that choice.
- The page or section cross-links to related public-safe surfaces when they
  exist, such as proof path index, proof source catalog, guided proof-path tour,
  evidence glossary, claim checklist, roadmap, or limitations surfaces, and
  states why this gallery is a story-oriented reading aid rather than the
  canonical proof ledger or catalog.
- A standalone public route includes concept-level title, description,
  canonical metadata, Open Graph metadata, sitemap metadata, and discovery
  metadata.
- A folded section records metadata reconciliation and gives each required
  section a stable anchor.

### Requirement 2: Define story card contract

Each future story card shall be a public-safe evidence orientation unit, not a
claim of runtime or production behavior.

Acceptance criteria:

- Every story card starts with one static question, such as "Which public-safe
  source surface leads to this endpoint-shaped surface?" or "Where does this
  package reference stop in static evidence?"
- Every story card includes a public-safe proof path with ordered steps from a
  root/source surface to one or more endpoint, service, data, package, config,
  project, generated artifact, or stop-condition surfaces.
- Every story card includes public-safe evidence packet references, rule IDs
  or rule-family labels, evidence tiers, coverage labels, supporting IDs when
  public-safe, limitations, and stop conditions.
- Every story card includes a story category drawn from the supported story
  category set.
- Every story card includes a per-card claim level. A card is labeled `demo`
  only when its story is backed by checked-in public-safe demo evidence and the
  stricter card-level decision is recorded in `implementation-state.md`;
  otherwise the card remains `concept`.
- Every story card includes a limitation or non-claim that states what the
  path does not prove.
- Every story card includes next-owner or next-question routing for the point
  where static evidence stops.
- Every story card uses synthetic labels, public demo labels, or
  public-safe category labels only.
- Story cards do not include raw code, raw SQL, config values, local paths,
  repository remotes, private sample names, hidden capability names, private
  labels, command output, generated local artifact paths, or credential-like
  values.

### Requirement 3: Define walkthrough contract

The future gallery shall include short walkthroughs that explain how to read a
proof path without upgrading the evidence into a stronger claim.

Acceptance criteria:

- Each walkthrough begins with a static question and ends with one of:
  `evidence-backed static path`, `reduced coverage`, `needs owner follow-up`,
  `internal only`, `hidden`, or `stop: no public-safe evidence`.
- Each walkthrough identifies the root/source surface and destination or
  stopping surface with public-safe category labels.
- Each walkthrough names rule IDs or rule families for evidence-bearing steps.
- Each walkthrough names evidence tiers and coverage labels for the path.
- Each walkthrough shows supporting IDs only when they are public-safe and
  meaningful for review.
- Each walkthrough keeps limitation and stop-condition language visible near
  the conclusion.
- Walkthrough conclusions avoid "impacted", "safe", "complete", "proves",
  "production", "runtime", "performance", "approved", and "release-ready"
  outside explicit rejected-example or boundary contexts, and they avoid
  phrase-level claims such as `endpoint performance`, `release approval`,
  `release safety`, `operational safety`, `complete coverage`, and
  `automated approval`.

### Requirement 4: Define required gallery sections

The future page or section shall include stable sections that make the story
contract reviewable.

Acceptance criteria:

- Include a `story contract` section.
- Include a `proof path anatomy` section.
- Include an `evidence packet references` section.
- Include a `coverage and limitations` section.
- Include a `stop conditions and routing` section.
- Include a `non-claims and forbidden wording` section.
- Include a `gallery validation` section.
- Required anchors are stable and machine-checkable:
  `#story-contract`, `#proof-path-anatomy`,
  `#evidence-packet-references`, `#coverage-and-limitations`,
  `#stop-conditions-and-routing`, `#non-claims-and-forbidden-wording`, and
  `#gallery-validation`.
- Required sections use public-safe authored examples only.
- Required sections do not publish raw artifacts, private repository material,
  generated local outputs, command output, hidden validation details, or
  credential-like values.
- Forbidden-wording, rejected-example, boundary, limitation, validation, and
  non-claim contexts use a stable, machine-distinguishable wrapper so
  validation can exclude those contexts from normal body copy without
  suppressing real claim violations.

### Requirement 5: Define story categories

The future gallery shall support a bounded set of story categories that map to
TraceMap static evidence surfaces without claiming runtime behavior.

Acceptance criteria:

- Include endpoint/service orientation stories.
- Include data or SQL/config orientation stories only as public-safe category
  paths, not raw SQL or raw config output.
- Include package or dependency orientation stories only as static evidence
  paths, not compatibility, vulnerability, or production dependency claims.
- Include generated artifact orientation stories that point to public-safe
  report families or evidence packets, not generated local files.
- Include reduced-coverage stories that show where semantic, build, adapter,
  or extractor gaps stop the path.
- Each category has an explicit limitation and stop-condition rule.
- Categories may be omitted from the first implementation only when the
  omission and rationale are recorded in `implementation-state.md`.

### Requirement 6: Require evidence packet references

The future gallery shall make evidence packet references visible and bounded.

Acceptance criteria:

- Each card references public-safe packet identifiers or report-family labels
  rather than raw files, local paths, or private labels.
- References include rule ID or rule family, evidence tier, coverage label,
  supporting ID when public-safe, limitation, and source context such as
  concept, public demo, hidden, local-only, or future-only.
- References state whether the path is semantic, structural, syntax/textual,
  unknown, or mixed evidence.
- References mark partial analysis, reduced coverage, unknown evidence, and
  analysis gaps visibly.
- References do not expose raw `facts.ndjson`, raw SQLite rows, analyzer logs,
  raw snippets, raw SQL, raw config, private sample names, generated scan
  directories, local absolute paths, raw remotes, command output, or hidden
  validation details.

### Requirement 7: Require stop conditions and owner routing

The future gallery shall show what happens when static evidence stops.

Acceptance criteria:

- Stop conditions include `no public-safe evidence`, `reduced coverage`,
  `semantic gap`, `syntax-only fallback`, `private-only evidence`,
  `hidden detail`, `missing rule ID`, and `requires reducer evidence`.
- Each stop condition includes a next-owner or next-question route such as
  reviewer, code owner, product owner, security owner, data owner, package
  owner, or "do not publish until public-safe summary exists".
- Stop routing does not expose real internal owner names, private teams,
  private sample identities, customer context, local branches, remotes, or
  unpublished artifact labels.
- Stop routing does not imply that a human owner can upgrade a claim without
  public-safe evidence.

### Requirement 8: Validate public safety and implementation scope

The future implementation shall add focused validation for the gallery and run
the site validation workflow when site code changes.

Acceptance criteria:

- Validation checks visible claim level and shared principle for public output.
- Validation checks required sections, stable anchors, required story-card
  fields, required walkthrough fields, stop conditions, and owner routing.
- Validation checks that standalone route metadata, discovery metadata,
  sitemap metadata, canonical URL, title, description, and Open Graph fields
  remain concept-level unless demo-level evidence is recorded.
- Validation checks forbidden AI/LLM, runtime, production, performance,
  release-safety, operational-safety, complete-coverage, and automated
  approval claims outside explicit rejected-example or boundary contexts.
- Validation uses the wrapper contract defined in the design to distinguish
  forbidden-wording boundary contexts from normal body copy.
- Validation checks forbidden private/raw material across rendered text,
  decoded HTML attributes, raw HTML, metadata, sitemap or discovery output,
  examples, fixtures, tests, validation messages, and review-packet
  references.
- Validation checks that story examples use synthetic, public-demo, or
  public-safe category labels only.
- Future site implementation runs `npm test`, `npm run validate`, and
  `npm run build` from `site/`, then `git diff --check` and
  `./scripts/check-private-paths.sh` from the repository root.
- Future layout or interaction changes include desktop and mobile browser
  sanity checks.
