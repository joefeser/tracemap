# Collect a maintainer edge-case follow-up

```text
Read docs/MAINTAINER_EDGE_CASE_TRIAGE.md and the sanitized example. Do not
transfer or reproduce private artifacts. Do not repeatedly rerun an operation
that previously required manual termination.

Return only closed stage tokens, elapsed-time buckets, aggregate count buckets,
completion state, the public TraceMap commit, and sanitized failure
rule/category/location. Replace all private source labels, repository names,
run IDs, and source commit SHAs with opaque references or an explicit
`unavailable-in-sanitized-handoff` state.

Do not weaken export safety, edit product code, commit, push, or publish. If no
bounded diagnostic mode exists, report `diagnostic-mode-unavailable` and stop.
```
