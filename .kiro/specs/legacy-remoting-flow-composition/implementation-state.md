# Legacy Remoting Flow Composition Implementation State

Status: implementation-complete
Branch: codex/implement-legacy-remoting-flow-composition
Public claim level: hidden

## Scope

This implementation integrates existing .NET Remoting facts into
`tracemap paths --view legacy-flows` and `--include-legacy-roots` as static,
evidence-backed flow composition. It does not add new scanner extraction,
runtime Remoting calls, endpoint probing, deployment inspection, LLM calls,
embeddings, vector databases, or prompt-based classification.

Implemented files:

- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs`
- `src/dotnet/tests/TraceMap.Tests/LegacyFlowCompositionTests.cs`
- `.kiro/specs/legacy-remoting-flow-composition/tasks.md`
- `.kiro/specs/legacy-remoting-flow-composition/implementation-state.md`

## Scope Decisions

- Remoting remains a sibling evidence family to WCF. WCF operation terminals and
  Remoting terminals use distinct node kinds, surface kinds, fact types, rule
  IDs, and limitations.
- Existing `Remoting*` facts are consumed from single or combined indexes; no
  scanner facts or extractor behavior were added.
- Remoting endpoint/registration/channel/object/API nodes are projected only
  through existing deterministic symbol, call, object-creation, projection, or
  supporting-fact evidence. URL hashes, object URI hashes, config values, and
  short names are not used to stitch client and service facts together.
- `remoting-endpoint`, `remoting-registration`, and `remoting-channel` are the
  primary selector surfaces. `remoting-object` and `remoting-api` are explicit
  review-tier selector surfaces for object-shape/API evidence.
- Remoting terminal paths are capped at `ProbableStaticPath` at strongest.
  Syntax-only, channel-only, API-only, and `MarshalByRefObject` object-shape
  paths are capped at `NeedsReviewStaticPath` or lower.
- Remoting channel `supportingFactIds` parsing accepts semicolon or comma
  delimiter formats, de-duplicates and sorts IDs, and emits
  `MalformedSupportingFactIds` when mixed delimiters appear.
- Current C# indexes with no Remoting facts emit an explicit
  `NoRemotingEvidenceFound` availability note/gap. Older indexes whose Remoting
  support cannot be proven emit `SchemaMissing` under
  `legacy.flow.input-availability.v1`.
- No public site copy or public claim promotion is included. Public claim level
  remains hidden.

## Redaction

Remoting flow output displays safe type names, fact IDs, rule IDs, evidence
tiers, repository-relative paths, line spans, and stable hash prefixes such as
`url-0123abcd` or `objectUri-abcdef12`.

Generated Markdown, JSON, logs, and display fields must not include raw:

- Remoting URLs or object URIs;
- channel ports, raw channel names, or provider/config values;
- local absolute paths, private source labels, raw remotes, or private repo
  names;
- source snippets, connection strings, secrets, or secret-looking tokens.

The implementation reuses legacy flow safe-display, safe-path, source-label
neutralization, and redaction limitation behavior.

## Tests Added

Focused tests cover:

- WebForms root to Remoting config endpoint composition and static cap.
- Client activation selector matching through safe URL hash display.
- Remoting channel `supportingFactIds` parsing, source scoping, and malformed
  mixed delimiter gaps.
- `MarshalByRefObject` object-shape selected paths capped at review tier.
- WCF and Remoting terminal separation in the same legacy flow report.
- Hash-only client activation/service declaration non-stitching.
- Older-index Remoting availability gaps, current-index zero Remoting evidence
  notes, and Remoting `AnalysisGap` propagation.
- Markdown/JSON redaction and forbidden runtime-overclaim wording.

## Review State

Sonnet implementation review was run with:

```bash
node scripts/kiro-review.mjs --phase legacy-remoting-flow-composition --kind implementation --model claude-sonnet-4.5 --fresh --timeout-ms 600000
```

Review coverage was reduced because the wrapper reported denied tool access for
one attempted command, but the review completed and returned actionable
findings.

Findings addressed:

- Blocking: stale implementation-state metadata replaced with this
  implementation record.
- Blocking: `tasks.md` checkboxes updated to reflect completed implementation.
- Important: added client/server same-hash non-stitching test.
- Important: added `MarshalByRefObject` object-shape cap test and report note.
- Important: added current-index zero Remoting evidence availability test.
- Important: added forbidden Remoting runtime-overclaim wording assertions.
- Medium: recorded single-PR delivery scope and validation results here.

No new rule IDs were required. Existing `legacy.flow.*` rules cover input
availability, traversal, classification, gap propagation, redaction, and report
output. Source Remoting rule IDs remain `legacy.remoting.*`.

## Validation

Completed validation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests
dotnet test src/dotnet/TraceMap.sln --filter LegacyRemotingExtractorTests
dotnet test src/dotnet/TraceMap.sln
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/dotnet-remoting-sample --out <tmp>/dotnet-remoting-scan
./scripts/smoke-combined-paths.sh
```

Initial `./scripts/smoke-combined-paths.sh` run found missing TypeScript npm
dependencies (`tsc` unavailable). Homebrew discovery confirmed TypeScript was
not installed as a formula, so `npm install --prefix src/typescript` restored
the local adapter dependencies and the smoke then passed.

Final pre-PR validation also runs:

```bash
./scripts/check-private-paths.sh
git diff --check
```

No pinned public Remoting smoke baseline exists yet. Public Remoting baselines
and public claim promotion remain deferred to a separately reviewed task.

## Follow-Ups

- Public Remoting smoke baseline and claim promotion.
- Service-side continuation from Remoting registration into implementation
  methods, only after explicit scanner facts and traversal rules exist.
- Reducer-specific contract-change integration beyond static path context.
- Machine.config, config transforms, encrypted sections, and external config
  include resolution.
- Richer activated-registration overload modeling.
