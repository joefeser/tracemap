# Event And Message Dependency Surfaces Design

## Overview

Add event/message surfaces as deterministic static dependency evidence. The
feature extends scanner facts and combined-index readers so queue, topic, event,
stream, exchange, routing-key, channel, and handler evidence can participate in
the same report, path, reverse, diff, and reducer workflows as existing HTTP,
SQL, package, and config surfaces.

The core shape is:

```text
source endpoint / symbol / route-flow node
  -> existing call, argument, object, or relationship evidence
  -> message publisher or consumer fact
  -> event/message dependency surface
  -> optional static publish/consume candidate edge by shared destination
```

Every arrow remains static evidence. TraceMap must not connect to brokers,
resolve live topology, inspect runtime subscriptions, claim traffic, prove
delivery, or infer payload compatibility without separate rule-backed evidence.

## Goals

- Represent publish and consume evidence as first-class dependency surfaces.
- Preserve rule IDs, evidence tiers, file spans, source labels, commit SHAs,
  extractor versions, and coverage labels.
- Normalize static destination identities deterministically and safely.
- Keep useful evidence when semantic analysis or project load fails.
- Make combined reports, reverse queries, paths, route-flow context, and
  reducer context able to include event/message surfaces.
- Define extension boundaries for .NET, TypeScript, Python, and JVM adapters.
- Keep public outputs safe by hashing or omitting unsafe values.

## Non-Goals

- No broker connection, cloud API call, schema registry call, or runtime probe.
- No live subscription, topology, routing, retention, retry, dead-letter, auth,
  ordering, exactly-once, or traffic proof.
- No reflection expansion, runtime topic expansion, config transform execution,
  expression-language evaluation, or dynamic string synthesis in v1.
- No payload compatibility, serializer contract, event versioning, or schema
  compatibility conclusions unless another feature provides explicit evidence.
- No new LLM calls, embeddings, vector databases, or prompt-based
  classification.

## Proposed Fact Types

Add or reuse deterministic fact types:

| Fact type | Purpose |
| --- | --- |
| `MessagePublisherSurface` | Static publish/send/produce/enqueue evidence. |
| `MessageConsumerSurface` | Static consume/subscribe/handle/dequeue evidence. |
| `MessageBindingDeclared` | Static framework binding, config, annotation, or manifest evidence. |
| `AnalysisGap` | Canonical gap fact with message-surface properties for dynamic, unsupported, ambiguous, unsafe, or reduced-coverage evidence. |

Destination metadata that is neither clearly publisher nor consumer should be
represented as `MessageBindingDeclared` with `operationDirection = declare` or
`bind` and a specific `frameworkFeature` or `bindingKind`. Do not introduce a
separate destination-only fact in v1 unless a future spec defines its combined,
path, reverse, and reducer integration path.

Suggested surface kinds for combined readers:

| Surface kind | Meaning |
| --- | --- |
| `message-queue` | Queue-like point-to-point destination. |
| `message-topic` | Pub/sub topic, subject, or event topic. |
| `message-subscription` | Named subscription/group/consumer binding where safe. |
| `message-exchange` | Exchange/routing-key style destination. |
| `message-stream` | Stream/log destination such as Kafka-like topics or event streams. |
| `message-event` | Event type/name when a framework uses event identity rather than broker destination. |
| `message-channel` | Framework logical channel or binding name. |
| `message-unknown` | Message evidence exists, but static kind is unknown. |

Publisher/consumer direction is not encoded only in `surfaceKind`; it should be
stored as `operationDirection` so the same destination can appear as both
publisher and consumer.

Graph edge and relationship kinds are a separate namespace from destination
surface kinds. `message-publish-consume` is the proposed async static candidate
edge kind for a publisher and consumer sharing safe destination identity; it is
not a surface kind and must not be accepted where a surface-kind selector is
expected.

## Rule Catalog Plan

Implementation should add rule catalog entries before emitting any new rule ID.
Proposed IDs:

| Rule ID | Purpose |
| --- | --- |
| `message.surface.publish.v1` | Generic publisher/send/produce evidence. |
| `message.surface.consume.v1` | Generic consumer/handler/subscribe evidence. |
| `message.surface.binding.v1` | Config, annotation, decorator, or manifest binding evidence. |
| `message.surface.identity.v1` | Destination identity normalization, hashing, and dynamic-gap rules. |
| `message.surface.combine.v1` | Combined report and static publish/consume candidate rows. |
| `message.surface.candidate-edge.v1` | Async static publish/consume candidate edge rows by shared safe destination identity. |
| `message.surface.paths.v1` | Path, route-flow, and reverse-query use of message surfaces. |
| `message.surface.reducer.v1` | Reducer context over event/message evidence. |
| `message.surface.gap.v1` | Unsupported, dynamic, wildcard/pattern, ambiguous, unsafe, or reduced-coverage gaps. Canonical gap reasons include `dynamic-destination`, `wildcard-pattern-destination`, `missing-destination-identity`, `unsupported-framework`, `unsafe-omitted`, `ambiguous-destination`, and `direction-filter-not-supported`. |

Rule catalog limitations must state that these rules are static evidence only
and do not prove broker topology, runtime delivery, live subscriptions,
production traffic, auth, ordering, retries, retention, dead-letter behavior,
schema compatibility, deployment reachability, or payload compatibility.

Language adapters may add framework-specific rule IDs, such as
`dotnet.message.azure-functions.v1` or `jvm.message.spring-kafka.v1`, but the
combined readers should consume them through the shared fact vocabulary.

## Evidence Model

Recommended safe metadata fields:

| Field | Description |
| --- | --- |
| `frameworkFamily` | Stable family such as `azure-functions`, `masstransit`, `kafka`, `rabbitmq`, `jms`, `celery`, or `dapr`. |
| `frameworkFeature` | Optional feature name such as attribute, decorator, config binding, client call, producer, consumer, or handler. |
| `operationDirection` | `publish`, `consume`, `bind`, `declare`, or `unknown`. |
| `operationKind` | `send`, `publish`, `produce`, `enqueue`, `subscribe`, `handle`, `receive`, `bind`, or adapter-specific normalized value. |
| `surfaceKind` | One of the message surface kinds above. |
| `destinationIdentityStatus` | `static`, `hashed`, `dynamic`, `unknown`, `ambiguous`, or `unsafe-omitted`. |
| `normalizedDestinationKey` | Safe normalized identity when renderable. |
| `destinationHash` | Full 64-character lowercase hex SHA-256 hash when the raw destination cannot be rendered. |
| `eventTypeIdentity` | Safe type/symbol/event identity where a framework exposes event type instead of destination name. |
| `handlerSymbolId` | Stable handler symbol identity where available. |
| `publisherSymbolId` | Stable publisher symbol identity where available. |
| `subscriptionIdentityStatus` | Safe status for group/subscription/channel identity when present. |
| `safeMetadataHash` | Full 64-character lowercase hex SHA-256 hash over sorted safe metadata fields for stable keys. |

`handlerSymbolId` and `publisherSymbolId` are message-specific role extensions.
The first implementation slice must decide in `docs/LANGUAGE_ADAPTER_CONTRACT.md`
whether they participate in `fact_symbols` role rows or remain properties-only.
Until that contract is updated, they should be treated as safe metadata, not as
portable graph edges.

`safeMetadataHash` should be stored as the full 64-character lowercase hex
SHA-256 over UTF-8 bytes of sorted
`key=value` pairs from the safe metadata allowlist, joined with newline
characters and using lowercase keys. Unsafe raw values are omitted or replaced
by their approved hashes before this computation. Reports may display a shorter
prefix, but stable keys and persisted safe metadata should use the full digest.

Do not store raw source snippets by default. Do not render raw config values,
hostnames, URLs, connection strings, secrets, local absolute paths, raw remotes,
or private sample identities. Raw destination values should be treated like raw
config values unless the shared safe-value helper explicitly allows rendering.

## Destination Identity Rules

Normalize only evidence that is static enough:

