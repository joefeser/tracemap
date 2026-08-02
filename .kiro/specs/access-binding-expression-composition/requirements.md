# Access binding and expression composition

## Purpose

Extend the source-neutral Access design projection with deterministic
record-source reconciliation, bounded expression dependencies, and separate
value/persistence versus list/combo population evidence.

## Requirements

- Preserve query-output and source-field identities separately.
- Resolve direct table/query bindings only with exact owning-surface context.
- Project bounded functions, operators, fields, controls, domain-query
  references, criteria dependencies, and safe literal classes without
  evaluating expressions.
- Keep dynamic, ambiguous, malformed, and unsupported expressions as explicit
  rule-backed gaps.
- Classify list/combo value binding independently from population binding and
  never invent persistence for an unbound selector.
- Preserve commit/snapshot identity, evidence tier, coverage, extractor
  version, supporting facts, spans, and limitations.
