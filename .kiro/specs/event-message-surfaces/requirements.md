# Event And Message Dependency Surfaces Requirements

## Introduction

TraceMap already models HTTP routes, outbound HTTP calls, SQL/query evidence,
packages, config, combined reports, paths, reverse queries, and reducer context.
Many service dependencies, however, are carried through queues, topics, event
buses, background job frameworks, and stream-like publish/consume APIs rather
than direct HTTP calls.

This phase adds deterministic static evidence for event and message dependency
surfaces. It recognizes statically visible queue, topic, subscription, channel,
event, exchange, routing-key, stream, and handler surfaces where supported by
language adapters. It must not connect to a broker, inspect live topology,
observe runtime traffic, prove message delivery, prove subscription activity, or
expand dynamic topic names beyond evidence stored by deterministic extractors.

This is not AI impact analysis. The implementation must not add LLM calls,
embeddings, vector databases, or prompt-based classification.

## Current State

- `tracemap scan` emits deterministic facts with rule IDs, evidence tiers, file
  paths, line spans, commit SHA, and extractor versions.
- `tracemap combine`, `tracemap report`, `tracemap paths`, `tracemap reverse`,
  and reducer context can already reason over endpoint, SQL, HTTP, package, and
  config surfaces.
- Language adapters have different maturity levels: .NET has Roslyn semantic
  analysis plus syntax fallback, TypeScript has compiler-backed and syntax
  fallback evidence, Python is reduced AST/package/config coverage, and JVM is
  Java compiler-backed with Kotlin syntax fallback.
- TraceMap does not yet have a shared event/message surface vocabulary or
  combined-index behavior for publish/consume evidence.

## MVP Scope Decisions

- Add shared fact vocabulary for publisher, consumer, binding/config, and
  analysis-gap evidence.
- Detect static destination names and event names only when visible through
  literal, constant, enum, annotation/decorator, framework config, or safe
  manifest evidence.
- Store normalized safe identities and hashes rather than raw source snippets,
  raw config values, URLs, hostnames, connection strings, secrets, or local
  absolute paths.
- Include .NET first-class extraction and design extension boundaries for
  TypeScript, Python, and JVM without requiring full parity in the first
  implementation.
- Exclude in-process mediator/notification patterns from broker-backed message
  surfaces in v1. They may become a separate future surface family, but must
  not be emitted under message-surface rules by default.
- Integrate event/message surfaces into combined reports, reverse queries, and
  paths/route-flow context where the existing query model can use safe static
  selectors.
- Label syntax-only, dynamic, framework-unsupported, missing-project-load, and
  missing-config evidence as reduced coverage or analysis gaps.
- Defer runtime topology, broker connection, live subscription discovery,
  schema registry lookup, reflection expansion, dynamic destination expansion,
  and message payload compatibility.

## Requirements

### Requirement 1: Shared Event/Message Fact Vocabulary

**User Story:** As a maintainer, I want a shared vocabulary for event/message
surfaces so that every language adapter can emit comparable evidence.

Acceptance Criteria:

1. WHEN implementation emits event/message facts THEN each fact SHALL include a
   rule ID, evidence tier, safe repo-relative file path, line span where
   available, commit SHA, extractor ID, and extractor version.
2. WHEN a publisher is statically detected THEN TraceMap SHALL emit a publisher
   fact with a normalized surface identity, framework family where available,
   operation kind, and safe destination metadata.
3. WHEN a consumer or handler is statically detected THEN TraceMap SHALL emit a
   consumer fact with a normalized surface identity, handler identity where
   available, framework family where available, and safe destination metadata.
4. WHEN config, annotations, decorators, attributes, or manifests declare
   bindings without a visible handler or publish call THEN TraceMap SHALL emit a
   binding/config fact rather than inventing a publisher or consumer.
5. WHEN destination or event identity is not statically known THEN TraceMap
   SHALL emit an `AnalysisGap` or reduced-coverage fact and SHALL NOT create a
   precise surface identity from a guessed name.
6. WHEN raw destination values are available THEN TraceMap SHALL store a safe
   normalized key and/or hash according to shared redaction rules and SHALL NOT
   persist raw source snippets by default.
7. WHEN a fact type is introduced THEN the rule catalog SHALL document
   limitations before implementation emits the rule in committed code.

### Requirement 2: Static Destination Identity

**User Story:** As an investigator, I want stable event/message identities so
that combined reports and diffs can compare message-driven dependencies without
row-ID churn.

Acceptance Criteria:

1. WHEN a destination name is a literal, compile-time constant, enum member,
   static attribute/decorator argument, framework annotation value, or safe
   config key THEN TraceMap SHALL derive a deterministic normalized destination
   identity.
2. WHEN a destination contains environment interpolation, string concatenation,
   runtime variables, reflection, expression-language expansion, or config
   substitution that cannot be resolved statically THEN TraceMap SHALL mark the
   identity dynamic or unknown and SHALL NOT claim a concrete destination.
