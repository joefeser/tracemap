# Site SQL Static/Observed Validation Requirements

## Goal

Publish a public-safe demo story showing how static SQL evidence and bounded
point-in-time validation observations compose without becoming execution or
approval claims.

## Requirements

1. The route SHALL distinguish evidence tiers from observed statuses.
2. It SHALL explain identity, context, freshness, integrity, and conflict gates.
3. It SHALL define `observed-pass`, `observed-fail`,
   `observed-indeterminate`, and `not-run` narrowly.
4. It SHALL preserve all SQL execution, connectivity, data, state, safety,
   approval, secrecy, and operator-authority boundaries.
5. It SHALL be discoverable at public claim level `demo` and link to the
   existing operator handoff and proof packet.
