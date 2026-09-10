# Claude prompt: autonomously repair workspace diagnostic lineage

Read these repository files completely before acting:

1. `AGENTS.md`
2. `.kiro/specs/webforms-bounded-report-memory/README.md`
3. `.kiro/specs/webforms-bounded-report-memory/restricted-run-2026-09-02.md`
4. `src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs`
5. `src/dotnet/TraceMap.Core/BuildEnvironmentDiagnosticExtractor.cs`
6. `scripts/Export-FocusedWebFormsWorkspaceSummary.ps1`

This is a TraceMap diagnostic-correctness task. Do not write a BRD, migration
requirements, application documentation, or application-source changes. Do not
infer runtime behavior or complete event-to-database chains.

The application repository and retained scan are private. Never commit, paste,
summarize, screenshot, or otherwise transmit repository names, project names,
paths, source, symbols, native diagnostic messages, configuration values,
connection information, or business data. Work with synthetic fixtures and
closed categories. Keep generated output outside both repositories.

## Objective

Make TraceMap explain how each projected `BuildEnvironmentDiagnostic` was
derived without retaining raw MSBuild/Roslyn text. Specifically, prevent
`LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset` from being
an untraceable projection and keep ordinary compiler diagnostics distinct from
workspace/project/solution-load failures.

Continue autonomously through inspection, failing tests, implementation,
validation, and a synthetic CLI scan. Do not stop merely to ask what to do next.
Stop only for a missing required tool, an unsafe operation, an ambiguous schema
decision that would break compatibility, or a failing test unrelated to the
change that cannot be isolated.

## Known field evidence

The retained private index reported:

- `BuildEnvironmentDiagnostic` projection:
  `LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset` = 10,588;
- `BuildEnvironmentDiagnostic` projection:
  `UncategorizedWorkspaceFailure|ReviewEnvironmentGap` = 1;
- retained `csharp.semantic.workspace.v1` `AnalysisGap` rows:
  one `WorkspaceDiagnostic` and one `ScanScopeExcludedSources`;
- retained `CompilationDiagnostic` rows under that rule: 0.

Therefore the origin of the 10,588 projected rows is indeterminate from the
retained index. Do not label them compiler errors, genuine toolset failures, or
duplicates without new evidence. One genuine workspace diagnostic is retained,
but its native message was deliberately redacted.

## Work sequence

### 1. Establish a clean implementation branch

Start from fresh `origin/dev`, not from the documentation branch. Use a new
`codex/` branch. Preserve the documentation branch unchanged. Record base/head,
scope decisions, validation, and deferrals in the most relevant existing Kiro
implementation-state file, or create a focused spec state file if necessary.

Do not push, open a pull request, or merge unless the operator explicitly asks.

### 2. Prove the current behavior with synthetic tests

Add synthetic tests covering these origins independently:

- an old-style, non-SDK .NET Framework 4.5 project with one ordinary compiler
  error such as `CS0103` after `MSBuildWorkspace` successfully creates a
  compilation;
- one `WorkspaceDiagnostic` callback;
- `ProjectLoadFailed` and `SolutionLoadFailed`;
- `CompilationCreateFailed` and `CompilationMissing`;
- `MSBuildRegistrationFailed`;
- one legacy project with static toolset/project-format diagnostics but no
  workspace failure;
- repeated equivalent diagnostics to prove deterministic bounded behavior.

First demonstrate the incorrect or missing lineage. Do not encode the current
misclassification as desired behavior.

### 3. Implement the smallest safe correction

Preserve enough closed metadata on projected environment facts to answer:

- original gap kind;
- safe compiler/MSBuild diagnostic ID when one exists;
- whether the origin is compilation, workspace callback, project load,
  solution load, compilation creation, MSBuild registration, restore, or static
  project inspection;
- deterministic occurrence count or bounded aggregation state when equivalent
  diagnostics repeat;
- sanitization state and coverage effect.

Requirements:

