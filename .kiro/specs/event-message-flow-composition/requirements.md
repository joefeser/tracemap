# Event Message Flow Composition Requirements

## Introduction

TraceMap can now inventory static event/message publisher, consumer, binding,
and static publish/consume candidate evidence. This follow-up defines how that
evidence can be composed into downstream review context without claiming broker
execution, production traffic, runtime publish/subscribe delivery, delivery
guarantees, payload compatibility, impact proof, or complete coverage.

Public claim level: hidden. Outputs from this slice are local review context
until a separate public-safe review promotes a narrower claim.

This is not AI impact analysis. The implementation must not add LLM calls,
embeddings, vector databases, or prompt-based classification.

## Current Context

- `event-message-surfaces` is `implemented-v1-with-follow-ups`.
- Live `origin/dev` and the live rule catalog/code must be rechecked before
  implementation because message reporting, paths, reverse, route-flow, and
  release-review code are active areas.
- Existing message rules include `message.surface.publish.v1`,
  `message.surface.consume.v1`, `message.surface.binding.v1`,
  `message.surface.identity.v1`, `message.surface.combine.v1`,
  `message.surface.candidate-edge.v1`, `message.surface.paths.v1`,
  `message.surface.reducer.v1`, and `message.surface.gap.v1`.
- Existing .NET code already projects `MessagePublisherSurface`,
  `MessageConsumerSurface`, `MessageBindingDeclared`, message directions,
  destination identity status, and `message-publish-consume` candidate edges
  into combined reporting/query surfaces.
- Existing deferred work includes route-flow async message-hop rendering,
  reducer context over message surfaces, Roslyn Tier1 message extraction, and
  non-.NET adapter slices.

## MVP Scope Decisions

- Compose existing event/message evidence into bounded review context.
- Preserve evidence provenance: rule IDs, evidence tiers, supporting IDs, file
  spans, commit SHA, extractor versions, coverage labels, and limitations.
- Define a shared closed vocabulary for event/message flow-context gaps before
  any new gap string is emitted.
- Require rule catalog entries and documented limitations before any new
  emitted rule ID or gap string lands in product code.
- Start with one deterministic report/query consumer path in PR 1. The
  recommended first slice is a combined-report message review context section
  derived only from existing combined message surface rows, candidate edges,
  source coverage, and known message gaps.
- Keep downstream reducer, release-review, route-flow async boundary rendering,
  cross-language extraction, and stronger semantic matching as deferred
  follow-ups unless a later implementer deliberately chooses a smaller
  equivalent first consumer path.

## Requirements

### Requirement 1: Hidden Static Review Context Only

**User Story:** As a maintainer, I want message evidence composed into review
context without overstating runtime behavior.

Acceptance Criteria:

1. WHEN event/message flow context is emitted THEN it SHALL be labeled as
   hidden/local review context.
2. WHEN context references publisher, consumer, binding, or candidate-edge
   evidence THEN it SHALL state that the evidence is static and SHALL NOT claim
   runtime broker delivery, topology, production traffic, subscriber liveness,
   ordering, retries, exactly-once behavior, dead-letter behavior, auth,
   retention, deployment reachability, payload compatibility, or impact.
3. WHEN a context row lacks enough evidence for a credible statement THEN it
   SHALL emit a rule-backed gap or reduced-coverage label rather than an
   affirmative conclusion.
4. WHEN public docs or site copy are touched by a future implementation THEN
   they SHALL keep this feature hidden unless a separate public-claim review
   explicitly promotes a safe summary.

### Requirement 2: Shared Context And Gap Vocabulary

**User Story:** As an implementer, I want a closed vocabulary so message
composition gaps are deterministic and reviewable across downstream commands.

Acceptance Criteria:

1. WHEN implementation introduces a new context rule, gap rule, context kind,
   gap kind, classification, or emitted string THEN the rule catalog SHALL be
   updated first with limitations.
2. WHEN a message context row is emitted THEN it SHALL include a rule ID,
   evidence tier, context kind, classification, coverage label, limitation
   text or limitation IDs, supporting fact IDs, supporting edge IDs where
   available, source labels, commit SHA values, and extractor versions.
3. WHEN a message gap is emitted THEN it SHALL include a rule ID, evidence tier,
   closed gap kind, classification, coverage label, supporting IDs where
   available, source labels, commit SHA values, and extractor versions.
4. WHEN an implementation needs a new gap kind not listed in the design THEN it
   SHALL update this spec or create a follow-up spec before emitting it.
5. WHEN existing `message.surface.*` rules are sufficient THEN implementations
   SHOULD reuse them rather than minting duplicate rule IDs.

### Requirement 3: Compose Existing Evidence Without New Extraction

**User Story:** As a reviewer, I want context over existing message facts
without waiting for new extractor breadth.

Acceptance Criteria:

1. WHEN PR 1 composes message context THEN it SHALL read only existing derived
   artifacts such as combined indexes, combined report rows, known gaps,
   dependency edges, and source metadata.
