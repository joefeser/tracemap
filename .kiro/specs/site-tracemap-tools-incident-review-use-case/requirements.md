# Site TraceMap Tools Incident Review Use Case Requirements

Status: implemented
Readiness: implemented

Public claim level: concept

## Objective

Create a future site phase for an incident-adjacent review use case page that
helps engineers, reviewers, managers, and architects understand how TraceMap
static evidence can orient code questions during review or incident follow-up.
The page must not claim runtime proof, production state, incident root cause, or
release safety.

This is a site-spec-only packet. It does not implement the public page and does
not change scanner, reducer, demo, or generated artifact behavior.

## Claim Level Rationale

The future page should use `Public claim level: concept` because the use case is
incident-adjacent and easy to overread as runtime observability or root-cause
analysis. Current public pages can demonstrate TraceMap evidence shapes, proof
paths, and limitations, but this specific incident-review story has not shipped
as a dedicated public route and must remain framed as orientation for code
questions, not production proof.

Do not upgrade the page to `demo` until a future phase has checked-in,
public-safe demo material that explicitly supports this route's examples without
publishing raw artifacts or implying incident-response capability.

## Route And Surface Recommendation

- Recommended route: `/use-cases/incident-review/`
- Source file: `site/src/use-cases/incident-review/index.html`
- Sitemap metadata: add `/use-cases/incident-review/` to
  `site/src/_site/pages.json` with `changefreq: "monthly"` and
  `priority: "0.7"`.

Use `/use-cases/incident-review/` instead of `/incident-review/` because the
page is a bounded use-case story, not a standalone product claim or incident
tooling surface. Nesting it under `/use-cases/` keeps the route close to the
existing review, release, dependency, and legacy analysis framing. A future
top-level `/incident-review/` would risk implying TraceMap is an incident
response or observability product.

## Audiences

- Engineers who need to inspect static code evidence after a review question or
  incident follow-up without treating it as runtime proof.
- Reviewers who need rule IDs, evidence tiers, file paths, line spans, coverage
  labels, and limitations before deciding what needs human review.
- Managers who need a bounded way to ask whether a code question has supporting
  static evidence, visible gaps, and follow-up owners.
- Architects who need to orient coupling, package, endpoint, and contract
  questions without turning static evidence into production topology.

## Core Message

TraceMap can help teams turn review or incident-follow-up code questions into a
static evidence packet: what repository snapshot was scanned, which rules found
which facts, what coverage labels apply, which proof paths and artifacts should
be inspected, and what the evidence cannot prove.

The page should describe TraceMap as a companion to logs, traces, tests,
ownership, human review, and incident response. It should never describe
TraceMap as the source of runtime truth.

## Requirements

### Requirement 1: Publish a bounded use-case page in a future phase

The future site implementation shall publish `/use-cases/incident-review/` as a
concept-level page using existing static site patterns.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states that TraceMap provides deterministic static repository
  evidence that can orient code questions during review or incident follow-up.
- The page does not claim TraceMap is an observability, incident-response,
  deployment, testing, ownership, or release-approval tool.
- The page introduces no runtime service, client-side state, external tracker,
  private dataset, or generated local artifact dependency.
- The page links back to `/use-cases/` as the parent surface.
- The page includes a placeholder `<header class="site-header">` block so
  `site/scripts/build.mjs` can replace it with the canonical navigation and set
  `aria-current` dynamically.
- The page does not add `/use-cases/incident-review/` to the primary navigation.

### Requirement 2: Explain role-specific questions

The page shall help each audience ask safer questions from static evidence.

Acceptance criteria:

- Engineers see questions about changed contracts, endpoints, call paths,
  packages, SQL/config surfaces, and analysis gaps.
- Reviewers see questions about rule IDs, evidence tiers, file paths, line
  spans, supporting IDs, coverage labels, and limitations.
- Managers see questions about what has evidence, what remains unknown, and
  where human follow-up is needed.
- Architects see questions about static coupling and dependency surfaces without
  claims about runtime topology, traffic, or deployment.
- Each role question is phrased as orientation or review input, not as a final
  impact or root-cause conclusion.

### Requirement 3: Preserve non-claims

The page shall keep incident-adjacent non-claims visible and explicit.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, deployment
  state, endpoint performance, production dependency understanding, P1 root
  cause, release safety, release approval, or AI impact analysis.
- The page does not imply TraceMap replaces Dynatrace, logs, traces, incident
  response, ownership, tests, human review, code review approval, or release
  approval.
- The page does not say TraceMap proves an endpoint caused an incident, proves a
  change is safe, proves production reachability, or identifies the responsible
  owner.
