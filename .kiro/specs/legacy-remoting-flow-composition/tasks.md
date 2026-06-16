# Legacy Remoting Flow Composition Tasks

## Implementation Tasks

- [x] 1. Confirm input contracts and rule coverage. Requirements: 1, 3.
  - [x] Re-read `.kiro/specs/legacy-remoting-detection/` and
        `.kiro/specs/legacy-flow-composition-reporting/` before implementation.
  - [x] Confirm current Remoting fact type constants, rule IDs, extractor
        versions, and `supportingFactIds` property formats.
  - [x] Confirm `legacy.flow.*` rules already cover availability, traversal,
        classification, gap propagation, redaction, and report output.
  - [x] Add or update rule catalog limitations only if Remoting-specific
        composition behavior requires a documented rule change.
  - [x] Do not add scanner facts or modify Remoting extraction unless a reader
        compatibility bug blocks composition.

- [x] 2. Extend the legacy-flow reader for Remoting inputs. Requirements: 1, 4.
  - [x] Read all existing `Remoting*` fact types from single and combined
        indexes where available.
  - [x] Read Remoting-related `AnalysisGap` facts and preserve source rule IDs.
  - [x] Detect missing Remoting fact availability in older indexes and emit
        `ExtractorUnavailable: legacy-remoting` or
        `SchemaMissing: legacy-remoting` gaps.
  - [x] Preserve fact IDs, supporting fact IDs, rule IDs, evidence tiers, file
        spans, commit SHA, extractor versions, source labels, and coverage.
  - [x] Parse `supportingFactIds` deterministically for semicolon-delimited,
        comma-delimited, empty, duplicate, and malformed mixed-delimiter values.
  - [x] Add tests for old indexes, missing tables/columns, and read-only input
        behavior.

- [x] 3. Add Remoting graph nodes and surface selectors. Requirements: 2, 4.
  - [x] Add node kinds for `remoting-endpoint`, `remoting-registration`,
        `remoting-channel`, `remoting-object`, and `remoting-api`.
  - [x] Add `--to-surface` support for `remoting-endpoint`,
        `remoting-registration`, and `remoting-channel`.
  - [x] Support safe fact ID, safe type-name, and display-hash matching through
        existing exact/wildcard selector behavior.
  - [x] Support `--surface-name <kind>-<hash-prefix>` as an exact generated
        display identity, and support wildcard display-hash matching only through
        the existing wildcard selector behavior, including collision cases that
        return all matches with ambiguity notes.
  - [x] Keep source-local candidates separate and reject short-name stitching
        across sources.
  - [x] Add selector tests for match, no-match, ambiguity, and combined-source
        separation.

- [x] 4. Build conservative Remoting edges. Requirements: 2, 3.
  - [x] Connect roots to Remoting facts only through existing call, object
        creation, symbol, parameter-forward, projection, fact-symbol, or
        supporting-fact evidence.
  - [x] Use Remoting `supportingFactIds` to connect channel declaration to
        registration only when the source fact already supports it.
  - [x] Do not connect client activation to service registration by URL hash,
        object URI hash, short type name, or config value alone.
  - [x] Preserve independent facts and emit an unsupported-link gap when a
        possible Remoting relationship is visible but outside deterministic v1
        rules.
  - [x] Add tests for supported channel links, unsupported links, and rejected
        hash/name-only stitching.

- [x] 5. Implement terminal handling. Requirements: 2, 4.
  - [x] Treat service registration, client registration, client activation,
        config service, and config client facts as `remoting-endpoint` or
        `remoting-registration` terminals.
  - [x] Treat channel/config-channel/provider/API/object facts as intermediate
        evidence unless terminal precedence rules make them selected
        lower-precedence terminals.
  - [x] Stop traversal at Remoting terminals; do not continue through
        service-side implementation or downstream evidence in this phase.
  - [x] Keep WCF operation terminals unchanged and separate from Remoting
        terminals.
  - [x] Add tests for terminal stopping behavior and mixed WCF/Remoting reports.
  - [x] Add a test where one root reaches WCF and Remoting through distinct call
        chains and produces two separate path results with separate rule ID
        families.

