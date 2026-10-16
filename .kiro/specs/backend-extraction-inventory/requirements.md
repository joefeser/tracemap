# Backend extraction inventory and migration handoff

Tracking: https://github.com/joefeser/tracemap/issues/861

Goal: identify database/outbound HTTP and service operations for extraction into
a human-approved .NET 10 API. Humans choose retained functionality. Static
connection evidence is useful without observing actual human use.

1. Discover supported operations independently of root-path filtering. Preserve
   unconnected operations and explicit coverage/unsupported-input gaps.
2. Deduplicate without losing callsites, evidence variants, inputs or callers.
   Distinguish query-root paths from verified page/event/job entry-point binding.
3. Show established evidence, candidate relationships, unresolved boundaries,
   and truncation. Never classify absent paths as unused code.
4. Carry rule-owned limitations beside affected edges in machine and human
   outputs, including generic-definition resolution without body specialization.
5. Separate private opt-in readable SQL/procedure and HTTP contract capture from
   default hash-only/public-safe reports. Preserve source identity/locations;
   record unknown parameters, results, transactions, auth, destination ownership,
   dynamic commands, missing procedure definitions and database-side dependencies.
6. Every derived machine artifact has generator and bounded-input SHA-256.
   Retained report projections inherit those identities from their container.
7. Add a migration-planning Claude handoff without weakening diagnostic prompts.
   Proposed API operations are proposals, not extracted facts or execution authority.
8. Human retain/retire/replace/undecided state is separate; default undecided.
9. Synthetic, public-safe DB/HTTP fixtures and generic negative controls must
   support one DB and one HTTP end-to-end proof before whole-site rollout.

No runtime connectivity, raw secret capture, inferred production use, automatic
retirement, automatic API generation or complete-coverage claims from partial input.