- string literals;
- compile-time constants;
- enum members;
- attribute/decorator/annotation literal values;
- framework manifest or config keys that pass safety checks;
- symbol-backed event type names or message contract type names;
- generated static binding metadata.

Do not resolve:

- runtime variables;
- environment interpolation;
- string concatenation with non-constant parts;
- reflection;
- dependency-injected options values unless already extracted as safe static
  config evidence;
- framework expression languages;
- wildcard subscriptions or pattern topics beyond a labeled pattern fact;
- broker topology aliases learned only at runtime.

Wildcard or pattern subscriptions should be emitted as `AnalysisGap` facts under
`message.surface.gap.v1` with message-surface properties,
`destinationIdentityStatus = dynamic`, and a safe gap reason such as
`wildcard-pattern-destination`. They must not become a consumer surface with a
guessed destination.

Event type identity follows the same safety posture but a different resolution
path. Compiler-resolved message/event type symbols may reach Tier1 when the
adapter can prove the type identity. Framework-declared type names without a
resolved symbol are Tier2 when they come from static annotation/config shape and
Tier3 when they are syntax/text-only. Unresolved type-like strings must not be
treated as payload compatibility evidence.

When raw destination text cannot be rendered but is available to the extractor,
`destinationHash` used in a stable key must be derived from the full raw value
with the project's shared SHA-256-style hash helper and stored as a full
64-character lowercase hex digest. It must not be truncated in the stable-key
input. Rendered reports may show shortened hashes, but the stable identity must
use collision-resistant full input. If duplicate stable keys still occur,
downstream projection must emit a duplicate-identity gap rather than silently
merging rows.

Stable key input should be:

```text
message-surface/v1
sourceLabel
language
surfaceKind
frameworkFamily
operationDirection
operationKind
destinationIdentityStatus
normalizedDestinationKey or destinationHash or eventTypeIdentity
static occurrence discriminator only when destination/direction/operation would
  otherwise collide inside one source
safeMetadataHash
```

For static destination identities, the stable key should be anchored on
destination identity, direction, and operation. `bind` and `declare` are
intentionally distinct directions for key purposes; adapters should normalize to
one value per artifact when a framework uses the two words for the same static
binding concept. Handler or publisher symbol ID belongs in evidence metadata
and must not be part of the stable key for static destination rows. If one
source emits multiple static rows that would otherwise collide, use an
occurrence discriminator formed as the first 16 lowercase hex characters of
SHA-256 over:

```text
safeRepoRelativePath|startLine|endLine|ruleId|safeMetadataHash
```

A handler rename must not churn the stable key for a single static-destination
surface.

If destination identity is dynamic, unknown, ambiguous, or unsafe-omitted, the
stable key may include safe file path, line span, rule family, and metadata hash
so rows remain deterministic. Those review-tier keys are expected to be more
volatile across edits than destination-backed keys and downstream reports must
mark them as review-needed.

## .NET Extraction Direction

.NET should use Roslyn semantic analysis where possible and syntax fallback
when semantic analysis or project load fails.

Candidate evidence families:

- Azure Functions queue, Service Bus, Event Hub, Event Grid, storage queue, and
  timer/event binding attributes.
- Worker service and hosted-service handlers with framework-specific message
  client calls.
- MassTransit producer, consumer, endpoint convention, and receive-endpoint
  configuration patterns.
- NServiceBus endpoint, handler, send, publish, and message type patterns.
- Kafka client producer/consumer calls.
- RabbitMQ/AMQP exchange, queue, routing-key, publish, consume, and bind calls.
- Dapr pub/sub publish and subscription declarations.
- CAP or similar transactional event bus publish/subscribe calls.
- MediatR-like notifications and other in-process mediator/event patterns are
  excluded from `MessagePublisherSurface` and `MessageConsumerSurface` in v1.
  They may become a separate `in-process-event` surface family in a future spec,
  but they must not pollute broker-backed message dependency evidence.

Semantic evidence can reach Tier1 only when the framework API or attribute
symbol resolves and the destination/event identity is static. Structural config
and attribute evidence can be Tier2. Syntax-only call/decorator/name evidence is
Tier3. Unsupported, ambiguous, or dynamic evidence is Tier4 gap or reduced
coverage.

