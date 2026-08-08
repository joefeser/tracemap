# Reverse Impact Query Implementation State

Status: implemented

## Shipped Scope

- Added `tracemap reverse` for bounded static reverse reachability from dependency surfaces back to endpoint, symbol, source, or all roots.
- Includes deterministic graph traversal, classifications, caps, gaps, safe rendering, Markdown/JSON output, and rule catalog updates.

## Follow-Ups

- Broader reverse selector semantics should use focused follow-up specs.

## Related Single-Scan Symbol Impact

- Issue #590 adds a separate `reverse-impact` surface over one standard scan index. It starts from a canonical symbol or exact unambiguous display name and walks explicitly impact-relevant incoming relationships.
- `tracemap reverse` remains the combined-index dependency-surface-to-root report defined by this spec; the two commands do not share input or output contracts.
- HTTP and database relationships opt into the single-scan kernel only when semantic facts carry canonical source and target identities. Other boundary labels remain excluded rather than name-linked.