2. WHEN PR 1 runs THEN it SHALL NOT rescan source files, connect to brokers,
   inspect live topology, read production traffic, call schema registries, or
   infer runtime behavior from package presence alone.
3. WHEN source coverage is reduced, unknown, stale, or conflicting THEN context
   SHALL preserve reduced coverage and downgrade to review-needed or unknown
   classifications.
4. WHEN only one side of a publisher/consumer pair exists THEN context SHALL
   preserve one-sided evidence and SHALL NOT invent the missing side.
5. WHEN a candidate edge exists THEN context SHALL label it as static
   destination-match evidence, not a call edge or delivery proof.

### Requirement 4: PR 1 Report/Query Consumer Path

**User Story:** As an implementer, I want the first implementation PR to be
small enough to validate thoroughly.

Acceptance Criteria:

1. WHEN PR 1 is implemented THEN it SHALL add shared event/message context/gap
   vocabulary plus exactly one deterministic downstream report/query consumer
   path unless this spec is amended.
2. WHEN PR 1 uses combined reporting, which is the default unless
   `implementation-state.md` documents a narrower equivalent report/query path,
   THEN `tracemap report` SHALL emit a bounded hidden `messageReviewContext`
   JSON/Markdown section from existing message surface rows, candidate edges,
   source coverage labels, and message gaps.
3. IF PR 1 uses another consumer path instead THEN it SHALL be no broader than
   one command or one existing report section and SHALL document why it is the
   first consumer.
4. WHEN the first consumer path renders context THEN it SHALL preserve stable
   ordering and caps, emit truncation gaps when caps are hit, and include empty
   arrays/sections consistently.
5. WHEN the first consumer path is disabled, unsupported, selector-filtered, or
   has no compatible message evidence THEN it SHALL emit one of the closed
   statuses defined in the design: `not_requested`, `available`, `partial`,
   `unavailable`, `selector_no_match`, or `no_compatible_evidence`.

### Requirement 5: Safety And Redaction

**User Story:** As an operator, I want message review context to be safe for
local review artifacts by default.

Acceptance Criteria:

1. WHEN context is stored or rendered THEN it SHALL NOT include raw payload
   values, secrets, config values, connection strings, raw remotes, local
   absolute paths, raw source snippets, raw broker URLs, raw hostnames, raw
   subscription group IDs, or unsafe destination strings by default.
2. WHEN destination identity is static and safe THEN context MAY render the
   normalized destination key already approved by message-surface rules.
3. WHEN destination identity is hashed, dynamic, ambiguous, wildcarded, unsafe,
   or omitted THEN context SHALL render only safe status labels, hashes already
   approved by existing rules, and caveats.
4. WHEN Markdown is rendered THEN values SHALL be escaped with existing report
   helpers and SHALL not break tables or links.
5. WHEN JSON is rendered THEN values SHALL use stable schema fields, `null`,
   and empty arrays instead of omitted ad hoc structures where possible.

### Requirement 6: Conservative Classifications

**User Story:** As a reviewer, I want composed message context to lower
confidence when evidence is weak.

Acceptance Criteria:

1. WHEN evidence is Tier3, syntax-only, name-only, hash-only, dynamic,
   ambiguous, duplicate, generic, high fan-out, reduced-coverage, or
   source-identity-conflicted THEN context SHALL remain review-tier or unknown.
2. WHEN a stable destination match is the only bridge between publisher and
   consumer evidence THEN context SHALL classify it as `NeedsReview` or
   equivalent review-tier static context.
3. WHEN path, reverse, route-flow, or reducer context is unavailable THEN the
   output SHALL emit an explicit gap rather than silently omitting the missing
   context.
4. WHEN noisy names such as `status`, `update`, `event`, `message`, or `data`
   create high fan-out context THEN output SHALL cap and downgrade instead of
   promoting stronger findings.

### Requirement 7: Validation

**User Story:** As a maintainer, I want the spec to define enough validation to
make the implementation reviewable.

Acceptance Criteria:

1. WHEN implementation changes product code THEN focused tests SHALL cover
   static candidate context, one-sided context, reduced coverage, dynamic or
   hashed destination status, truncation, empty output, private-output safety,
   and deterministic ordering for the chosen consumer path.
2. WHEN rule catalog entries are added THEN catalog tests SHALL prove the new
   rule IDs and limitations exist before emitted rows use them.
3. WHEN `.NET` report/query behavior changes THEN run the relevant focused
   tests plus `dotnet test src/dotnet/TraceMap.sln` unless explicitly deferred
   with a reason in the implementation state.
4. WHEN validation finishes THEN run `git diff --check` and
   `./scripts/check-private-paths.sh`.

## Explicit Non-Goals

- No broker execution, runtime publish/subscribe delivery, live topology
  discovery, telemetry, production traffic, delivery guarantees, or consumer
  liveness.
- No payload compatibility, schema registry, serializer compatibility, or
  runtime breakage proof.
- No LLM, embedding, vector database, prompt classification, or AI judgment.
- No new scanner/extractor breadth in PR 1.
- No public-site copy or public claims in PR 1.
- No raw snippets or unsafe raw values by default.
