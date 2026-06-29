# TraceMap Kiro Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

Review the `site-tracemap-tools-adoption-playbook` spec for future
implementation readiness.

This is a spec-only public site phase for a future `/adoption/` or
`/playbook/` page. The page should explain a concept-level process for
introducing TraceMap into review workflows: start with a public demo, identify
a candidate repository, run deterministic scans, read evidence packets, make
analysis gaps explicit, and decide follow-up ownership.

Please inspect:

- `.kiro/specs/site-tracemap-tools-adoption-playbook/requirements.md`
- `.kiro/specs/site-tracemap-tools-adoption-playbook/tasks.md`
- `.kiro/specs/site-tracemap-tools-adoption-playbook/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-adoption-playbook/review-packet.md`

Review focus:

- Return spec-review findings first, severity ordered.
- Does the spec keep the page concept-level and process/onboarding focused?
- Does it avoid product guarantees, runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  AI impact analysis, LLM analysis, and complete product coverage claims?
- Does it avoid implying TraceMap replaces CI/CD, tests, telemetry, ownership,
  human review, release approval, incident response, or governance?
- Are the workflow steps clear enough for future implementation: public demo,
  candidate repository, deterministic scans, evidence packets, explicit gaps,
  and follow-up ownership?
- Are links to demo, docs, validation, limitations, proof paths, review room,
  and static triage specified with safe route-gap handling?
- Are forbidden private/raw materials excluded from page copy and metadata,
  including source snippets, raw SQL, config values, secrets, local absolute
  paths, raw remotes, generated scan directories, private sample names, raw
  `facts.ndjson`, raw `index.sqlite`, and analyzer logs?
- Are discovery metadata, sitemap/index expectations, validation commands, and
  implementation-state update requirements specific enough for a future site
  implementation?

Include suggested spec edits for any Medium or higher findings.

## Review Output

Review status: patched-after-initial-review

Initial Opus review returned reduced coverage due to denied tool access. Initial
Sonnet review returned Medium findings around validation convention,
discovery-source specificity, forbidden-positioning determinism, validation
script gap handling, route-gap handling, and word-count measurement. Those
findings were patched in the spec files. Sonnet re-review confirmed the Medium
findings were resolved and reported Low clarifications for shared denylist reuse
and word-count lower-bound enforcement; those clarifications were also patched.
Final Sonnet re-review reported no Medium or higher findings. Opus re-review
then reported one Medium finding that a shared denylist constant does not exist
in the current codebase; the spec was patched to match the current neighboring
inline denylist pattern while allowing a future shared-module refactor only if
neighboring validators migrate in the same change. A later Opus re-review
reported one Medium finding that `docs-index.json` is generated only from
`repo-doc` entries, not `site-page` entries; the spec was patched to expect the
future adoption route in `routes-index.json` and the route section of
`llms.txt`. A later Opus re-review reported one Medium finding that `llms.txt`
route-section inclusion depends on a mapped `hintCategory` and needs validator
coverage; the spec was patched to require an `llms.txt` route-section-compatible
`hintCategory` plus generated `routes-index.json` and `llms.txt` assertions.
Final Sonnet re-review reported no Medium or higher findings.

Current review result: ready for future implementation unless a later reviewer
finds a new blocker.