- The page uses language such as "orient", "inspect", "ask", "review", "static
  evidence", "coverage", "gap", and "limitation" instead of "prove", "confirm",
  "certify", "guarantee", or "root cause".

### Requirement 4: Keep public artifact safety boundaries

The page shall summarize only public-safe information.

Acceptance criteria:

- The page does not publish raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repo remotes, raw `facts.ndjson`,
  `index.sqlite`, combined SQLite files, generated scan directories, analyzer
  logs, or private sample identities.
- The page may mention public route names, public-safe generated summaries,
  checked-in public demo source paths, report-family names, rule/status labels,
  evidence tiers, coverage labels, supporting IDs, counts already published in
  public demo pages, and documented limitations.
- The page states that raw artifacts remain local unless a future explicit
  export policy says otherwise.
- The page keeps every public conclusion tied to static evidence, rule IDs or
  status framing, evidence tiers, coverage labels, proof paths, and
  limitations.

### Requirement 5: Define page sections

The future page shall be concise, scan-friendly, and tied to proof paths.

Acceptance criteria:

- Include a hero that says the route is a concept-level incident-review use case
  for static evidence, not runtime proof.
- Include a "What TraceMap can orient" section covering changed surfaces,
  static paths, packages, SQL/config surfaces, coverage labels, and gaps.
- Include a role-based section for engineers, reviewers, managers, and
  architects.
- Include a proof-path section that points readers to public-safe evidence
  surfaces, describes how each linked surface should be used, and says raw
  artifacts remain local.
- Include a non-claims and artifact-safety section with incident-specific
  boundaries.
- Include a final link section to the parent use-cases page and detailed proof,
  capability, limitation, and roadmap pages.
- Use one canonical back-link set on the new page: `/use-cases/`,
  `/manager-packet/`, `/packets/`, `/demo/proof-assets/`,
  `/demo/proof-upgrades/`, `/capabilities/`, `/limitations/`, `/roadmap/`,
  `/evidence/`, and `/outputs/`.

### Requirement 6: Cross-link from existing public surfaces

The page shall be discoverable from relevant existing pages without expanding
the primary navigation.

Acceptance criteria:

- `/use-cases/` links to `/use-cases/incident-review/` near review, release, or
  incident-adjacent language.
- `/manager-packet/` links to `/use-cases/incident-review/` as a bounded
  follow-up path for managers.
- `/packets/` links to `/use-cases/incident-review/` where packet readers choose
  a workflow or role path.
- `/demo/proof-assets/` links to `/use-cases/incident-review/` only if the copy
  says visuals are orientation over demo evidence, not incident proof.
- `/demo/proof-upgrades/` links to `/use-cases/incident-review/` only if the
  copy remains tied to demo proof rows and limitations.
- `/capabilities/` links to `/use-cases/incident-review/` from static evidence
  or boundaries context.
- `/limitations/` links to `/use-cases/incident-review/` only in a way that
  reinforces non-claims.
- `/roadmap/` links to `/use-cases/incident-review/` from an existing
  concept/future planning section with bounded concept-level wording; if that
  section no longer exists or the wording would be awkward, the implementation
  defers the roadmap link and records the reason in `implementation-state.md`.
- The new page links back to `/use-cases/`, `/manager-packet/`, `/packets/`,
  `/demo/proof-assets/`, `/demo/proof-upgrades/`, `/capabilities/`,
  `/limitations/`, `/roadmap/`, `/evidence/`, and `/outputs/`.

### Requirement 7: Validate the future site implementation

The future implementation shall run the existing static-site validation workflow
and record results in this spec's implementation state.

Acceptance criteria:

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Run desktop and mobile browser sanity checks for
  `/use-cases/incident-review/`.
- Run a focused browser sanity check for at least one touched doorway page.
- Manually review visible copy for the `Public claim level: concept` marker,
  non-claims, artifact-safety boundaries, static-evidence framing, and
  proof-path links because `npm run validate` does not inspect claim language.
  This is a durable manual review requirement, not a temporary tooling gap.
- Manually record each conditional cross-link added from `/demo/proof-assets/`,
  `/demo/proof-upgrades/`, `/limitations/`, and `/roadmap/`, including the
  wording that keeps the link bounded.
- Confirm internal links resolve from each touched doorway page and from the new
  page back to the canonical back-link set.
- Confirm `site/dist/` and `site/output/` are generated outputs and are not
  hand-edited.
- Record any intentionally deferred validation with the reason.

## Validation Plan For This Spec Packet

- Run the repo-supported Kiro spec review with Opus if available.
- Run the repo-supported Kiro spec review with Sonnet if available.
- Patch Medium+ findings and any narrow safe Low findings.
- Run `git diff --check`.
- Run any cheap spec or wrapper validation available for spec-only changes.
