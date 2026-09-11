# Implementation state

- Branch: `codex/webforms-annotated-source-view`
- Base: `origin/dev` at `4e0e34ff69666f477af272de15f0fb2f2a88d257`
- Tracking: #728, part of #651
- Scope: opt-in private source navigation over retained Web Forms review evidence; no scanner-fact, anonymous-report, BRD, planner, or generator changes.
- Implemented: explicit raw-source mode generates path-sorted sibling annotated HTML files for every retained file. Each complete bounded file has stable line anchors, categorical handler/event-binding/declaration/call-site highlighting, exact links from the private graph/path/evidence, and badges back to the retained evidence cards.
- Bounds and privacy: existing source-root containment, physical-path/symlink, 4 MiB per-file, 16 MiB aggregate, and transactional publication protections remain in force. Full-file views add a 100,000-line fail-closed ceiling. Default runs emit no source views, and anonymous HTML/JSON contain neither private source nor source-view links.
- Tests: six focused .NET report tests pass, including multi-file ordering, overlapping evidence, complete long-line rendering, HTML escaping, output collision, pathological line-count rollback, traversal, and symlink protections. The PowerShell review-set test passes with explicit option-forwarding coverage. Full solution validation passes 1,802/1,802; private-path guard and `git diff --check` pass. One pre-existing nullable warning remains in `PropertyMappingTests.cs`.
- Status: implementation complete; commit, push, PR, and exact-head review remain.