3. WHEN a framework distinguishes queue, topic, subscription, exchange, routing
   key, stream, event type, binding, channel, or subject THEN TraceMap SHALL
   preserve the kind when statically visible.
4. WHEN only a framework call is visible without destination identity THEN
   TraceMap SHALL retain publisher/consumer evidence as reduced coverage with a
   gap explaining the missing destination.
5. WHEN a stable key is emitted THEN it SHALL be derived from source label,
   language, surface kind, framework family, normalized destination identity,
   operation kind, and safe metadata hash where needed.
6. WHEN a stable key would require unsafe raw values THEN TraceMap SHALL use a
   deterministic hash and mark the row as needing review in downstream reports.

### Requirement 3: .NET Framework Coverage

**User Story:** As a .NET maintainer, I want TraceMap to recognize common
message publish and consume surfaces without requiring a clean build.

Acceptance Criteria:

1. WHEN Roslyn semantic analysis resolves common .NET messaging APIs THEN
   TraceMap SHOULD emit Tier1 semantic publisher or consumer evidence.
2. WHEN semantic analysis fails but syntax patterns are visible THEN TraceMap
   SHALL continue scanning and emit Tier3 syntax/textual evidence or gaps.
3. WHEN ASP.NET, worker service, or Azure Functions attributes declare queue,
   topic, Service Bus, Event Hub, storage queue, or timer-like event bindings
   THEN TraceMap SHALL emit binding/consumer evidence where static arguments are
   visible.
4. WHEN MassTransit, NServiceBus, CAP, Dapr pub/sub, Kafka, RabbitMQ, or common
   client-library calls expose static destination names THEN TraceMap SHOULD
   emit publisher/consumer evidence under documented framework families.
5. WHEN framework-specific patterns are ambiguous or too broad THEN TraceMap
   SHALL emit a gap or syntax-only evidence instead of overclaiming.
6. WHEN project load fails THEN event/message extraction SHALL still run over
   `.cs`, project, and config files where possible and mark coverage reduced.
7. WHEN MediatR-like or other in-process notification patterns are detected in
   v1 THEN TraceMap SHALL NOT emit `MessagePublisherSurface` or
   `MessageConsumerSurface` facts for them; future support requires a separate
   rule and surface family that is clearly not broker-backed dependency
   evidence.

### Requirement 4: TypeScript, Python, And JVM Extension Boundaries

**User Story:** As a cross-language maintainer, I want a clear adapter contract
for event/message evidence without overcommitting v1 parity across ecosystems.

Acceptance Criteria:

1. WHEN TypeScript extraction supports event/message surfaces THEN it SHALL use
   the shared fact vocabulary and static identity rules for frameworks such as
   Kafka, AMQP/RabbitMQ, SNS/SQS-style clients, Bull/BullMQ, NestJS microservices,
   and Dapr where deterministic evidence is available. EventEmitter/EventTarget
   patterns are in-process evidence by default and SHALL become message surfaces
   only when framework context explicitly proves an external messaging
   dependency.
2. WHEN Python extraction supports event/message surfaces THEN it SHALL use the
   shared fact vocabulary and static identity rules for frameworks such as
   Celery, FastAPI/background integrations, Kafka clients, Kombu/AMQP,
   RabbitMQ clients, cloud queue clients, and Dapr where deterministic evidence
   is available.
3. WHEN JVM extraction supports event/message surfaces THEN it SHALL use the
   shared fact vocabulary and static identity rules for frameworks such as
   Spring Kafka, Spring Cloud Stream, JMS, RabbitMQ, Kafka clients, annotations,
   and config where deterministic evidence is available.
4. WHEN an adapter cannot support a framework in v1 THEN it SHALL emit no claim
   or an explicit analysis gap only when there is evidence of an unsupported
   messaging boundary.
5. WHEN language-specific framework names differ THEN combined-index readers
   SHALL map them into the shared surface kinds without losing the original
   rule ID or framework family.

### Requirement 5: Combined Reports And Dependency Surfaces

**User Story:** As a reviewer, I want combined reports to show event/message
surfaces beside HTTP, SQL, package, and config evidence.

Acceptance Criteria:

1. WHEN combined indexes contain event/message publisher or consumer facts THEN
   `tracemap report` SHALL include event/message surface counts and safe
   surface rows.
2. WHEN a report renders event/message evidence THEN it SHALL include source
   label, language, surface kind, operation kind, evidence tier, rule ID,
   safe file span, static identity status, and coverage caveats where available.
3. WHEN a publisher and consumer share a stable destination identity across
   sources THEN combined reporting MAY show a static cross-source candidate
   relationship, but it SHALL label it as static evidence only and not runtime
   delivery.
4. WHEN only one side is present THEN combined reporting SHALL still show the
   evidence and SHALL NOT imply that the opposite publisher or consumer exists.
5. WHEN binding-only evidence exists with no publisher or consumer in any source
   THEN combined reporting SHALL still show the binding/config evidence when
   safe or emit a rule-backed gap when omitted.
6. WHEN destination identity is hashed or dynamic THEN the report SHALL label
   the limitation near the row.
