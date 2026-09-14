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
- Battle-test scaling follow-up: some authorized legacy repositories contain
  many unrelated solutions/projects plus Web Site files with no project owner.
  Project discovery must remain bounded to the configured Web Forms, backend,
  and controls roots. The current supported bridge is to pass the deduplicated
  scoped `-ProjectRelativePath` array for semantic loading while retaining
  syntax/structural extraction for other in-scope files.
- Future operator-contract work: allow each of `WebFormsFolder`,
  `BackendFolder`, and `ControlsFolder` to accept multiple roots (native arrays;
  optional delimited input only as a convenience), propagate those roots
  through resume and summary scripts, deduplicate overlaps, and retain coverage
  provenance per project-owned versus projectless file. Do not fall back to an
  unbounded repository-wide project search.
- Pre-flight usability follow-up: `Invoke-FocusedWebFormsReview.ps1` currently
  collapses a mistyped or missing `SolutionRelativePath` into the opaque
  `SOLUTION_SCOPE_UNAVAILABLE` exception at `Resolve-RelativeChild`. Before any
  scan starts, distinguish blank input, rooted/traversal input, wrong extension,
  not-found path, non-file path, and path outside the source root. Report the
  supplied repository-relative value and show a bounded, deterministic list of
  `.sln` candidates discovered only beneath the configured Web Forms, backend,
  and controls roots. Keep categorical error codes stable and do not disclose
  unrelated absolute paths.
- Unified-runner follow-up: generate one run ID once and place scan, packet,
  evidence-docs, application workbench, and optional supplemental review beneath
  one run root. Downstream/optional phases should accept that run root as their
  single locator instead of requiring operators to reconstruct `IndexPath`,
  `PacketPath`, and `EvidenceDocsRoot` from independently timestamped folders.
- Persist the six workstation inputs (`SourceRoot`, `WebFormsFolder`,
  `BackendFolder`, `ControlsFolder`, `SolutionRelativePath`, and `OutputRoot`) in
  a private local configuration file at the run root so a resume can read them
  back. Support the explicit-project alternative as a `ProjectRelativePath`
  array and the future multi-root folder arrays. Keep reusable operator inputs
  separate from the immutable run manifest, never treat paths as portable or
  public metadata, and store no credentials or source contents in the config.
- The run manifest must link every phase to the same scan ID and source/TraceMap
  commit provenance, record phase status and relative artifact paths, and reject
  mixed-run inputs. Add completion/failure markers, retained/regenerable/optional
  size classifications, a latest-successful locator, and dry-run-first cleanup
  that cannot delete artifacts still referenced by a retained handoff.
