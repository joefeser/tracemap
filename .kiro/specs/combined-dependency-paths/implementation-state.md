# Combined Dependency Paths Implementation State

Status: implemented

## Shipped Scope

- Added `tracemap paths` for bounded static dependency path search over combined indexes.
- Includes graph inventory, endpoint matching, source-local path traversal, deterministic Markdown/JSON, classifications, rule-backed gaps, and safe rendering.

## Follow-Ups

- Reverse traversal and broader graph semantics live in later specs such as `reverse-impact-query`.
- Do not use unchecked boxes in this spec for wishlist traversal modes.

## 2026-09-19 terminal reachability follow-up

- Algorithm version `1.2` inventories all distinct supported terminal nodes for
  every selected Web Forms handler and retains one shortest deterministic
  witness per terminal before ordinary depth-limited detail enumeration.
- The prewalk is iterative and cycle safe. It deduplicates handler, graph node,
  and dispatch mode, preserves dispatch-cross-hop restrictions, and does not
  add a graph database or runtime-reachability claim.
- Packet, handoff, comparison, and HTML outputs distinguish retained-graph
  terminal reachability completeness from path-detail truncation. Work,
  frontier, and path safety limits explicitly make the terminal inventory
  incomplete instead of supporting a false absence conclusion.
- The targeted diagnostic regenerates depth 8 and depth 10 from the same merged
  index and current generator before comparison.
- Validation: focused traversal/Web Forms tests passed 90/90; the full .NET
  solution passed 1,959/1,959; all 18 PowerShell regression scripts passed;
  `git diff --check` passed.
