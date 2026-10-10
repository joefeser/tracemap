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

## Baz review cycle 1 (PR #851, head `65cbbbd5`)

Baz returned five inline findings; dispositions:

1. TOCTOU symlink swap between the attribute check and the open — fixed: the
   opened path's link state is re-verified (`File.ResolveLinkTarget`) before
   parsing; an unprovable path becomes a `ci-workflow-linked-path` gap.
2. `working-directory` misattribution — fixed: workflow/job `defaults.run`
   and step `working-directory` are tracked (literal-only, workspace-relative,
   step over job over workflow). Relative pack targets resolve from the
   effective directory; a templated directory leaves relative targets
   unattributed (projectPath omitted, project-fallback ids gap as
   `ci-producer-id-unevidenced`). Solution targets never ride projectPath.
3. env declared after jobs/steps lost versions — fixed: env maps are
   per-scope collections resolved after the whole document parses
   (declaration order no longer matters; step > job > workflow preserved).
4. malformed UTF-8 silently replacement-decoded — fixed: strict UTF-8
   decode; malformed bytes are a `ci-workflow-invalid` gap with no rows.
5. literal flag ids "bypass" target attribution — not adopted: per the
   pinned SPEC-020 §2b contract, a literal `-p:PackageId` (+ version) is
   complete ci-defined identity evidence on its own; the project target is
   attribution metadata that is omitted when absent, never a reason to
   downgrade the fact to a gap. Pinned by
   `Literal_flag_identity_is_complete_evidence_without_a_target` and
   documented in the rule catalog; thread answered with this rationale.

## Codex review cycle 2 (PR #851, fix head `f7bc969e`)

`@codex review` returned five findings (1×P1, 4×P2); all five were fixed:

1. P1 — shell comments could overwrite property flags
   (`dotnet pack -p:PackageId=Good # -p:PackageId=Bad`): fixed; command text is
   comment-stripped (unquoted `#` at a word boundary, quotes and `${{ }}`
   protected) before tokenization, per logical line, folded blocks included.
2. `shell: python` (or custom/templated shells) treated as command text:
   fixed; the effective shell is tracked (step over job defaults.run.shell
   over workflow defaults.run.shell) and only command shells (bash, sh,
   pwsh, powershell, cmd, or undeclared defaults) are parsed as commands;
   pack-looking text under any other shell is a `ci-workflow-unsupported`
   gap with no rows.
3. `defaults.run.working-directory` declared after jobs resolved against the
   root: fixed; working-directory scopes now resolve after the whole
   document parses, exactly like env (the same order-independence class).
4. Package identities grouped case-sensitively: fixed; NuGet identities now
   group OrdinalIgnoreCase with the first deterministic occurrence supplying
   display casing, so case-variant claims collapse or conflict as pinned.
5. The TOCTOU fix re-resolved the path rather than the opened handle: fixed;
   macOS/Linux now open workflows through a true `O_NOFOLLOW` descriptor
   (the read belongs to the opened inode), with the Windows path-check
   residual documented in the rule catalog.

Focused suite after fixes: 32/32 pass.

## Loop convergence (head `c48dac1f`)

Baz re-reviewed at the codex-fix head and marked every finding addressed
(its TOCTOU thread notes the Windows path-check residual, which the rule
catalog documents). Codex completed a review of `c48dac1f` (07:12Z,
COMMENTED, zero inline comments) — the zero-finding delta round. All PR
checks green; PR #851 mergeable. Merge remains owner-mediated. Full Debug
suite at `c48dac1f`: 3,533 passed, 0 failed, 1 Windows-only skip; CLI smoke
emits exactly one ci-workflow PackageProduced fact from the public fixture.

Follow-ups for later slices — each needs its own typed-gap-first design
before facts:

- msbuild `/t:Pack` invocations, partial env substitution, and other CI
  vendors (GitLab, Azure Pipelines).
- **Reusable workflow calls and composite actions (backlogged 2026-10-10
  after owner design discussion).** Two cross-repo carriers of pack logic
  are currently *silent* non-evidence (no fact, no gap): job-level
  `uses: owner/repo/.github/workflows/*.yml@ref` (reusable workflows,
  incl. local `./.github/...` forms) and step-level composite actions
  (`action.yml` with `runs.using: composite`, which can hold `run:` steps).
  Stage 1 (cheap, coverage-honest): a typed gap naming each un-followed
  reference — no facts, no crawling. Stage 2 (bounded opt-in): an explicitly
  supplied workflow-repo checkout + ref indexed like the binlog/deps.json
  lanes (hashed as its own bounded input, freshness unknown), with literal
  `with:` inputs mapped through the callee's `workflow_call.inputs`
  defaults. Design constraints pinned in that discussion: `env` never
  crosses the workflow_call boundary (only inputs/secrets), so the env
  scope chain correctly stops at the call edge; moving refs (`@main`,
  tags) make the effective definition time-dependent and only SHA-pinned
  references support a deterministic merge; callee→callee nesting needs
  its own absolute limits.


