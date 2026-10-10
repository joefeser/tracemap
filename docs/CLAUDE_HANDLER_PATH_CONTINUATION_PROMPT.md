# Claude continuation prompt

Continue the existing TraceMap fix on branch `codex/webforms-directory-publication-maps`. Read `AGENTS.md` and `docs/CLAUDE_HANDLER_PATH_DEBUGGING_HANDOFF.md` first, then inspect current HEAD and all uncommitted changes. Preserve prior work. Do not restart the investigation from scratch.

Problem: the normal handler-to-database report produced 0 exact chains and 0 retained variants, while the unfiltered graph retained SQL endpoints. The graph-only misleading-report fix is already implemented; it does not prove normal traversal works.

The previous session found and edited a specific candidate in:
`src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.CompiledIl.cs`

The owner's screenshot shows removal of this filter from the `ManagedMethodDeclared` input to `PublishMemberCandidateIndex`:

```csharp
.Where(fact => fact.Properties.GetValueOrDefault("provenanceState") == "bound")
```

That edit may exist only in the owner's checkout. Inspect before assuming it is committed or present locally.

Hypothesis: this filter excludes deploy-only compiled methods even when a publication receipt independently verifies the assembly hash. Full source/build provenance and publication-receipt identity are different evidence. This is a candidate cause, not yet a proven fix.

Your job:

1. Inspect the existing diff and trace the downstream publication-receipt assembly-hash and member-identity checks. Establish whether removing this filter is sound. Do not accept the added comment as proof.
2. Add a focused regression that fails before the fix: a valid publication receipt, without full compiled build provenance, bridges an exact source handler to a compiled method and retains a database route in **normal native requery**.
3. Verify missing/mismatched receipt or assembly hash cannot create the bridge; ambiguous members remain unresolved. Preserve evidence tiers and provenance limitations.
4. Verify the normal receipt, handoff, and HTML agree on nonzero chains/variants. Merely finding an endpoint in the unfiltered graph is insufficient.
5. Run focused tests first. Do not repeatedly run the full suite during diagnosis. Do not rebuild assemblies concurrently with provenance-sensitive tests. If a test stalls, identify the active test/process before rerunning.
6. Once the fix and negative cases pass, run relevant broader validation once, then commit and push to this existing branch. No force-push or merge.

Use synthetic fixtures only; do not commit private screenshots, graphs, source, paths, or SQL.

Keep a concise implementation-state note with the hypothesis, evidence, changed files, completed tests, and next step so compaction does not cause repeated work. Do not stop merely to ask whether to continue.

Finish with the proven cause, exact commit, completed validation, and any remaining private acceptance boundary. Do not claim the customer's case is fixed unless actually verified. If blocked, identify the precise missing evidence rather than asking for another broad diagnostic cycle.