7. WHEN report output is Markdown or JSON THEN it SHALL not render raw source
   snippets, raw config values, raw URLs, hostnames, secrets, connection
   strings, local absolute paths, or unsafe destination values.

### Requirement 6: Paths, Route Flow, And Reverse Queries

**User Story:** As an investigator, I want static path and reverse queries to
include event/message surfaces when that helps answer dependency questions.

Acceptance Criteria:

1. WHEN `tracemap paths` is run over a combined index with event/message
   surfaces THEN event/message surfaces SHALL be eligible terminal surfaces
   when selected or when default surface traversal includes them.
2. WHEN a publisher call is reachable from an endpoint, route-flow node, symbol,
   or source selector through existing static edges THEN paths MAY show the
   publisher as a terminal surface with supporting fact IDs and edge IDs.
3. WHEN a consumer handler is statically visible THEN reverse queries SHALL be
   able to find endpoints, symbols, or sources that statically flow to or from
   that consumer only when existing graph evidence supports the traversal.
4. WHEN the only connection between publisher and consumer is a shared
   destination identity THEN TraceMap SHALL represent it as a static
   publish/consume candidate edge, not a call edge or runtime path.
5. WHEN route-flow integration includes event/message hops THEN reports SHALL
   label async/message boundaries and SHALL NOT imply request/response ordering,
   exactly-once delivery, consumer liveness, or broker routing.
6. WHEN no static path is found THEN reports SHALL distinguish `NoPathEvidence`
   from reduced coverage, unsupported framework, or dynamic destination gaps.

### Requirement 7: Reducer And Contract-Change Context

**User Story:** As a maintainer, I want reducer outputs to use event/message
evidence as context without making unsupported impact claims.

Acceptance Criteria:

1. WHEN a contract delta matches an event/message surface by stable destination,
   event type, handler symbol, DTO symbol, package/config evidence, or path
   context THEN reducer reports MAY include event/message context with the
   supporting rule IDs and evidence tiers.
2. WHEN event/message context is name-only, syntax-only, high fan-out, dynamic,
   hashed, or ambiguous THEN reducer output SHALL downgrade to review-needed or
   gap-aware classifications rather than `DefiniteImpact`.
3. WHEN event payload schema or serializer mapping is not statically proven THEN
   TraceMap SHALL NOT claim payload compatibility, deserialization success, or
   runtime consumer breakage.
4. WHEN a publisher and consumer share a destination identity but no payload
   contract evidence exists THEN reducer output SHALL say the relationship is
   destination-level static evidence only.

### Requirement 8: Safety, Redaction, And Public Output

**User Story:** As a project owner, I want event/message outputs safe for public
review and honest about evidence limits.

Acceptance Criteria:

1. WHEN event/message facts or reports are emitted THEN they SHALL not include
   raw source snippets, raw config values, raw remotes, local absolute paths,
   URLs, hostnames, connection strings, secrets, credentials, or private sample
   identities.
2. WHEN destination names are rendered THEN implementation SHALL use shared
   safe-value policies, including hashing or omission when names are unsafe,
   secret-like, host-like, URL-like, too long, or configured as private.
3. WHEN Markdown renders user-controlled text THEN it SHALL escape table and
   link delimiters consistently with existing report writers.
4. WHEN scan coverage is reduced THEN reports SHALL visibly label event/message
   coverage as reduced or partial.
5. WHEN limitations are rendered THEN they SHALL state that static evidence does
   not prove runtime delivery, broker topology, live subscriptions, traffic,
   auth, retention, ordering, delivery semantics, retries, dead-letter behavior,
   schema compatibility, or deployment reachability.

### Requirement 9: Validation And Tests

**User Story:** As a maintainer, I want focused validation proving the feature
is deterministic, safe, and useful across adapter maturity levels.

Acceptance Criteria:

1. WHEN implementation adds event/message rules THEN unit tests SHALL cover
   publisher, consumer, binding/config, dynamic destination gap, unsupported
   framework gap, redaction, report rendering, combined report, paths, reverse,
   and reducer-context behavior where relevant.
2. WHEN .NET extraction is implemented THEN tests SHALL cover semantic and
   syntax fallback evidence and project-load failure coverage.
3. WHEN TypeScript, Python, or JVM extraction is implemented THEN each adapter
   SHALL add fixture tests for its supported frameworks and reduced-coverage
   gaps.
4. WHEN combined-index behavior changes THEN validation SHALL include a
   combine/report/paths/reverse smoke over public or synthetic fixtures.
5. WHEN output schemas change THEN tests SHALL prove stable JSON fields,
   deterministic ordering, required empty arrays, report schema/version or
   documented-contract updates following the existing dependency-report and
   combined-query conventions, and safe Markdown/JSON output.
6. WHEN implementation is complete THEN `dotnet test`, relevant adapter tests,
   `./scripts/check-private-paths.sh`, and `git diff --check` SHALL pass or be
   explicitly deferred with rationale in implementation state.
