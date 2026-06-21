# Event And Message Dependency Surfaces Implementation State

Status: implemented-v1-with-follow-ups

Post-promotion note: PR #225 implemented the first event/message surface slice
and PR #247 promoted it to `main`. Remaining unchecked task items are follow-up
depth, not current unmerged work.

## Current Follow-Up Slice

- Branch: `codex/implement-event-message-surfaces-followup`
- Base: `origin/dev`
- Selected scope: message direction filtering for `paths` and `reverse`.
- Implemented:
  - Added explicit `--message-direction publish|consume|bind|declare|all`
    support for `tracemap paths` and `tracemap reverse`.
  - Carried `operationDirection` from combined message surface rows into path
    graph/report nodes.
  - Filtered only message surfaces by direction; non-message surfaces are not
    affected by the selector.
  - Removed the old `direction-filter-not-supported` gap for selected message
    surfaces because the filter is now implemented.
  - Recorded the selected direction in Markdown/JSON query metadata.
  - Added focused tests for publish/consume filtering and invalid direction
    validation.
  - Added follow-up tests for `all` direction behavior and `declare`
    direction filtering.
  - Moved message-direction normalization into `CombinedReportHelpers` after
    PR-loop feedback flagged duplicate helpers in paths and reverse reporters.
  - Tightened the `declare` direction test fixture after PR-loop feedback so
    the fixture creates a queue declare surface before asserting selected
    declare surfaces.
  - Updated `paths` and `reverse` CLI help after Qodo feedback so documented
    surface-kind selectors include supported `message-*` kinds.
- Still out of scope:
  - Route-flow async message-hop rendering.
  - Reducer context over message surfaces.
  - Roslyn semantic Tier1 message extraction.
  - TypeScript, Python, and JVM message adapter slices.
