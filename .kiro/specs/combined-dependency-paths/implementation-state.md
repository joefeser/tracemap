# Combined Dependency Paths Implementation State

Status: implemented

## Shipped Scope

- Added `tracemap paths` for bounded static dependency path search over combined indexes.
- Includes graph inventory, endpoint matching, source-local path traversal, deterministic Markdown/JSON, classifications, rule-backed gaps, and safe rendering.

## Follow-Ups

- Reverse traversal and broader graph semantics live in later specs such as `reverse-impact-query`.
- Do not use unchecked boxes in this spec for wishlist traversal modes.

## 2026-09-19 terminal reachability follow-up

- Algorithm version `1.1` keeps the ordinary path-enumeration depth bound but
  uses remaining work/frontier budget to retain one deterministic shortest
  terminal witness for a root that otherwise stopped at the depth frontier.
- The prewalk is iterative and cycle safe. It deduplicates graph node plus
  dispatch mode, preserves dispatch-cross-hop restrictions, and does not add a
  graph database or runtime-reachability claim.
- Depth gaps remain when other branches were not enumerated, so finding one
  deeper terminal does not turn partial evidence into a completeness claim.
- Validation: focused terminal, cycle, Web Forms, fairness, and memory-budget
  checks passed 7/7; the full .NET solution passed 1,958/1,958; `git diff
  --check` passed.
