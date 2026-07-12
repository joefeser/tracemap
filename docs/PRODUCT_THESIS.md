# TraceMap Product Thesis

TraceMap is the open evidence engine. The paid or services layer, if one exists,
should be the human-approved operating layer that turns evidence into safe
decisions, workflows, and receipts.

This note preserves the product thesis behind the project so future agents and
contributors do not mistake TraceMap for only a code analyzer.

## Core Belief

Organizations do not only need more answers. They need durable, inspectable
evidence that survives meetings, handoffs, reviews, archives, and operator
mistakes.

TraceMap exists because real engineering work often fails at the boundary
between systems:

- nobody can prove what endpoint calls what service;
- nobody knows which app, script, or scheduled job touches a data surface;
- SQL or release work depends on the right server, database, role, and context;
- review conclusions lose their source evidence;
- knowledge disappears into tickets, chats, vendor decks, and memory.

The product stance is intentionally conservative:

- no conclusion without evidence;
- no evidence without a rule ID;
- no rule without documented limitations;
- no runtime claim from static evidence;
- partial analysis is useful, but must be labeled partial.

## Open Core

TraceMap should stay open where openness builds trust:

- deterministic extractors;
- fact schemas;
- rule IDs and limitations;
- evidence tiers;
- generated reports;
- validation fixtures;
- safety and redaction behavior.

The evidence engine is more credible when users can inspect how facts are
created and why a conclusion is bounded.

## Commercial Or Services Layer

The likely paid value is not hidden analyzer logic. It is operationalization:

- hosted or managed deployment;
- HACP Dispatch / Routeboard-style task custody;
- policy, approval, and audit workflows;
- enterprise identity and repository integrations;
- evidence packet generation for review, release, and change-management rooms;
- SQL/script preflight and operator handoff workflows;
- dashboards, retention, and searchable receipts;
- onboarding, consulting, custom adapters, and support.

Most organizations cannot get the outcome by copying the code alone. The hard
part is knowing what evidence matters, preserving limitations, integrating with
PR and release workflows, avoiding secret leaks, and turning output into
decisions people trust.

## HACP / TraceMap / ACK Relationship

These projects should remain conceptually distinct:

- HACP owns authority, approvals, and human-in-the-loop task custody.
- TraceMap owns static evidence about code, configuration, dependencies, routes,
  SQL surfaces, and gaps.
- ACK owns PR-loop readiness, reviewer state, merge gates, and review receipts.

Together, they support a larger operating model:

1. HACP decides who is allowed to ask for or approve work.
2. TraceMap provides evidence for what the system appears to touch.
3. ACK decides whether the review loop is ready for a human merge decision.
4. The site/docs explain only what the evidence supports.

## Product Wedge

The best wedge is not "AI impact analysis." It is evidence packaging for change
risk:

- legacy modernization without pretending the code ran;
- endpoint-to-service-to-data review;
- SQL archive and operator preflight;
- release-review packets with gaps and limitations;
- AI/agent review support using deterministic evidence instead of blind source
  dumping.

The demo should start with a painful human question:

> Before I run this script, merge this change, archive this data, or tell a
> manager it is safe, what evidence do I have and what can I not prove?

## Boundary

Do not turn the core scanner/reducer into an LLM, embedding, vector database, or
prompt-classification product. Those tools can exist around the evidence layer,
but TraceMap's trust comes from deterministic facts, rule IDs, tiers, coverage
labels, line spans, commit SHA, and explicit gaps.

