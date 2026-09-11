# Implementation state

- Branch: `codex/issue-744-webforms-application-workbench`
- Base: stacked on the docs-export safety fixes in PR #741.
- Scope: issue #744 workbench, issue #745 review overlay, and issue #746
  operator runbook.
- The workbench reads an existing packet and optional docs corpus. It does not
  invoke a scan and never opens `chunks.jsonl` for writing.
- Raw source is absent by default. `-IncludeRawSource` requires `-SourceRoot`
  and retains only bounded excerpts from safe repository-relative paths.
- Post-review hardening uses the authoritative packet and evidence-corpus
  validators, rejects source-root symlink/junction escapes, preserves full gap
  and inventory metadata, distinguishes case-sensitive surface IDs, and emits
  boundary retrieval recipes only when the terminal evidence is a fact.
