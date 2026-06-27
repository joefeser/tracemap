# UI Field Property Lineage Terminal Context Coverage Tasks

Status: ready-for-implementation
Readiness: validated-spec-only

## Spec-Only PR Scope

- [x] Create `.kiro/specs/ui-field-property-lineage-terminal-context-coverage/`.
- [x] Fetch `origin/dev` and base the branch on current `dev`.
- [x] Inspect `PropertyFlowReport.cs`.
- [x] Inspect `CombinedTerminalSurfaceKinds.cs`.
- [x] Inspect current `PropertyFlowTests.cs` terminal-context coverage.
- [x] Inspect existing Kiro specs around `ui-field-property-lineage` terminal
  context.
- [x] Draft requirements, design, tasks, implementation state, and review
  prompts.
- [x] Run Kiro spec review with Opus, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Run Kiro spec review with Sonnet, or record exact unavailable/tool/timeout
  evidence in `implementation-state.md`.
- [x] Patch Medium+ actionable findings; patch Low findings only when narrow
  and safe.
- [x] Run one bounded re-review if feasible and record it.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm diff is limited to this spec folder.
- [x] Commit the spec branch.
- [x] Push the branch and open a PR to `dev`.
- [x] Wait 3 minutes, then run ACK PR loop.
- [x] Follow ACK-authorized actions only; do not manually tag review bots, do
  not force-push, and do not manually merge outside the authorized
  auto-merge command.

## PR 1: Terminal Vocabulary And Bridge Coverage

- [ ] 1. Add closed-vocabulary bucket tests.
  Requirements: 1, 4, 5.
  - [ ] Enumerate `CombinedTerminalSurfaceKinds.All` in a direct mapping-level
    table test.
  - [ ] If needed, extract a tiny internal mapping helper or make the current
    mapper internal so tests can cover vocabulary without scanner-like
    fixtures for every surface kind.
  - [ ] Assert `sql-query` and `sql-persistence` map to
    `data-surface terminal context` after an exact selected-property bridge.
  - [ ] Assert `legacy-data` maps to `legacy-data terminal context`.
  - [ ] Assert `package-config` maps to `package/config terminal context`.
  - [ ] Assert every current `message-*` value maps to
    `message-surface terminal context`, including `message-unknown`.
  - [ ] Assert every current `asmx-*`, `remoting-*`, and exact `wcf-operation`
    value maps to `legacy-communication terminal context`.
  - [ ] Assert `asmx-config` and `asmx-metadata` remain
    `legacy-communication terminal context`, not package/config context.
  - [ ] Assert future `wcf-*` values do not implicitly inherit the exact
    `wcf-operation` decision without a mapped, suppressed, or deferred
    vocabulary decision.
  - [ ] Assert an out-of-vocabulary probe such as `wcf-service` maps to
    `dependency-surface terminal context`, not legacy-communication context.
  - [ ] Assert the literal `dependency-surface` surface kind maps to
    `dependency-surface terminal context`.
  - [ ] Assert remaining non-HTTP closed-vocabulary values map to
    `dependency-surface terminal context` unless a more specific bucket is
    documented.
  - [ ] Assert `http-route` and `http-client` do not render property-flow
    terminal context under this slice even when an exact selected-property
    bridge is present.
  - [ ] Make the test fail closed when a new combined terminal surface kind is
    added without a mapped, suppressed, or deferred property-flow decision.
  - [ ] Add representative full report fixtures for selected buckets; do not
    require one full fixture per surface kind.

- [ ] 2. Add exact selected-property bridge tests.
  Requirements: 2, 4, 5.
  - [ ] Cover selected combined fact ID as an exact bridge.
  - [ ] Isolate selected combined fact ID in at least one fixture when
    practical so the fixture does not pass through a simultaneous symbol
    bridge.
  - [ ] Cover selected symbol ID or selected target symbol as an exact bridge.
  - [ ] Cover selected member/target symbol metadata as an exact bridge.
  - [ ] Cover selected type-qualified model property as an exact bridge.
  - [ ] Cover selected type-qualified DTO property as an exact bridge.
  - [ ] Cover selected type-qualified UI/model binding display where fixture
    data can represent it without scanner changes.
  - [ ] Assert terminal context does not upgrade path classification beyond the
    weakest required evidence.
  - [ ] Record any bridge family that cannot be represented in current tests
    as deferred, with reason and risk.

