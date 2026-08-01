# Implementation state

- Branch: `codex/access-conditional-rowsource-composition`
- Issue: #574
- Base: current `origin/dev`, including merged PR #573
- Scope: literal conditional RowSource composition only
- Implemented: static SELECT projection, branch/condition provenance, event
  support, UI binding context, safe fact properties, synthetic tests
- Deferred: bulk #571/#572 work, dynamic SQL, runtime branch reachability,
  Access execution, row reads, and layout reconstruction
- Validation: focused Access VBA tests currently passing; full solution and
  repository validation are run before PR handoff