- Validation so far:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`: 8 passed, 0 failed.
  - `dotnet build src/dotnet/TraceMap.sln`: passed.
  - `dotnet test src/dotnet/TraceMap.sln`: 585 passed, 0 failed.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.
  - After PR-loop duplicate-helper fix:
    `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`,
    `dotnet build src/dotnet/TraceMap.sln`,
    `dotnet test src/dotnet/TraceMap.sln`,
    `./scripts/check-private-paths.sh`, and `git diff --check` passed.
  - After PR-loop declare-fixture fix:
    `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`,
    `dotnet build src/dotnet/TraceMap.sln`,
    `dotnet test src/dotnet/TraceMap.sln`,
    `./scripts/check-private-paths.sh`, and `git diff --check` passed.
  - After Qodo CLI-help fix:
    `dotnet build src/dotnet/TraceMap.sln`,
    `dotnet test src/dotnet/TraceMap.sln`,
    `./scripts/check-private-paths.sh`, and `git diff --check` passed.
- Review:
  - Sonnet implementation review artifact:
    `.tmp/kiro-reviews/event-message-surfaces/2026-06-21T204944-520Z-implementation-claude-sonnet-4.6.clean.md`.
    Coverage was reduced due denied tool access and findings largely reviewed
    the prior V1 scope.
  - Opus re-review artifact:
    `.tmp/kiro-reviews/event-message-surfaces/2026-06-21T205351-261Z-re-review-claude-opus-4.8.clean.md`.
    Coverage was reduced due denied tool access. Opus found no blocking issues
    for this follow-up slice and requested slice-local `all`/`declare`
    direction tests; those tests were added.

## Branch

- `codex/implement-event-message-surfaces`
- Base: `origin/dev`

## Completed Scope

- Added shared message fact vocabulary constants for:
  - `MessagePublisherSurface`
  - `MessageConsumerSurface`
  - `MessageBindingDeclared`
  - message-specific `AnalysisGap` rows under `message.surface.gap.v1`
- Added `message.surface.*` rule catalog entries with limitations for publisher,
  consumer, binding, identity, combine, candidate edge, paths, reducer, and gap
  behavior before emitting new facts.
- Updated `docs/LANGUAGE_ADAPTER_CONTRACT.md` with shared message surface kinds,
  safe metadata fields, destination identity rules, full SHA-256 hash
  requirements, and the decision that `handlerSymbolId` and
  `publisherSymbolId` remain properties-only metadata in this slice.
- Added deterministic destination identity helpers for safe normalized keys,
  full 64-character lowercase SHA-256 destination hashes, safe metadata hashes,
  and stable message surface keys.
- Added .NET syntax/static extraction for selected message surfaces in the
  existing C# integration syntax pass:
  - Kafka-like `Produce` / `ProduceAsync`
  - Dapr `PublishEventAsync`
  - queue-style `SendToQueue`, `QueueMessageAsync`, `Enqueue`, `EnqueueAsync`
  - RabbitMQ-like `BasicPublish`, `QueueDeclare`, `ExchangeDeclare`
  - external message-bus-looking `Publish`, `Send`, `Subscribe`, `Consume`,
    `Receive`, `BasicConsume`, `OnMessage`
  - Azure Functions-style `QueueTrigger`, `ServiceBusTrigger`,
    `EventHubTrigger`, `QueueOutput`, `ServiceBusOutput`, `TimerTrigger`
  - Dapr `Topic` attribute
- Added conservative gap behavior for visible message calls or attributes whose
  destination identity is dynamic, missing, or unsafe.
- Explicitly excludes MediatR-like in-process `Publish`/`Send` calls from
  broker-backed message surface facts.
- Projected message surfaces into combined dependency surface rows and JSON
  fields.
- Added report-level `message-publish-consume` candidate edges only for
  publisher and consumer surfaces that share the same safe static destination
  identity and surface kind.
- Allowed message surface kinds in `paths` and `reverse` surface selectors.
- Rejected `message-publish-consume` as a surface-kind selector in paths and
  reverse because it is an edge kind.
- Added focused tests in `MessageSurfaceTests` for extraction, gaps, redaction,
  stable-key behavior across handler rename, combined report projection,
  candidate edge rows, paths/reverse selectors, edge-kind rejection, and full
  unsafe destination hashes.

## Open Scope / Deferrals

- Roslyn semantic Tier1 message extraction is not implemented in this slice;
  .NET message extraction is syntax/static structural evidence.
- Reducer context over message surfaces is documented in the rule catalog but
  not wired into reducer classification yet.
- Route-flow async message-hop rendering is not implemented.
- TypeScript, Python, and JVM message adapters remain separate future slices.
- Report schema version was not bumped; message fields are additive on existing
  combined report rows.
- No runtime broker, topology, delivery, traffic, live subscription, schema
  registry, LLM, embedding, vector database, or prompt-classification behavior
  was added.

## Validation

- `dotnet build src/dotnet/TraceMap.sln` passed on 2026-06-20.
- Focused tests passed on 2026-06-20:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "MessageSurfaceTests|IntegrationExtractorTests|CombinedDependencyReportTests|CombinedDependencyPathTests|CombinedReverseQueryTests"`
- Synthetic CLI smoke passed on 2026-06-20:
  - scan publisher fixture
  - scan consumer fixture
  - combine
  - report
  - paths with `--to-surface message-stream`
  - reverse with `--surface message-stream --to sources`
- First full `dotnet test src/dotnet/TraceMap.sln` run had one transient
  `BuildEnvironmentDiagnosticTests.Cli_restore_failure_artifacts_are_sanitized`
  failure looking for `NuGetRestoreFailed`; the same test passed immediately
  when rerun in isolation.
- Full `dotnet test src/dotnet/TraceMap.sln` rerun passed on 2026-06-20:
  521 passed, 0 failed.
- `./scripts/check-private-paths.sh` passed on 2026-06-20.
- `git diff --check` passed on 2026-06-20.
- After Kiro review patches, final validation passed on 2026-06-20:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`: 6 passed, 0 failed.
  - `dotnet test src/dotnet/TraceMap.sln`: 522 passed, 0 failed.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.
  - `dotnet build src/dotnet/TraceMap.sln`: passed.
- After first PR-loop review-thread fixes, validation passed on 2026-06-20:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`: 6 passed, 0 failed.
  - `dotnet test src/dotnet/TraceMap.sln`: 522 passed, 0 failed.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.
  - `dotnet build src/dotnet/TraceMap.sln`: passed.
- After second PR-loop review-thread fixes, validation passed on 2026-06-20:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`: 7 passed, 0 failed.
  - `dotnet build src/dotnet/TraceMap.sln`: passed.
  - `dotnet test src/dotnet/TraceMap.sln`: 523 passed, 0 failed.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.
- After fresh Codex PR-loop findings, validation passed on 2026-06-20:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter MessageSurfaceTests`: 8 passed, 0 failed.
  - `dotnet build src/dotnet/TraceMap.sln`: passed.
  - `dotnet test src/dotnet/TraceMap.sln`: 524 passed, 0 failed.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.