- do not store native messages, snippets, absolute paths, private names, URLs,
  connection material, configuration values, or source-derived identifiers;
- do not treat `CompilationDiagnostic` as a workspace/toolset failure;
- do not relabel an uncategorized diagnostic solely because the project is
  legacy without preserving its origin and explicitly bounded reasoning;
- retain safe IDs such as `CS0103` or `MSB4019` only when they match a strict
  closed pattern;
- preserve existing rule IDs where compatible; document and test any schema
  addition;
- preserve partial scanning and all independently proven semantic, syntax,
  markup, configuration, SQL, and Web Forms evidence;
- keep output deterministic and bounded;
- do not add LLM calls or heuristic prompt-based classification.

Prefer improving the normal safe artifacts over adding raw logging. If exact
native text is still required, document the existing local-debugger procedure
as a separate operator action. Do not implement an unsafe raw-output feature in
this slice unless it is strictly necessary and explicitly authorized.

### 4. Repair summary lineage

Update `Export-FocusedWebFormsWorkspaceSummary.ps1` so its counts retain the
safe origin category and cannot present a projected guidance count as a proven
toolset failure. Add PowerShell regression tests for the new output.

The summary must distinguish at least:

- compiler diagnostic;
- workspace callback;
- project/solution load failure;
- compilation creation/missing;
- MSBuild registration;
- static legacy-project clue;
- unknown origin.

Unknown lineage must remain explicitly unknown; never backfill it by guessing.

### 5. Validate with synthetic data

Run focused tests, the full .NET solution tests, formatting verification,
private-path guard, and `git diff --check`. Run the relevant PowerShell tests.
Follow `docs/VALIDATION.md` for any language-adapter implications and explicitly
defer unrelated pinned smokes.

Run the CLI against a synthetic non-compiling .NET Framework 4.5 solution and
show that:

- the ordinary compiler error remains a compiler diagnostic with its safe ID;
- it is not reported as a proven toolset prerequisite;
- a synthetic workspace/load failure retains its safe origin;
- reduced coverage remains truthful;
- syntax and Web Forms evidence remain present;
- no native diagnostic text or private-like value appears in artifacts.

### 6. Optional private rerun

Only after implementation and all relevant validation pass, the operator may
run the existing focused local-review command against the private repository
using an already-known solution path and a new empty output directory. Do not
guess paths, delete prior output, modify the application repository, restore
packages without explicit authorization, or write inside either repository.

If the command and paths are already present in operator-owned local notes, use
them. Otherwise stop and provide the exact command template with placeholders.

Analyze the fresh index using only aggregate closed fields. Report whether the
10,588 orphaned projection disappears or becomes attributable to safe origins.
Do not compare counts across different source snapshots as a product regression
unless the source snapshot and scan options match.

## Local diagnosis of the one native workspace message

If a genuine workspace/load failure remains after the lineage fix, use a local
debugger to break in the `MSBuildWorkspace` failure callback and inspect
`args.Diagnostic.Kind` and `args.Diagnostic.Message` on screen. Do not save or
transmit the raw message. Reduce it locally to:

```text
diagnostic kind
safe MSB/CS diagnostic ID, when present
closed missing-component category
affected project ordinal
normalized occurrence count
```

Allowed closed categories include only categories already supported or added
with deterministic tests, such as reference assemblies, Web Application
targets, imported targets, SDK resolution, project evaluation, MSBuild
registration, compilation creation, or unknown.

## Final response

Return a concise engineering handoff containing:

- branch, base SHA, head SHA, and commits;
- the proven defect and its synthetic reproduction;
- files changed;
- safe schema/summary behavior before and after;
- focused and full validation results;
- private rerun status, if performed;
- remaining genuine workspace/load categories;
- explicit limitations and deferred work.

Do not produce a BRD. Do not claim the application is ready for migration. Do
not push, open a PR, merge, or modify the private application repository without
separate operator authorization.
