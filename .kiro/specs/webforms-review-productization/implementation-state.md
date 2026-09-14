# Web Forms Review Productization Implementation State

Status: planned-with-documentation-front-door
Readiness: public-and-work-first
Branch: codex/vb-webforms-battle-test
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

## Next implementation slice

Implement and test `Initialize-FocusedWebFormsReview.ps1` and the persisted
config contract. The config must support three folder roots, multiple projects
per root, an explicit solution or projectless mode, actionable preflight errors,
and a single run receipt shared by subsequent stages.
