# Legacy Remoting Flow Composition Tasks

## Implementation Tasks

- [ ] 1. Confirm input contracts and rule coverage. Requirements: 1, 3.
  - [ ] Re-read `.kiro/specs/legacy-remoting-detection/` and
        `.kiro/specs/legacy-flow-composition-reporting/` before implementation.
  - [ ] Confirm current Remoting fact type constants, rule IDs, extractor
        versions, and `supportingFactIds` property formats.
  - [ ] Confirm `legacy.flow.*` rules already cover availability, traversal,
        classification, gap propagation, redaction, and report output.
  - [ ] Add or update rule catalog limitations only if Remoting-specific
        composition behavior requires a documented rule change.
  - [ ] Do not add scanner facts or modify Remoting extraction unless a reader
        compatibility bug blocks composition.

- [ ] 2. Extend the legacy-flow reader for Remoting inputs. Requirements: 1, 4.
  - [ ] Read all existing `Remoting*` fact types from single and combined
        indexes where available.
  - [ ] Read Remoting-related `AnalysisGap` facts and preserve source rule IDs.
  - [ ] Detect missing Remoting fact availability in older indexes and emit
        `ExtractorUnavailable: legacy-remoting` or
        `SchemaMissing: legacy-remoting` gaps.
  - [ ] Preserve fact IDs, supporting fact IDs, rule IDs, evidence tiers, file
        spans, commit SHA, extractor versions, source labels, and coverage.
  - [ ] Parse `supportingFactIds` deterministically for semicolon-delimited,
        comma-delimited, empty, duplicate, and malformed mixed-delimiter values.
  - [ ] Add tests for old indexes, missing tables/columns, and read-only input
        behavior.

- [ ] 3. Add Remoting graph nodes and surface selectors. Requirements: 2, 4.
  - [ ] Add node kinds for `remoting-endpoint`, `remoting-registration`,
        `remoting-channel`, `remoting-object`, and `remoting-api`.
  - [ ] Add `--to-surface` support for `remoting-endpoint`,
        `remoting-registration`, and `remoting-channel`.
  - [ ] Support safe fact ID, safe type-name, and display-hash matching through
        existing exact/wildcard selector behavior.
  - [ ] Support `--surface-name <kind>-<hash-prefix>` as an exact generated
        display identity, and support wildcard display-hash matching only through
        the existing wildcard selector behavior, including collision cases that
        return all matches with ambiguity notes.
  - [ ] Keep source-local candidates separate and reject short-name stitching
        across sources.
  - [ ] Add selector tests for match, no-match, ambiguity, and combined-source
        separation.

- [ ] 4. Build conservative Remoting edges. Requirements: 2, 3.
  - [ ] Connect roots to Remoting facts only through existing call, object
        creation, symbol, parameter-forward, projection, fact-symbol, or
        supporting-fact evidence.
  - [ ] Use Remoting `supportingFactIds` to connect channel declaration to
        registration only when the source fact already supports it.
  - [ ] Do not connect client activation to service registration by URL hash,
        object URI hash, short type name, or config value alone.
  - [ ] Preserve independent facts and emit an unsupported-link gap when a
        possible Remoting relationship is visible but outside deterministic v1
        rules.
  - [ ] Add tests for supported channel links, unsupported links, and rejected
        hash/name-only stitching.

- [ ] 5. Implement terminal handling. Requirements: 2, 4.
  - [ ] Treat service registration, client registration, client activation,
        config service, and config client facts as `remoting-endpoint` or
        `remoting-registration` terminals.
  - [ ] Treat channel/config-channel/provider/API/object facts as intermediate
        evidence unless terminal precedence rules make them selected
        lower-precedence terminals.
  - [ ] Stop traversal at Remoting terminals; do not continue through
        service-side implementation or downstream evidence in this phase.
  - [ ] Keep WCF operation terminals unchanged and separate from Remoting
        terminals.
  - [ ] Add tests for terminal stopping behavior and mixed WCF/Remoting reports.
  - [ ] Add a test where one root reaches WCF and Remoting through distinct call
        chains and produces two separate path results with separate rule ID
        families.

- [ ] 6. Apply Remoting classification caps and gaps. Requirements: 3.
  - [ ] Cap all Remoting-terminal paths at `ProbableStaticPath` at strongest.
  - [ ] Cap syntax-only, name-only, object-shape-only, unlinked channel,
        high-fan-out, ambiguous, generated-code-uncertain, or reduced-coverage
        Remoting paths at `NeedsReviewStaticPath`, `ReducedCoverage`, or
        `AnalysisGap`.
  - [ ] Ensure Remoting evidence never produces `StrongStaticPath`.
  - [ ] Emit or propagate gaps for unsupported activated registration detail,
        dynamic registration, config includes, encrypted sections, transforms,
        factories, reflection, dependency injection, and machine.config.
  - [ ] Add tests proving classification caps and absence-vs-unavailable
        behavior.
  - [ ] Add tests for old indexes missing Remoting availability,
        full-availability zero-evidence notes, and Remoting `AnalysisGap`
        propagation.

- [ ] 7. Extend Markdown and JSON output safely. Requirements: 4, 5.
  - [ ] Add Remoting summary counts by node kind, fact type, rule ID, and
        classification.
  - [ ] Render representative Remoting static paths with safe labels, hashes,
        paths, line spans, facts, rules, tiers, coverage, and limitations.
  - [ ] Include Remoting gaps and limitations that deny runtime channel proof,
        object lifetime proof, process boundary proof, deployment proof, and
        endpoint reachability proof.
  - [ ] Preserve `legacy-flow.v1` if Remoting additions are additive; otherwise
        document and test any schema version change.
  - [ ] Add forbidden-wording tests for runtime Remoting overclaims.
  - [ ] Add byte-stable JSON tests where the same Remoting facts in different
        SQLite row order produce identical output.

- [ ] 8. Enforce redaction. Requirements: 5, 6.
  - [ ] Reuse existing safe display, hashing, source-label neutralization, and
        output guard helpers.
  - [ ] Suppress raw URLs, object URIs, ports, channel names/properties, config
        values, local absolute paths, private repo names, raw remotes, source
        snippets, connection strings, and secrets.
  - [ ] Avoid echoing unsafe selector values in logs and diagnostics.
  - [ ] Include `legacy.flow.redaction.v1` when flow output performs additional
        redaction.
  - [ ] Add tests for private source-label neutralization and safe hash-prefix
        display.
  - [ ] Add Markdown, JSON, logs, and display-field redaction tests.

- [ ] 9. Update validation docs and implementation state. Requirements: 6.
  - [ ] Update `docs/VALIDATION.md` only if Remoting flow composition changes
        validation workflow or smoke expectations.
  - [ ] Record implementation decisions, oddities, validation commands, smoke
        deferrals, and public claim level in this spec's
        `implementation-state.md`.
  - [ ] Explicitly defer public Remoting smoke baselines if none are available.
  - [ ] Keep generated scan/path artifacts out of git.

- [ ] 10. Validate implementation. Requirements: 6.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests`
  - [ ] `dotnet test src/dotnet/TraceMap.sln --filter LegacyRemotingExtractorTests`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] Relevant smoke checks from `docs/VALIDATION.md`, or explicit deferral
        with rationale.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

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
