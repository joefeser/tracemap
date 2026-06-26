# Site TraceMap Tools Static Vs Runtime Field Guide Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Summary

This design describes a future concept-level public field guide for
`tracemap.tools`. The guide should explain how deterministic static evidence
and runtime telemetry answer different questions, and how teams can use both
without overstating either.

The surface is explanatory guidance. It is not a runtime observability feature,
APM integration, incident dashboard, traffic analysis, endpoint performance
report, release-safety gate, AI/LLM impact-analysis feature, or replacement
for service-owner judgment.

## Recommended Placement

The site already has a shipped `/static-vs-runtime/` concept page produced by
the `site-tracemap-tools-static-vs-runtime-telemetry` spec. Future
implementation must reconcile with that page before adding a new route.
Extending the existing `/static-vs-runtime/` page is preferred when the field
guide can fit as a deeper practical section without weakening the existing
page's claim boundaries. A distinct `/static-vs-runtime-field-guide/` route is
allowed only when the implementation records why a second concept-level static
versus runtime surface is warranted, how the two pages cross-link, and how
duplicate discovery entries are avoided.

Candidate placements:

- `/static-vs-runtime/` section: preferred first check because this route
  already owns the static-versus-runtime concept boundary.
- `/static-vs-runtime-field-guide/`: allowed if the existing page cannot host
  the practical field-guide content cleanly and a separate route's role is
  documented.
- A route under a future guide or article family: acceptable if the site has a
  stable public article pattern at implementation time.
- Section on `/limitations/`: acceptable if the implementation wants this
  boundary near limitation copy, but it may reduce discoverability for review
  and handoff workflows.
- Section on `/use-cases/incident-review/` or `/incident-call/`: acceptable
  only if the guide is intentionally scoped to incident-adjacent usage, not the
  broader static-versus-runtime distinction.

The future implementation should prefer a standalone route when the comparison
table, workflow guidance, non-claims, metadata, validation, and related links
would make a folded section too dense. If a folded placement is selected, add
stable section anchors and record how host metadata remains concept-level and
does not imply runtime proof.

The page should not be added to primary navigation unless a future
information-architecture note records why a concept-level field guide belongs
there. Default discovery should come from sitemap metadata, discovery metadata,
and bounded cross-links from adjacent public-safe pages.

## Page Structure

Recommended section order:

1. Opening field-guide header with `Public claim level: concept`,
   `No public conclusion without evidence`, and the core message:
   `TraceMap shows static dependency evidence and limitations; runtime tools
   show observed behavior. Neither replaces the other.`
2. `Different questions`: a scannable comparison between static evidence
   questions and runtime observability questions.
3. `How to use both`: before runtime review, during handoff, and after runtime
   review.
4. `Reading a static evidence packet`: rule ID, evidence tier, file path, line
   span, commit SHA, extractor version, coverage label, limitation, and
   follow-up owner.
5. `Where runtime tools remain authoritative`: logs, traces, metrics, APM,
   dashboards, alerts, production traffic, endpoint performance, incident
   timelines, runtime errors, and service-owner interpretation.
6. `Non-claims`: explicit boundaries for runtime proof, release safety,
   operational safety, incident truth, complete coverage, AI/LLM analysis, and
   replacement of human judgment.
7. `Proof paths and limitations`: public-safe links to docs, validation,
   limitations, outputs, proof paths, capabilities, demo summaries, and
   adjacent field-guide pages that exist at implementation time.
8. Related links and handoff next steps.

Recommended stable anchors:

- `#different-questions`
- `#how-to-use-both`
- `#reading-static-evidence`
- `#runtime-authority`
- `#non-claims`
- `#proof-paths-and-limitations`
- `#related-links`

If folded into a host page and any anchor collides, prefix the anchors with
`static-runtime-field-guide-` and record the mapping in
`implementation-state.md`.

## Comparison Table

The central comparison should preserve row relationships on desktop and mobile.
Use existing site table patterns where available. If current site patterns do
not support narrow viewports well, use a responsive wrapper or stacked row
layout that keeps headers associated with cells.

Minimum fields:

| Static question | TraceMap evidence shape | Runtime question | Runtime owner or system | Limitation | Handoff |
| --- | --- | --- | --- | --- | --- |
| What static references exist for this surface? | Rule-backed fact, evidence tier, file path, line span, commit SHA, extractor version. | Did requests exercise the surface in production? | Logs, traces, metrics, APM, dashboard, or service owner. | Static evidence does not observe traffic. | Hand static context to runtime owner. |
| What dependency path is visible in the repository snapshot? | Deterministic static dependency evidence with coverage label and limitations. | Did the dependency participate in an incident or runtime error? | Incident review, runtime telemetry, and service-owner interpretation. | Static paths are not incident truth. | Pair static path with runtime timeline. |
| What analysis gaps remain? | Reduced coverage, syntax fallback, unavailable semantic evidence, or `AnalysisGap` style fact. | Did runtime data resolve or contradict the concern? | Runtime systems, tests, and owner review. | A gap is not a clean bill of health. | Assign follow-up instead of claiming certainty. |

