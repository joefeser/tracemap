# Legacy Remoting Flow Composition Implementation State

Status: spec-reviewed
Branch: codex/spec-legacy-remoting-flow-composition
Public claim level: hidden

## Scope

This is a spec-only branch for a conservative next phase that integrates
existing .NET Remoting evidence into the implemented legacy static flow/path
composition model.

Owned files:

- `.kiro/specs/legacy-remoting-flow-composition/requirements.md`
- `.kiro/specs/legacy-remoting-flow-composition/design.md`
- `.kiro/specs/legacy-remoting-flow-composition/tasks.md`
- `.kiro/specs/legacy-remoting-flow-composition/implementation-state.md`
- `.kiro/specs/legacy-remoting-flow-composition/review-prompts.md`

No source code, docs outside this spec, existing specs, generated outputs, or
site files are in scope for spec delivery.

## Scope Decisions

- Remoting is treated as sibling evidence to WCF, not as a subtype of WCF.
- Existing Remoting facts are inputs; no new scanner extraction is proposed.
- `tracemap paths --view legacy-flows` should display Remoting registrations,
  activations, config service/client declarations, channels, API usage, and
  `MarshalByRefObject` evidence as static nodes.
- Remoting registrations and activations are highest-precedence terminals;
  channel/API/object evidence is intermediate unless deterministic terminal
  precedence rules make it selected lower-precedence terminal evidence.
- Remoting terminals cap at `ProbableStaticPath` at strongest and cannot produce
  `StrongStaticPath`.
- `MarshalByRefObject` evidence remains object-shape evidence only.
- Client activation and service registration must not be stitched together by
  URL hash, object URI hash, short type name, or config value alone.
- No runtime channel proof, remote object lifetime proof, process boundary
  proof, deployment proof, endpoint reachability proof, exploitability claim, or
  production-usage claim is in scope.
- Public claim level remains hidden.

## Required Redaction

Generated flow artifacts, logs, and display fields must not include raw:

- Remoting URLs;
- object URIs;
- channel ports;
- channel names or provider properties when treated as config values;
- config values;
- local absolute paths;
- private repo names;
- raw remotes;
- source snippets;
- connection strings;
- secrets or secret-looking tokens.

Safe output should use fact IDs, rule IDs, evidence tiers, safe type names,
repo-relative paths, line spans, extractor versions, commit SHA, neutral source
labels, and existing value hashes.

## Validation Commands For Spec Delivery

```bash
node scripts/kiro-review.mjs --phase legacy-remoting-flow-composition --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000
./scripts/check-private-paths.sh
git diff --check
```

No .NET implementation validation is required for this spec-only branch unless
review patches touch source code, docs outside the spec, validation scripts, or
existing specs.

## Review State

Drafted locally. Sonnet spec review is required if the local Kiro wrapper and
auth are available. Blocking or Medium+ findings should be patched before final
commit. If the wrapper or auth is unavailable, record the gap here before
delivery.

Review results:

- Sonnet spec review completed with full coverage using:
  `node scripts/kiro-review.mjs --phase legacy-remoting-flow-composition --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000`.
- The review output reported stale branch metadata from the wrapper
  (`codex/legacy-sample-evidence-pack`), but `git branch --show-current`
  confirmed the working branch for delivery is
  `codex/spec-legacy-remoting-flow-composition`.
- Blocking findings patched: deterministic `supportingFactIds` parsing,
  terminal precedence, unavailable-vs-absent Remoting gap placement, hash
  selector format, and branch-state clarification.
- Important findings patched: WCF/Remoting output separation, permanent
  Remoting classification cap rationale, row-order byte-stability test, source
  label neutralization rules, and deferred service-side continuation rationale.
- No known blocking or Medium+ spec-review findings remain after these patches.

## Implementation Validation

Spec delivery validation completed:

- Sonnet spec review completed with full coverage and review findings patched.
- `./scripts/check-private-paths.sh` passed.
- `git diff --cached --check` passed for the staged spec files.
- .NET build/test not run because this branch adds only Kiro spec files and no
  source code, docs outside the spec, validation scripts, or existing specs.

Future implementation should run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests
dotnet test src/dotnet/TraceMap.sln --filter LegacyRemotingExtractorTests
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Relevant smoke guidance from `docs/VALIDATION.md`:

- Legacy Static Flow Reporting Smoke.
- Legacy Remoting Smoke.

No pinned public Remoting smoke baseline exists at spec drafting time. Public
Remoting baselines require a separate reviewed baseline task or spec.

## Follow-Ups To Keep Out Of This Slice

- Source-code implementation.
- Public site copy or claim promotion.
- Public Remoting smoke baseline.
- Runtime Remoting probing or deployment inspection.
- Reducer-specific contract-change conclusions.
