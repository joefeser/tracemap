# Implementation state

- Branch: `codex/access-conditional-rowsource-composition`
- Issue: #574
- Base: current `origin/dev`, including merged PR #573
- Scope: literal conditional RowSource composition only
- Implemented: static SELECT projection, branch/condition provenance, event
  support, UI binding context, safe fact properties, synthetic tests
- Deferred: bulk #571/#572 work, dynamic SQL, runtime branch reachability,
  Access execution, row reads, and layout reconstruction
- Validation: focused Access VBA tests 14/14; full solution build 0 warnings,
  full solution tests 1,059/1,059; private-path guard and `git diff --check`
  pass. Access COM/private representative smoke was not run in this slice;
  validation remains synthetic and source-neutral.