## Kiro Review State

- Initial Sonnet implementation review:
  - Command: `node scripts/kiro-review.mjs --phase event-message-surfaces --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Artifact: `.tmp/kiro-reviews/event-message-surfaces/2026-06-20T154009-935Z-implementation-claude-sonnet-4.6.clean.md`
  - Coverage: reduced because Kiro reported denied tool access.
  - Findings patched: `direction-filter-not-supported` gaps, event/message JSON limitation, static stable-key discriminator behavior, and overly broad `endpoint` unsafe hashing.
- First Sonnet re-review:
  - Command: `node scripts/kiro-review.mjs --phase event-message-surfaces --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Artifact: `.tmp/kiro-reviews/event-message-surfaces/2026-06-20T154708-466Z-re-review-claude-sonnet-4.6.clean.md`
  - Coverage: full.
  - Findings patched: `safeMetadataHash` now matches the stable-key metadata hash; unchecked required tests are explicitly documented as deferred.
- Final Sonnet re-review:
  - Command: `node scripts/kiro-review.mjs --phase event-message-surfaces --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Artifact: `.tmp/kiro-reviews/event-message-surfaces/2026-06-20T155127-764Z-re-review-claude-sonnet-4.6.clean.md`
  - Coverage: reduced because Kiro reported denied tool access.
  - Decision: no blocking issues. Recommended pre-merge stable-key source label fix was patched by using the scan repo identity instead of the literal `scan-source`; `@` and wildcard identity handling were also tightened. No further Kiro cycles were run because this hit the two re-review cycle cap.

## Oddities

- Safe static destination keys are intentionally conservative. Values that look
  URL-like, host-like, secret-like, interpolated, wildcarded, or environment
  substituted are hashed or gapped instead of rendered.
- Static publish/consume candidate edges require the same message surface kind
  and same safe normalized destination key. A queue consumer and stream
  publisher with the same text do not produce a candidate edge.
- `stableMessageSurfaceKey` uses metadata that excludes handler/publisher
  symbols so handler renames do not churn static destination identities.
- PR-loop review fixes narrowed MediatR exclusion so broker senders named like
  `ServiceBusSender` are still extracted, and made same-name constants
  ambiguity-aware so duplicate unresolved destinations emit gaps instead of
  projecting the wrong static surface.
- Fresh Codex review fixes made qualified constant member access resolve only
  through qualified type/member keys and classify one-argument
  `ServiceBusTrigger` attributes as queue surfaces while keeping two-argument
  topic/subscription triggers as topic surfaces.

## Follow-Up Items

- Add Roslyn semantic message extraction for a small framework set and Tier1
  tests.
- Add reducer context and downgrade behavior for message surfaces.
- Add route-flow async boundary rendering for message hops.
- Add cross-language adapter slices only after the shared contract is accepted.

## Explicit Test Deferrals

The following unchecked task-list tests are intentionally deferred to follow-up
slices rather than silently considered complete:

- Markdown escaping for rendered message destination names. Current safe
  destination rendering rejects pipe/bracket-style values into hashed identities;
  a direct escaping regression test should be added with the next report-contract
  hardening slice.
- Hashed-destination collision or duplicate-key gap behavior. Current hashed
  destinations carry full SHA-256 inputs and are not merged by projection, but a
  dedicated collision/duplicate stable-key gap test remains follow-up work.
- Same destination with different operation directions, and `bind` versus
  `declare` normalization/distinctness. The implementation keeps direction in
  the stable key and candidate edges only use `publish`/`consume`, but dedicated
  row-separation tests are deferred.
- Stable-key fallback tests for dynamic and unsafe-omitted destinations. Dynamic
  evidence currently emits `AnalysisGap`; hashed unsafe destinations emit review
  tier surface evidence. More focused key-fallback tests are follow-up work.
- Byte-stable combined report output and required empty message arrays/sections.
  Message fields are additive on existing `dependencySurfaces` rows and no
  separate message section was introduced in this slice, so schema version bump
  was deferred.
- Combined report schema/version contract update. The decision for this slice is
  additive JSON fields under existing report version `1.0`; a formal version bump
  is deferred to the next combined-report contract update.
- `./scripts/check-private-paths.sh` generated-output coverage for message
  reports. The current script guards committed files; the synthetic CLI smoke
  uses temporary outputs and is not committed. Extending generated-output
  sentinel coverage is follow-up work.
