# Implementation state

- Branch: `codex/issue-744-webforms-application-workbench`
- Base: stacked on the docs-export safety fixes in PR #741.
- Scope: issue #744 workbench and issue #746 runbook. Issue #745 remains a
  separate review-overlay slice.
- The workbench reads an existing packet and optional docs corpus. It does not
  invoke a scan and never opens `chunks.jsonl` for writing.
- Raw source is absent by default. `-IncludeRawSource` requires `-SourceRoot`
  and retains only bounded excerpts from safe repository-relative paths.

