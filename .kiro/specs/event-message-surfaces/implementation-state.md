# Event And Message Dependency Surfaces Implementation State

Status: spec-ready

## Branch

- `codex/spec-event-message-surfaces`

## Scope Decisions

- This PR is spec-only and does not implement scanner, reducer, report, schema,
  or adapter code.
- The spec treats event/message surfaces as deterministic static dependency
  evidence, not runtime topology or traffic proof.
- v1 should detect static queue/topic/event identities only when safe evidence
  exists through literals, constants, annotations/decorators, framework config,
  manifests, or symbol-backed event types.
- In-process mediator/notification patterns are explicitly excluded from
  broker-backed message surfaces in v1.
- Dynamic, ambiguous, unsupported, or unsafe destination evidence must produce
  reduced coverage or analysis gaps rather than guessed concrete surfaces.
- .NET is the likely first implementation slice. TypeScript, Python, and JVM
  are defined as extension boundaries to avoid overcommitting v1 parity.
- Combined reports, paths, route-flow, reverse queries, and reducer context can
  consume event/message evidence only with explicit caveats and supporting rule
  IDs.

## Safety Notes

- Do not commit private sample names, raw queue/topic/event names from private
  repositories, raw config values, hostnames, URLs, connection strings, raw
  remotes, local absolute paths, source snippets, or secrets.
- Public outputs should render safe framework families, direction, surface kind,
  evidence tier, rule ID, safe file span, and hashes or safe normalized keys.
- Static publish/consume matches do not prove runtime delivery, subscription
  activity, broker topology, production traffic, payload compatibility, auth,
  retries, retention, ordering, or deployment reachability.

## Validation

- Kiro Opus spec review ran with full coverage on 2026-06-18. It found no
  blocking issues and identified important spec clarifications.
- Kiro Sonnet spec review ran with full coverage on 2026-06-18. It found no
  blocking issues and identified important spec clarifications.
- First Sonnet re-review ran with reduced coverage because Kiro attempted a
  denied shell tool. The review still identified concrete blockers around
  candidate edges, stable keys, binding projection, and destination hashes.
- Final Sonnet re-review ran with full coverage after shell read access was
  allowed. It identified final blockers around rule-catalog gating,
  `bind`/`declare` key semantics, and binding-only report requirements.
- Post-final-review patches addressed those blockers by strengthening task
  gates, clarifying stable-key rules, adding binding-only acceptance criteria,
  and naming the required implementation tests. No third Kiro re-review was run
  because this loop is capped at two re-review cycles.
- `git diff --check -- .kiro/specs/event-message-surfaces` passed.
- `./scripts/check-private-paths.sh` passed.

## Follow-Up Items

- Implementation PRs must update task checkboxes as work completes.
- First implementation PR must add or update rule catalog entries before
  emitting new rule IDs.
- Adapter implementation slices should update `docs/LANGUAGE_ADAPTER_CONTRACT.md`
  and validation notes if shared facts, schema, or report contracts change.
