# Design: Web Forms annotated source view

The existing per-case private report remains the review hub. With explicit raw-source opt-in, the report writer derives a deterministic, path-sorted set of retained files and writes sibling `case-NNN.source-NNN.html` artifacts transactionally with the existing private and anonymous outputs.

Each source artifact renders the complete bounded working-tree file with `L<number>` anchors. A line receives one or more categorical CSS classes when a retained span covers it. Badges appear at span starts and link back to the owning evidence card. Private report links point to exact line anchors. Anonymous artifacts are generated independently and retain their existing leak check.

The existing 4 MiB per-file and aggregate source-byte bounds remain authoritative. A separate line-count ceiling rejects pathological files rather than silently producing a partial full-file view. Git is not required; the report continues to state that working-tree equality with the inspection commit is unproven.
