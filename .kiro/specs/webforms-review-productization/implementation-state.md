# Web Forms Review Productization Implementation State

Record type: historical implementation and validation record

Status: clean-run pipeline present on current `dev`; PR #770 is authoritative
for terminal reachability

Historical branch: `codex/vb-webforms-battle-test` (retained research only; do
not merge wholesale)

Public claim level: hidden

## Completed foundation

- Full application workbench with compact application index and per-page
  evidence handoffs.
- Explicit `P / F / S` call accounting, normalized source sites, evidence
  ceilings, omissions, gaps, and incomplete-chain states.
- Private application-index rows expose the full retained route and bounded
  control ID/type projection beneath each alias; shareable outlier artifacts
  remain alias/count only.
- Compiler-backed technology-family projections with syntax fallback retained
  separately.
- C# and VB.NET code-behind support, plus projectless reduced-coverage handling.
- Private versus alias-only shareable output boundary.
- Generator and bounded-input provenance hashes for current handoff artifacts.
- WITS immutable human-review overlay and supplemental exceptional-handler
  review set.

## Aggregate validation checkpoint

An authorized identity-free stress run completed 476 selected surfaces. It
demonstrated that projection/fact/site counts can differ, that evidence ceilings
and omissions need first-class reporting, and that repeated coverage gaps must
be triaged as possible extractor limitations before being treated as hundreds
of application issues. Private source identities and paths are intentionally
not recorded here.

## Current decisions

- Public documentation and work-machine agent ingestion are higher priority
  than external ticket automation.
- The VB.NET battle-test guide remains a validation appendix, not the primary
  onboarding path.
- The existing scripts remain supported while a setup/config/orchestration
  layer is added above them.
- Agent prompts consume retained evidence and produce interpretations; they do
  not become scanner rules or scanner facts.
- External ticket creation begins with an alias-only dry run and explicit
  approval pipeline.

## Implemented clean-run workflow

- `Initialize-FocusedWebFormsReview.ps1` creates one empty review root, a
  seven-setting config, logs directory, and local retention README.
- `Invoke-FocusedWebFormsPipeline.ps1` runs build, scan, all/selected-page
  packet composition, evidence-docs export, and application workbench creation.
- `run-receipt.json` binds the run to the config hash, pipeline generator hash,
  source commit, TraceMap commit, exact artifact paths, sizes, and hashes.
- Resume validates every completed artifact and reuses only an identical run;
  it does not select folders by timestamp.
- Project selection supports explicit solution, explicit projects, bounded
  discovery under the three configured roots, and explicit projectless mode.
- Wrong folders identify the requested value and source root. Wrong solution
  and project paths include bounded in-scope candidates.
- Config loading rejects unescaped Windows backslashes before JSON parsing,
  including sequences such as `\t` that JSON would otherwise accept and alter.
  The failure directs operators to the unambiguous `C:/...` form.
- First-run recovery guidance distinguishes an empty solution intersection from
  empty bounded discovery, routes Web Site checkouts to explicit projectless
  mode, and limits failed-receipt removal to runs with no completed retained
  stage.
- Retained artifact hashing is bounded at 16 GiB. A narrowly guarded migration
  promotes an otherwise complete scan that failed only at the former 2 GiB
  receipt limit, preserves the prior TraceMap commit and generator hash in the
  migration record, and continues downstream stages without rescanning.
- C# call-edge producers now label compiler-resolved evidence
  `bounded-semantic-callgraph` and syntax fallback `syntax-only`. This prevents
  otherwise valid retained calls from being misreported as
  `EvidenceCoverageLabelUnavailable` packet gaps.
- `Export-FocusedWebFormsPageShareable.ps1` validates the completed workbench
  receipt, resolves one page alias, and emits one explicitly named shareable
  JSON/ZIP pair. The projection keeps anonymous chain/endpoint/handler/site/
  callee equality and bounded technology/boundary shape signals while omitting
  paths, symbols, URLs, spans, source/scan/commit identity, raw evidence IDs,
  human comments, and the private handoff fingerprint.
- Compiler-resolved C# calls through local helpers now join generated WCF
  client operations by canonical call-target identity. Web Forms traversal
  stops at the external `wcf-operation` boundary and does not claim behavior
  inside the remote service or any downstream database.

## Validation checkpoint

- Focused setup/config, launcher, and application-workbench PowerShell suites
  pass.
- Setup/config tests pin actionable failures for both invalid and silently
  parseable single-backslash Windows paths.
- One-project C#, mixed multi-project C#/VB.NET, discover, projectless, all-page,
  and selected-page config contracts are pinned.
- Folder discovery retains C# and VB.NET project files beneath the three roots
  and excludes an unrelated fourth root.
- Focused C# semantic, syntax, and Web Forms packet tests pin explicit call
  coverage labels and pass.
- A synthetic public regression pins `AJAX -> ASHX -> local helper -> generated
  WCF proxy` composition, the external WCF terminal, and the absence of an
  inferred database boundary.
- The application-workbench regression pins per-page shareable provenance,
  shared endpoint/handler equality, normalized-site structure, ZIP contents,
  structural signals, and a denylist of private fixture identities.
- An external selected-page C# validation retained identical call accounting
  before and after the producer-label correction: 43 pages, 3,999
  chain-associated projections, 3,798 unique call facts, and 1,943 normalized
  source sites. `EvidenceCoverageLabelUnavailable` fell from 3,798 to zero and
  the alias-only summary reported zero remaining packet gaps. No source paths,
  symbols, repository identity, or private input fingerprints were retained in
  this checkpoint.
- A clean projectless VB.NET Web Forms fixture completed scan, packet,
  evidence-docs, and workbench publication. A second invocation reused all five
  stages under the original run ID after verifying receipt provenance and
  artifact hashes.

## Remaining follow-up

Consolidate the long manual compatibility reference only after its diagnostic
and recovery entry points have equivalent behavioral tests. Ticket automation
and licensing remain deliberately separate private follow-up work.