The implemented copy may adjust row wording, but it must retain the boundary:
TraceMap can show static evidence and limitations; runtime tools show observed
behavior.

## Wording Model

Use bounded language:

- `static evidence shows`
- `TraceMap found static references`
- `dependency evidence points to`
- `coverage is reduced`
- `this scan does not establish runtime behavior`
- `runtime owners should review observed behavior`
- `needs review`
- `owner decision needed`

Avoid affirmative wording that upgrades static evidence:

- `TraceMap proved impact`
- `runtime confirmed`
- `production is unaffected`
- `safe to release`
- `approved to merge`
- `endpoint performance is known`
- `incident root cause`
- `complete coverage`
- `AI analyzed the change`

Unsafe examples may appear only in an explicitly marked forbidden-wording or
non-claim context so validation can distinguish teaching examples from product
claims.

## Forbidden-Wording Example Wrapper

When the future page includes a forbidden-wording teaching example, such as a
`do not say` table row, the example must be wrapped in a
machine-distinguishable marker. Acceptable patterns at implementation time
include a `data-forbidden-wording-example` attribute on the containing element,
a dedicated CSS class such as `tracemap-forbidden-example`, or an equivalent
site pattern that validation scripts can query without relying on surrounding
prose position.

The implementation must document the chosen wrapper mechanism in
`implementation-state.md` before writing validation tests that depend on it.

## Metadata And Discovery

Standalone implementations should add route metadata using concept-level copy:

- Title: a field-guide title, such as `Static Evidence vs Runtime Telemetry`.
- Description: describe the page as a guide for comparing deterministic static
  evidence with runtime observability, without claiming runtime proof.
- Canonical and Open Graph metadata: use the selected public route.
- Sitemap entry: include the route only after the page is public-safe and
  link-valid.
- Discovery metadata: use the current schema. If the current `site-page` shape
  remains in use, include `path`, `title`, `summary`, `publicClaimLevel`,
  `sourceType`, `hintCategory`, `preferredProofPath`, `limitations`, and
  `nonClaims`. At spec time, allowed `hintCategory` values are `start`,
  `evidence`, `limitations`, `demo`, `repo-doc`, `roadmap`, and `use-case`;
  verify the current set before implementation.

The metadata must not describe TraceMap as a runtime monitor, APM tool,
telemetry collector, production traffic analyzer, endpoint performance tool,
incident automation system, release-safety system, or AI/LLM impact analyzer.

Recommended `nonClaims` concepts:

- Does not prove runtime behavior.
- Does not observe production traffic.
- Does not establish endpoint performance.
- Does not determine incident truth or outage cause.
- Does not approve releases or merges.
- Does not replace logs, traces, metrics, APM, tests, service owners, incident
  response, or human judgment.
- Does not use AI/LLM impact analysis in the core scanner or reducer.

## Public-Safety Boundaries

The future guide must use category-level, synthetic, or already public-safe
examples. It must not publish raw facts, SQLite indexes, analyzer logs, source
snippets, SQL, config values, secrets, local paths, raw remotes, generated scan
directories, private sample names, raw telemetry payloads, incident timelines,
customer data, service names, production identifiers, or vendor dashboard
screenshots.

Specific observability vendors should not be named as shipped integrations
unless a future implementation has public repo evidence for those integrations.
Generic references to APM, logs, traces, metrics, dashboards, alerts, and
telemetry are allowed when they are clearly complementary runtime systems.

## Validation Shape

Future validation should follow existing site validation patterns and inspect
rendered text, decoded HTML attributes, raw HTML, route metadata, sitemap
metadata, discovery metadata, tests, and validation messages where relevant.

Validation should cover:

- Required visible copy and claim-level labels.
- Required sections and stable anchors.
- Accessible comparison table structure and mobile readability.
- Related link resolution in generated output.
- Standalone metadata, sitemap metadata, discovery metadata, and canonical URL
  when a standalone route is selected.
- Section metadata reconciliation when a folded placement is selected.
- Forbidden runtime, production, performance, incident-truth, release-safety,
  operational-safety, complete-coverage, AI/LLM, and replacement-of-human-
  judgment claims.
- Forbidden raw/private material and credential-like values.
- Allowed generic runtime terms only in complementary or non-claim contexts.
- Machine-distinguishable wrappers for forbidden wording examples.
- Desktop and mobile browser sanity when page layout or interaction changes
  are implemented.
