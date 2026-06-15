# Legacy WebForms Event Flow Implementation State

Status: implemented and ready for PR review

Branch/PR:

- Implementation branch: `codex/legacy-webforms-event-flow`
- PR: pending

Scope:

- Define a conservative WebForms event-to-backend flow phase.
- Cover markup event bindings, code-behind handler resolution, designer control
  fields, event-to-WCF/service/SQL projection, static logic signals, reporting,
  validation, and redaction boundaries.
- Keep runtime UI execution, IIS simulation, event feasibility, DI/runtime
  dispatch proof, and ORM metadata mapping out of this phase.

State Notes:

- This spec intentionally follows the legacy WCF evidence work and should reuse
  existing WCF/service-reference facts where possible.
- Local legacy samples may be used for ignored smoke testing, but committed
  artifacts must use generic labels and redacted counts only.
- Public claim level remains hidden until a checked-in public fixture or
  redacted legacy summary demonstrates the workflow.
- Implementation scope keeps WebForms evidence additive as normal facts in
  `facts.ndjson` and `index.sqlite`; no dedicated SQLite tables are required for
  the MVP because the existing facts table preserves rule IDs, tiers, paths,
  line spans, properties, and supporting IDs.
- WebForms code-behind and designer files are inventoried with WebForms-specific
  kinds while remaining eligible for C# syntax and semantic extraction.
- Auto-event-wireup is limited to `Page_Load` and `Page_Init` and requires
  explicit `AutoEventWireup="true"` evidence in the page/control/master
  directive or explicit static event subscription evidence such as
  `Load += Page_Load`. False, unknown, or contradictory evidence without static
  subscription remains a Tier4 gap.
- Event-flow projection is direct-evidence MVP scope: resolved handlers connect
  to existing direct call/object, WCF mapping, HTTP, SQL/query, config, and
  dependency facts. Multi-hop call graph traversal, runtime DI, reflection, and
  event bubbling remain follow-ups.
- The existing `legacy.validation.ui-events.v1` summary now lets precise
  `WebForms*` evidence supersede the coarse UI token probe when those facts are
  present.
- PR review loop follow-up fixed Windows-style relative markup path handling and
  pre-filtered WebForms code-file/fact scans so large non-WebForms repositories
  do not pay avoidable extractor cost.

Validation:

- `dotnet build src/dotnet/TraceMap.sln` passed with 0 warnings.
- `dotnet test src/dotnet/TraceMap.sln` passed: 275 tests.
- `python3 -m unittest scripts.tests.test_legacy_codebase_validation` passed:
  11 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.
- CLI smoke scan against a temporary WebForms fixture emitted
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log`, and report sections for WebForms Events/Event Flow.

Follow-Ups:

- DBML/EDMX/old ORM metadata extraction should be the next legacy-data spec.
- A public demo page should wait until there is checked-in or redacted evidence.
- Rich multi-hop WebForms event-flow graph traversal should wait for a separate
  bounded graph spec.