- [x] 6. Apply Remoting classification caps and gaps. Requirements: 3.
  - [x] Cap all Remoting-terminal paths at `ProbableStaticPath` at strongest.
  - [x] Cap syntax-only, name-only, object-shape-only, unlinked channel,
        high-fan-out, ambiguous, generated-code-uncertain, or reduced-coverage
        Remoting paths at `NeedsReviewStaticPath`, `ReducedCoverage`, or
        `AnalysisGap`.
  - [x] Ensure Remoting evidence never produces `StrongStaticPath`.
  - [x] Emit or propagate gaps for unsupported activated registration detail,
        dynamic registration, config includes, encrypted sections, transforms,
        factories, reflection, dependency injection, and machine.config.
  - [x] Add tests proving classification caps and absence-vs-unavailable
        behavior.
  - [x] Add tests for old indexes missing Remoting availability,
        full-availability zero-evidence notes, and Remoting `AnalysisGap`
        propagation.

- [x] 7. Extend Markdown and JSON output safely. Requirements: 4, 5.
  - [x] Add Remoting summary counts by node kind, fact type, rule ID, and
        classification.
  - [x] Render representative Remoting static paths with safe labels, hashes,
        paths, line spans, facts, rules, tiers, coverage, and limitations.
  - [x] Include Remoting gaps and limitations that deny runtime channel proof,
        object lifetime proof, process boundary proof, deployment proof, and
        endpoint reachability proof.
  - [x] Preserve `legacy-flow.v1` if Remoting additions are additive; otherwise
        document and test any schema version change.
  - [x] Add forbidden-wording tests for runtime Remoting overclaims.
  - [x] Add byte-stable JSON tests where the same Remoting facts in different
        SQLite row order produce identical output.

- [x] 8. Enforce redaction. Requirements: 5, 6.
  - [x] Reuse existing safe display, hashing, source-label neutralization, and
        output guard helpers.
  - [x] Suppress raw URLs, object URIs, ports, channel names/properties, config
        values, local absolute paths, private repo names, raw remotes, source
        snippets, connection strings, and secrets.
  - [x] Avoid echoing unsafe selector values in logs and diagnostics.
  - [x] Include `legacy.flow.redaction.v1` when flow output performs additional
        redaction.
  - [x] Add tests for private source-label neutralization and safe hash-prefix
        display.
  - [x] Add Markdown, JSON, logs, and display-field redaction tests.

- [x] 9. Update validation docs and implementation state. Requirements: 6.
  - [x] Update `docs/VALIDATION.md` only if Remoting flow composition changes
        validation workflow or smoke expectations.
  - [x] Record implementation decisions, oddities, validation commands, smoke
        deferrals, and public claim level in this spec's
        `implementation-state.md`.
  - [x] Explicitly defer public Remoting smoke baselines if none are available.
  - [x] Keep generated scan/path artifacts out of git.

- [x] 10. Validate implementation. Requirements: 6.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests`
  - [x] `dotnet test src/dotnet/TraceMap.sln --filter LegacyRemotingExtractorTests`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] Relevant smoke checks from `docs/VALIDATION.md`, or explicit deferral
        with rationale.
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

## Suggested PR Boundaries

- PR 1: Reader support, node/surface model, and selector tests.
- PR 2: Edge construction, terminal handling, classification caps, and mixed
  WCF/Remoting fixtures.
- PR 3: Markdown/JSON output, redaction guards, docs, and full validation.

## Deferred Follow-Ups

- Public Remoting smoke baseline and claim promotion.
- Service-side continuation from Remoting registration to implementation
  methods.
- Reducer-specific contract-change integration.
- Machine.config, config transform, encrypted section, and external include
  support.
- Richer activated-registration overload modeling.
- Visual graph UI.
