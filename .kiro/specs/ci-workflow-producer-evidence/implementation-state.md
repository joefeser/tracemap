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



## Independent no-ACK review (2026-10-10)

Reviewed head `4490ea5bbffd07c98e8d346db2952d2c4efd3ccf` against base
`556cc4193c8584d82eb75f426c94efacd126858e`. Review and remediation are
owner-requested, outside ACK; no merge or bot retag is authorized by this work.

Confirmed P1/P2 repairs:

- Unknown/empty inner env declarations now mask parent literals; opaque
  env/defaults scope values fail closed instead of disappearing.
- Here-documents, multiline shell quotes, here-strings/block comments and
  substitutions produce run-level unsupported gaps rather than facts from data.
  Unmodeled YAML escapes and folded blocks retaining newlines are unsupported.
- Commands after both characters of `&&`/`||` are recognized. Inline option
  values no longer swallow the following property; MSBuild slash/single-long
  property spellings are recognized. Empty overrides invalidate prior literals,
  and grouped property lists cannot silently retain project fallback identity.
- FIFO opens are nonblocking and nonseekable handles are rejected before reads;
  attribute-read failures are caught as gaps, and CI-only partial coverage uses
  the package-evidence build-status explanation.
- Linked `.github` ancestors are rejected. Unix opens use held directory
  handles plus no-follow `openat` for every component; macOS ELOOP uses its
  actual value (62). Windows concurrent replacement remains a documented
  residual; this is not an atomic snapshot of a changing repository.
- CI facts now include generator/input SHA-256 fields. The input digest also
  binds the projected project identity inputs actually used for fallback.
- Dedupe keeps an intact occurrence, preferring one with known version when
  available; another command's version/project cannot ride the wrong span.

The initial 11 regression cases failed against the original head and passed
with remediation. Focused CI-producer/receipt validation passed 82/82 with warnings as errors.
The full Debug suite (`dotnet test src/dotnet/TraceMap.sln --no-restore
-warnaserror`) passed 3,558 tests, zero failures, one explicit Windows-only
skip (21m35s). The public CLI smoke produced one `Contoso.Sample` 0.1.0
CI fact with both provenance hashes, zero gaps, Level1SemanticAnalysis.
`git diff --check` passed. The public fixture remains
the only CLI smoke input; private corpus and authentic Windows validation are
deferred to their existing pinned hosted lanes.

### Hosted Windows test isolation follow-up

The `public-corpus` job for `db25f93a` failed its full terminal wizard replay
with `WEBFORMS_REVIEW_SOURCE_IDENTITY_CHANGED` (147 passed, 1 failed).
`WebFormsWizardCommandTests` lacked the Git-sensitive collection already used
by `WebFormsWizardExecutionTests`; exact-identity assertions were running
alongside the Git-heavy corpus. Apply the same existing nonparallel collection
to the terminal command tests. This addresses the documented probe-contention
risk without relaxing production identity validation or increasing timeouts.
The retained job log does not identify which individual Git probe failed.

Local wizard command/execution and GitMetadataOutput regression selection:
29 passed, zero failed/skipped, warnings as errors. Runtime code is unchanged;
the previous 3,558-pass full runtime validation remains prior-head evidence.
Hosted Windows validation must confirm this scheduling-only correction.

Hosted confirmation: Windows public-corpus run `38069213747` passed on
`4b79e06f` (148 passed, zero failed/skipped, both Windows publishing cases).

### Owner-requested assessment of the two runtime Baz findings

Both findings were confirmed and repaired: embedded shell quotes could split
an unsafe package id into a false safe prefix; workflow discovery materialized
all candidates before enforcing its cap. Shell operator/comment scanning now
honors embedded quotes and escapes consistently with tokenization. Discovery
retains at most 64 candidates, inspects at most 4,096 directory entries plus
one overflow witness, and sorts only after bounded collection succeeds.
Either overflow emits the same deterministic `ci-workflow-file-limit` gap and
no rows, avoiding filesystem-order-dependent producer evidence. Exclusions
remain honored within the absolute discovery bound.

Eight added regression cases cover embedded quotes/escaped operators, bounded
lazy enumeration, and enumeration-order-independent overflow. The combined
CI-producer/receipt/wizard/Git-output selection passed 119/119 with warnings as
errors; the public fixture CLI smoke passed with one CI fact and zero gaps.
The separate test-only persisted-artifact suggestion was not part of the
owner-requested two runtime findings. Full hosted checks for the new runtime
head remain separate from the prior-head full-suite and Windows evidence.
