# Legacy WebForms Event Flow Implementation State

Status: ready-for-review

Branch/PR:

- Spec branch: `codex/legacy-webforms-event-flow-spec`
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

Validation:

- Spec-only branch. No code validation required beyond private-path checks and
  Markdown sanity before PR.

Follow-Ups:

- DBML/EDMX/old ORM metadata extraction should be the next legacy-data spec.
- A public demo page should wait until there is checked-in or redacted evidence.
