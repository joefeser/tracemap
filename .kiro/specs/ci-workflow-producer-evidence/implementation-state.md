# Implementation state

Branch: `codex/ci-workflow-producer-evidence`, from merged `Origin/dev` at `556cc419` (#848).
Scope: read `.github/workflows/*.yml|*.yaml` pack definitions only when
`--index-ci-producers` is enabled. This is the tracemap half of upgrade-authority
SPEC-020 §2b (`ci-defined` producer provenance); the ua-side fusion mapping
(`sourceKind=ci-workflow` ⇒ provenance `ci-defined`, precedence operator > ci >
project) is deliberately NOT implemented here and is built by the ua
coordinator against the pinned fact bytes once this merges.

Design decisions (precedent: the deps.json slice #848):

- No new package dependency. Workflows are parsed by a bounded structural YAML
  subset written for the GitHub workflow dialect. Soundness rule: any construct
  the subset cannot prove becomes a typed gap for the whole file
  (`ci-workflow-unsupported` for recognized-but-unmodeled constructs such as
  anchors, `ci-workflow-invalid` for malformed structure such as tab
  indentation or duplicate keys) — never a guessed fact. Multi-line plain
  scalars and multi-line flow collections therefore reduce coverage instead of
  risking a misparse.
- Fixed absolute caps (64 files, 1 MiB per file, 8 MiB total) with no
  options-tunable limits record; the slice contract requires caps that cannot
  be raised by Core overrides, and fewer knobs means a smaller surface.
- The fact shape mirrors the committed project-declared PackageProduced bytes
  with new property values: `sourceKind=ci-workflow`,
  `manifestKind=github-workflow`, plus `workflowPath`. Unresolvable versions
  are omitted entirely (never empty-string, never guessed) per the pinned ua
  contract. `projectPath` rides the fact only when the pack command names an
  explicit project target.
- Identity resolution implements the SPEC-020 quote: `-p:PackageId` wins; the
  fallback is the packed project's EXPLICIT PackageId declaration (AssemblyName
  fallback ids are not promoted through the CI lane — the project lane already
  carries that weaker claim with `packageIdSource`); everything else is a
  typed `ci-producer-id-unevidenced` gap.
- Version resolution substitutes exactly one `${{ env.NAME }}` expression
  against the workflow's own literal env (step over job over workflow).
  Matrix/github/needs/secrets templates and shell `$VAR` forms are
  unevidence: version omitted + `ci-producer-version-template` gap naming the
  template. Partial templates (`1.0-${{ env.X }}`) are intentionally NOT
  substituted in v1.
- Cross-source dedupe stays in ua's fusion: when project and CI both claim a
  package, BOTH facts are emitted (acceptance case 2). Within the ci-workflow
  source, one effective version collapses; two versions conflict (gap, no fact).
- `${{ }}` expressions must not break shell tokenization (GitHub substitutes
  before the shell parses), so the tokenizer treats them as non-breaking; the
  operator splitter and comment/key scanners skip their interiors.
- Workflow YAML at any other depth (nested dirs) and other CI vendors are out
  of scope; npm publish detection is out of scope because ua is NuGet-only.

Validation: focused `CiWorkflowProducerEvidenceTests` (23 cases) and the
`ScanExecutionReceiptTests` fingerprint additions pass in standard Debug
configuration, and the CLI smoke below emits the pinned fact bytes. Full Debug
suite (`dotnet test src/dotnet/TraceMap.sln --no-restore -warnaserror`):
the first run reported one failure in
`AccessMacroReportingTests.Macro_evidence_reports_hidden_static_counts...`
(3,523 passed / 1 failed); that test passes in isolation on the same binaries
and touches the Access evidence-docs lane with this flag off — no mechanism
connects it to this diff, matching the one-off full-run flake recorded for the
deps.json slice. The second full run passed clean: 3,524 passed, zero failed,
one Windows-only skip (19m37s). Smoke:
`dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/ci-workflow-producers --out <outside> --index-ci-producers`
emits exactly one ci-workflow PackageProduced fact (Contoso.Sample 0.1.0) at
Level1SemanticAnalysis with no gaps.

Follow-ups for later slices: msbuild `/t:Pack` invocations, composite action
pack steps, partial env substitution, and other CI vendors — each needs its
own typed-gap-first design before facts.