Project files and config files should be scanned for safe binding names and
framework package clues so failed MSBuild loads still produce partial analysis.

## TypeScript Extension Boundary

TypeScript support should start from the shared adapter contract rather than a
separate schema. Candidate evidence families include:

- KafkaJS, node-rdkafka, and similar producer/consumer topic calls.
- amqplib/RabbitMQ exchange, queue, routing key, publish, sendToQueue, consume,
  and bind calls.
- AWS SDK SQS/SNS-style send/publish/subscribe calls where static command
  inputs are visible.
- Bull/BullMQ queue creation, processors, and add calls.
- NestJS microservices message patterns, event patterns, client emit/send, and
  transport config.
- Dapr publish/subscribe calls.
- Node EventEmitter/EventTarget only as local/in-process event evidence unless
  framework context proves an external messaging dependency.

Compiler-backed evidence can be Tier1 when symbols resolve. Static framework
config/decorator evidence is Tier2. Syntax-only fallback is Tier3. Dynamic
object construction, computed property names, environment values, and unresolved
imports should produce reduced coverage or gaps.

## Python Extension Boundary

Python support should remain honest about reduced AST coverage. Candidate
evidence families include:

- Celery task declarations, send_task/app.task/delay/apply_async call sites, and
  configured queues where static.
- Kombu, pika, RabbitMQ/AMQP exchange, queue, routing-key, publish, and consume
  evidence.
- Kafka clients and producer/consumer topic calls.
- cloud queue/topic clients when command input is static and safe.
- FastAPI or background-worker integrations only when they expose static event
  bindings.
- Dapr pub/sub calls.

Because Python extraction does not import user code or execute settings, many
findings will be Tier2/Tier3 or reduced coverage. The adapter should not import
modules, evaluate settings, run decorators, or install dependencies to resolve
message names.

## JVM Extension Boundary

JVM support should share Java/Kotlin contracts where practical and remain
explicit about Java semantic versus Kotlin syntax fallback. Candidate evidence
families include:

- Spring Kafka annotations, listener containers, KafkaTemplate send calls, and
  topic config.
- Spring Cloud Stream bindings, functional consumers/producers, and channel
  config.
- JMS annotations/listeners, templates, destination names, and config.
- RabbitMQ annotations/templates/listeners.
- Kafka producer/consumer clients.
- Dapr or cloud messaging clients where static evidence is available.

Java compiler-backed symbol evidence can be Tier1. Annotation/config structural
evidence can be Tier2. Kotlin syntax fallback and unresolved framework calls
should be Tier3 or gaps. Gradle/Maven package metadata may support framework
family detection, but package presence alone must not create a publisher or
consumer surface.

## Combined Index Integration

Combined readers should project event/message facts into dependency surface
rows using the shared surface model.

Report behavior:

- Add event/message counts to dependency surface summaries.
- Render safe rows with source label, language, framework family, surface kind,
  direction, evidence tier, rule ID, file span, identity status, and caveats.
- Show static publish/consume candidate relationships only when stable
  destination identity matches across sources or within a source.
- Keep one-sided publisher or consumer evidence visible.
- Project `MessageBindingDeclared` rows with `operationDirection = bind` or
  `declare` as first-class message surface rows when they have safe static
  identity or as explicit gap rows when identity is dynamic, unsafe, or absent.
  Binding-only evidence must not disappear from combined reports.
- Use reduced coverage wording when an adapter or framework is partial.
- Apply existing combined report row caps and truncation-gap behavior to
  message surfaces and candidate edges. Large fan-out must be truncated with a
  rule-backed gap rather than rendering unbounded rows.

Candidate edge behavior:

- A shared static destination identity may create a `message-publish-consume`
  dependency edge for combined queries.
- The edge must carry supporting publisher and consumer fact IDs, rule IDs,
  evidence tiers, source labels, and caveats.
- The edge is not a call edge and must be labeled as asynchronous static
  publish/consume evidence.
- `message-publish-consume` must be rejected with a clear validation error if a
  user passes it where a surface kind is expected.