- [ ] 3. Add proximity-only negative tests.
  Requirements: 2, 3, 5.
  - [ ] Same method proximity alone does not attach terminal context.
  - [ ] Same endpoint or route proximity alone does not attach terminal
    context.
  - [ ] Same class proximity alone does not attach terminal context.
  - [ ] Same file proximity alone does not attach terminal context.
  - [ ] Same namespace, folder, or project proximity alone does not attach
    terminal context when represented in fixture data.
  - [ ] Same short property name alone does not attach terminal context.
  - [ ] Same short symbol or method name alone does not attach terminal
    context.
  - [ ] Current property-flow generic selected names (`id`, `name`, `type`,
    `value`, `state`, `status`) do not attach terminal context without exact
    selected-property identity.
  - [ ] Broader route-flow/high-fan-out generic examples such as `result` or
    `response` require an explicit compatibility decision before they are
    added to property-flow terminal-context coverage.
  - [ ] Case-boundary behavior for selected symbols is explicitly tested or
    documented as a deferred known boundary.
  - [ ] Broad endpoint dependency, route-flow context group, touched file,
    touched symbol, or dependency-surface inventory evidence does not attach
    terminal context without the selected-property bridge.
  - [ ] Every negative test asserts no `StaticTerminalContext` note and no
    `terminalContextKind` safe metadata.

- [ ] 4. Lock rule-catalog and report-version decisions.
  Requirements: 4, 5.
  - [ ] Assert emitted path/surface rule IDs resolve in `rules/rule-catalog.yml`.
  - [ ] Assert terminal-context notes and `terminalContextKind` metadata remain
    additive presentation over existing path/source rules, not a new rule-less
    conclusion.
  - [ ] Reuse existing rules when terminal context remains additive note/safe
    metadata over existing combined path evidence.
  - [ ] Add catalog entries, limitations, and tests before emitting any new
    terminal-context rule ID or gap code.
  - [ ] Keep report version `1.0` only if metadata remains additive and safely
    ignorable.
  - [ ] Add a report-version pin asserting `report.Version == "1.0"` when the
    chosen decision is additive metadata without a version bump.
  - [ ] Bump report version if terminal context becomes required or changes
    existing path, node, edge, inventory, gap, summary, or Markdown semantics.
  - [ ] Record the final rule/version decision in implementation state before
    product edits.
  - [ ] Assert terminal-context notes and `terminalContextKind` metadata do not
    include raw SQL, snippets, literal values, connection strings, raw URLs,
    local paths, remotes, hostnames, credentials, secrets, or private sample
    names.
  - [ ] Assert deterministic note ordering when `StaticRouteFlowContext` and
    `StaticTerminalContext` coexist.

- [ ] 5. Validate PR 1.
  Requirements: 6.
  - [ ] Run focused `PropertyFlowTests`.
  - [ ] Run focused route-flow/path/reverse/export tests if touched.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln` unless explicitly deferred
    with reason and risk.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] If a required validation script is missing or non-executable, record
    exact evidence and label validation partial.
  - [ ] Update implementation state with validation evidence, deferred checks,
    and readiness.

## Deferred Follow-Ups

- Add broader terminal-context families only after the coverage harness lands.
- Add new scanner extraction only through a separate scanner spec.
- Resolve any deferred case-boundary bridge decision before adding PR 2 or
  later bridge-family expansion.
- Add route-flow/path/reverse terminal composition only when exact
  selected-property bridge rules are documented.
- Add public site copy or public claims only through a separate public-copy
  spec.
- Add runtime/browser/live HTTP/database/dependency validation only as
  non-upgrading metadata in a separate spec.
- AI/LLM classification, embeddings, vector databases, and prompt-based
  analysis remain out of scope.
