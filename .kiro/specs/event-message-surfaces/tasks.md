# Event And Message Dependency Surfaces Tasks

Status: implemented-v1-with-follow-ups

Post-promotion note: PR #225 implemented the first event/message surface slice.
Unchecked items below are follow-up depth, not evidence that the v1 slice is
still pending.

## Spec Authoring Tasks

- [x] Create requirements for static event/message publish, consume, binding,
  coverage-gap, report, path, reverse, and reducer behavior.
- [x] Create design covering fact vocabulary, rule IDs, identity rules,
  adapter extension boundaries, combined-index integration, and safety.
- [x] Keep implementation tasks unchecked for this spec-only PR.

## Implementation Tasks

- [ ] 1. Add shared vocabulary and rule catalog entries. Requirements: 1, 2, 8.
  - [x] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` with the shared message
        fact vocabulary, safe metadata fields, surface kinds, evidence-tier
        guidance, and adapter obligations before TypeScript, Python, or JVM
        implementation slices begin.
  - [x] Commit and review rule catalog entries before any implementation emits
        `MessagePublisherSurface`, `MessageConsumerSurface`,
        `MessageBindingDeclared`, message-specific `AnalysisGap` facts,
        `message-publish-consume`, or any `message.surface.*` rule-backed fact
        or row.
  - [x] Add rule catalog entries for publisher, consumer, binding, identity,
        combine, candidate-edge, paths, reducer, and gap rules before emitting
        facts.
  - [x] Document limitations for static evidence, runtime delivery, topology,
        live subscriptions, traffic, auth, retries, retention, ordering,
        dead-letter behavior, schema compatibility, and deployment reachability.
  - [x] Add shared surface kinds for queue, topic, subscription, exchange,
        stream, event, channel, and unknown message surfaces.
  - [x] Add safe destination identity normalization and hashing helpers.
  - [x] Document `MessageBindingDeclared` as the v1 home for declaration-only
        destination metadata; defer any separate destination-only fact until a
        future spec defines its integration path.
  - [x] Require full collision-resistant destination hash input for stable keys
        and document any shortened display-only hash behavior.
  - [ ] Add tests for safe renderable names, hashed names, dynamic names,
        unsafe omitted names, deterministic stable keys, and Markdown escaping.
  - [x] Add a stable-key test proving a handler rename does not change a
        static-destination surface key when destination and direction are
        unchanged.
  - [ ] Add a hashed-destination collision or duplicate-key test proving
        distinct hidden destinations are not silently merged.
  - [x] Add a negative test proving MediatR-like in-process notification
        patterns do not emit broker-backed message publisher or consumer facts.

- [ ] 2. Implement .NET publisher/consumer/binding extraction. Requirements:
      1, 2, 3, 8, 9.
  - [ ] Detect selected .NET publisher calls with Roslyn semantic evidence where
        symbols resolve.
  - [ ] Detect selected .NET consumer/handler declarations with semantic
        evidence where symbols resolve.
  - [x] Detect selected attribute/config binding declarations.
  - [x] Emit syntax fallback evidence when MSBuild or semantic model loading
        fails.
  - [x] Emit analysis gaps for dynamic, ambiguous, unsupported, or unsafe
        destination identity.
  - [x] Preserve file paths, line spans, commit SHA, extractor versions,
        rule IDs, evidence tiers, and supporting symbol IDs where available.
  - [ ] Add checked-in synthetic fixtures covering semantic, syntax fallback,
        config/binding, dynamic destination, unsupported framework, and failed
        project-load scenarios without private names or unsafe values.
  - [ ] Add focused .NET tests for semantic, syntax fallback, failed-project
        load, config, redaction, dynamic gaps, and unsupported framework gaps.

- [ ] 3. Project event/message facts into combined dependency surfaces.
      Requirements: 5, 8, 9.
  - [x] Update combined surface projection to include message surface kinds.
  - [x] Extend `CombinedSurfaceProjectionRow` and combined report JSON rows with
        message-specific fields such as operation direction, framework family,
        destination identity status, destination key/hash, event type identity,
        handler/publisher identity, and safe metadata hash.
  - [x] Decide and document whether `handlerSymbolId` and `publisherSymbolId`
        are `fact_symbols` role rows or properties-only safe metadata.
  - [ ] Update report schema version or documented JSON contract according to
        existing dependency-report conventions.
  - [x] Render event/message surface summaries and rows in combined Markdown
        and JSON reports.
  - [x] Preserve one-sided publisher and consumer evidence.
  - [x] Project `MessageBindingDeclared` bind/declare rows into combined
        reports when safe, or emit explicit gap rows when omitted because
        identity is dynamic, unsafe, or absent.
  - [x] Create static publish/consume candidate rows only for safe stable
        destination matches.
  - [ ] Add identity-collision tests proving same destination with different
        operation directions does not collapse into one row.
  - [ ] Add tests proving `bind` and `declare` directions are either
        intentionally distinct rows or normalized to one row per framework
        artifact according to adapter rules.
  - [ ] Add stable-key fallback tests for dynamic and unsafe-omitted
        destinations.
  - [x] Label hashed, dynamic, ambiguous, unsafe, and reduced-coverage rows.
  - [ ] Add combined report tests for rows, counts, candidate relationships,
        binding-declared-only evidence, redaction, deterministic ordering,
        byte-stable output, empty message sections/arrays, and required empty
        arrays.
  - [ ] Name and test required JSON fields for empty message sections, including
        message surfaces and publish/consume candidate rows, following existing
        report naming conventions.
  - [x] Test that combined report rendering never emits
        `message-publish-consume` as a `surfaceKind`.

- [ ] 4. Integrate message surfaces with paths, route-flow, and reverse
      queries. Requirements: 6, 8, 9.
  - [x] Allow message surface kinds as path and reverse selectors.
  - [x] Extend reverse-query surface allowlists and validation errors for
        message surface kinds, with tests for accepted selectors and clean
        rejection of unsupported values.
  - [x] Reject `message-publish-consume` as a surface-kind selector with a
        clear validation error and test.
  - [x] Add direction filtering for publisher, consumer, binding, or all
        directions; reserve `direction-filter-not-supported` for future
        adapters or report contexts that cannot honor a direction-specific
        selector.
  - [ ] Show endpoint/symbol/source-to-publisher paths when existing graph
        evidence reaches publisher facts.
  - [ ] Show consumer-to-downstream paths when existing graph evidence starts
        from handler facts.
  - [ ] Represent publisher-to-consumer destination matches as async static
        candidate hops, not call edges.
  - [ ] Label async/message boundaries in route-flow context where integrated.
  - [ ] Add a route-flow test proving a message hop includes async boundary
        caveat text.
  - [ ] Add same-source and cross-source publish/consume candidate path tests,
        including the same-source async boundary caveat.
  - [ ] Add tests for no-path evidence, reduced coverage, dynamic destination,
        unsupported framework, path caps, reverse caps, and safe rendering.

- [ ] 5. Add reducer context over event/message evidence. Requirements: 7, 8, 9.
  - [ ] Add reducer context rows for stable destination, event type, handler,
        publisher, DTO, package/config, and bounded path matches.
  - [ ] Downgrade name-only, syntax-only, dynamic, hashed, ambiguous, generic,
        and high fan-out matches to review-needed or gap-aware classifications.
  - [ ] Avoid payload compatibility or runtime breakage claims without explicit
        serializer/schema evidence.
  - [ ] Add tests for destination-only context, payload-unknown caveats,
        high-fan-out downgrade with event names that match many routes or
        handlers, and reduced-coverage behavior.

- [ ] 6. Add TypeScript adapter support in a separate implementation slice.
      Requirements: 1, 2, 4, 8, 9.
  - [ ] Select a small framework set and document unsupported frameworks.
  - [ ] Emit shared event/message facts for static publisher, consumer, binding,
        and gap evidence.
  - [ ] Add tests for visible messaging packages or framework markers that are
        not yet supported: emit no claim, or emit an unsupported-framework gap
        only when there is concrete messaging-boundary evidence.
  - [ ] Use compiler-backed evidence when available and syntax fallback when
        reduced.
  - [ ] Add fixture tests, adapter validation, and private-output guards.

- [ ] 7. Add Python adapter support in a separate implementation slice.
      Requirements: 1, 2, 4, 8, 9.
  - [ ] Select a small framework set and document unsupported frameworks.
  - [ ] Emit shared event/message facts without importing or executing user
        code.
  - [ ] Add tests for visible messaging packages or framework markers that are
        not yet supported: emit no claim, or emit an unsupported-framework gap
        only when there is concrete messaging-boundary evidence.
  - [ ] Label AST-only and unresolved evidence as reduced coverage.
  - [ ] Add a negative test proving message extraction does not import modules,
        call `__import__`, use `importlib`, `exec`, or `eval`, or otherwise
        execute project code.
  - [ ] Add fixture tests, adapter validation, and private-output guards.

- [ ] 8. Add JVM adapter support in a separate implementation slice.
      Requirements: 1, 2, 4, 8, 9.
  - [ ] Select a small framework set and document unsupported frameworks.
  - [ ] Emit shared event/message facts from Java semantic evidence and Kotlin
        syntax fallback where available.
  - [ ] Add tests for visible messaging packages or framework markers that are
        not yet supported: emit no claim, or emit an unsupported-framework gap
        only when there is concrete messaging-boundary evidence.
  - [ ] Use package metadata only as framework context, not as publisher or
        consumer proof.
  - [ ] Add fixture tests, adapter validation, and private-output guards.

- [ ] 9. Validate implementation. Requirements: 8, 9.
  - [x] Run `dotnet test`.
  - [ ] Run relevant TypeScript, Python, and JVM adapter tests for touched
        adapters.
  - [x] Run a combine/report/paths/reverse smoke over public or synthetic
        event/message fixtures.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [ ] Confirm `./scripts/check-private-paths.sh` covers combined report,
        paths report, reverse report, route-flow report, reducer output, and
        any new message-surface output files; update it if coverage is missing.
  - [x] Run `git diff --check`.
  - [x] Update docs, acceptance notes, report schema/version documentation, and
        language adapter contract where rule or schema behavior changes.