- Dynamic, hashed, ambiguous, or unsafe-omitted identities cannot create strong
  cross-source candidate edges unless a future rule explicitly defines the
  safety and confidence.

## Paths, Route Flow, And Reverse Queries

Paths should treat event/message publisher and consumer facts as surfaces and,
where enabled, as asynchronous boundary nodes.

Rules:

- Endpoint-to-publisher paths may be shown when existing static call/argument
  edges reach the publish fact.
- Consumer-to-downstream paths may be shown when existing static call/argument
  edges start from or pass through the handler.
- Publisher-to-consumer transitions by destination identity should be a labeled
  async candidate hop, not a function call.
- Reverse queries can select `message-queue`, `message-topic`,
  `message-event`, `message-stream`, `message-exchange`, `message-channel`, or
  all message surfaces.
- Reverse and path selectors should support direction filtering separately from
  surface kind, for example publisher, consumer, binding, or all directions.
  If direction filtering is not implemented in the first slice, reports must
  emit an unavailable gap with reason `direction-filter-not-supported` rather
  than folding publishers and consumers together silently.
- Route-flow reports can show message boundary nodes only with explicit async
  caveats.
- `NoPathEvidence` must remain separate from dynamic destination,
  unsupported-framework, missing-edge, or reduced-coverage gaps.

## Reducer Context

Reducer integration should be context-first:

- Event/message surfaces can be supporting context for contract deltas when the
  reducer already has stable matches to destination identity, event type,
  handler/publisher symbol, DTO symbol, package/config evidence, or bounded
  path context.
- Name-only, syntax-only, dynamic, hashed, ambiguous, or high fan-out evidence
  should downgrade to `NeedsReview` or an existing gap-aware classification.
- Destination-level matches do not prove payload compatibility or runtime
  breakage.
- Payload contract claims should wait for explicit serializer/schema evidence.

Noisy event names such as generic status/update/create/delete-like names should
use the same conservative fan-out posture as other noisy contract names.

## Storage And Schema Notes

Prefer reusing the existing fact storage schema and combined surface projection.
If implementation needs schema changes, add them through the shared language
adapter contract and update schema compatibility tests.

Recommended storage approach:

- emit facts in `facts.ndjson`;
- persist facts in `index.sqlite`;
- attach symbol IDs through existing symbol/fact-symbol mechanisms;
- attach call/object/argument relationships through existing graph tables;
- project message surfaces in reporting/query layers without duplicating raw
  unsafe values.

Generated outputs should preserve stable JSON schemas where possible. If a
report schema adds a new message-surface section or enum value, follow the
existing `dependency-report.json`, `paths-report.json`, `reverse-report.json`,
and related combined-query conventions: update the documented report contract
or version field when one exists, add compatibility tests for required empty
arrays/sections, and keep byte-stable output for identical inputs.

## Safety And Redaction

Use existing safe path, hashing, Markdown escaping, and public-output guards.
Event/message-specific unsafe values include:

- queue names that look secret-like or environment-specific;
- topic names containing hostnames, URLs, tenant IDs, or credentials;
- broker connection strings;
- raw subscription/group IDs when configured as unsafe;
- raw routing keys with private tenant or environment data;
- raw config values and source snippets.

Safe output can render generic framework families, operation directions,
surface kinds, evidence tiers, rule IDs, safe relative file spans, and hashes.
When in doubt, keep identity hashed and label the row `hashed` or
`unsafe-omitted`.

## Implementation Slices

Recommended reviewable slices:

1. Shared vocabulary, rule catalog, safe identity helper, and .NET fixture
   tests for publisher/consumer/binding/gap evidence.
2. .NET extraction for a small set of high-value frameworks plus syntax fallback
   and failed-build coverage.
3. Combined report and surface projection support.
4. Paths, route-flow, and reverse-query support for message surfaces.
5. Reducer context integration with conservative downgrade rules.
6. TypeScript/Python/JVM adapter slices, each with its own fixtures and reduced
   coverage labels.

Each slice should update this spec's task checkboxes only when implemented and
validated.
