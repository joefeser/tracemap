# Legacy Remoting Flow Composition Requirements

## Introduction

TraceMap now emits deterministic static .NET Remoting evidence for API usage,
`MarshalByRefObject` types, channel construction and registration,
`RemotingConfiguration` registrations, `Activator.GetObject` activations, and
`<system.runtime.remoting>` config declarations. The legacy flow/path model
already composes WebForms, API, WCF, HTTP, SQL/query, dependency-surface, and
legacy data metadata evidence into conservative static path reports.

This phase integrates existing Remoting facts into that composition layer as a
sibling to WCF evidence. Remoting may appear as terminal service-boundary
evidence or as intermediate static context when a flow reaches a registration,
activation, channel, or Remoting-capable object candidate. It remains static
evidence only. It must not prove runtime channel configuration, remote object
lifetime, process boundaries, deployment, endpoint reachability, production
usage, exploitability, or impact.

Public claim level: hidden until validated through reviewed redacted summaries
or checked-in public fixtures.

## Scope

In scope:

- Read existing `Remoting*` facts and Remoting-related `AnalysisGap` facts from
  `index.sqlite` or combined indexes.
- Add Remoting evidence to `tracemap paths --view legacy-flows` and
  `--include-legacy-roots` output as static terminal or intermediate evidence.
- Preserve source Remoting fact IDs, rule IDs, evidence tiers, file spans,
  commit SHA, extractor versions, coverage labels, and limitations.
- Add conservative Remoting node/terminal kinds and selector support where it
  fits the existing path selector model.
- Emit availability and analysis gaps when Remoting facts are absent, ambiguous,
  unsupported, or from older indexes.
- Extend tests and validation guidance for Remoting flow composition and
  redaction.

Out of scope:

- No new Remoting scanner extraction.
- No runtime Remoting activation, endpoint probing, network calls, process
  inspection, deployment inspection, machine.config evaluation, or config
  transform execution.
- No proof of process boundary, application-domain boundary, channel listener
  startup, remote object lifetime, lease behavior, or object activation at
  runtime.
- No WCF fact reuse for Remoting-specific evidence.
- No reducer claim that a changed contract is impacted solely because Remoting
  evidence appears in a path.
- No public site claim promotion.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No raw URLs, object URIs, config values, local absolute paths, private repo
  names, raw remotes, source snippets, secrets, or connection strings in
  generated artifacts.

## Requirements

### Requirement 1: Remoting Inputs

**User Story:** As a maintainer, I want legacy flow composition to consume the
existing Remoting detector output without rerunning or changing the scanner.

#### Acceptance Criteria

1. WHEN an index contains `RemotingApiUsageDeclared`,
   `RemotingMarshalByRefObjectDeclared`, `RemotingChannelDeclared`,
   `RemotingChannelRegistered`, `RemotingServiceTypeRegistered`,
   `RemotingClientTypeRegistered`, `RemotingClientActivationDeclared`,
   `RemotingConfigSectionDeclared`, `RemotingConfigChannelDeclared`,
   `RemotingConfigServiceDeclared`, `RemotingConfigClientDeclared`, or
   `RemotingConfigProviderDeclared` facts THEN the legacy flow reader SHALL read
   them as Remoting inputs.
2. WHEN an index contains Remoting-related `AnalysisGap` facts from
   `legacy.remoting.*` rules THEN the legacy flow reader SHALL preserve them as
   Remoting gaps or path notes.
3. WHEN Remoting fact tables, columns, extractor versions, or rule IDs are
   unavailable because the index predates Remoting extraction THEN the report
   SHALL emit an availability gap rather than treating Remoting as absent.
4. WHEN Remoting facts include supporting fact IDs, symbol IDs, file spans,
   rule IDs, evidence tiers, extractor versions, coverage labels, or commit SHA
   THEN the composed output SHALL preserve those values in deterministic order.
5. The implementation SHALL NOT introduce new scanner facts or reinterpret
   WCF facts as Remoting facts.

### Requirement 2: Remoting Static Nodes And Terminals

**User Story:** As a reviewer, I want Remoting registrations and endpoints to be
visible in legacy flow reports without implying runtime proof.

#### Acceptance Criteria

1. WHEN a static path reaches a Remoting service registration, config service
   declaration, client activation, client registration, or config client
   declaration THEN `tracemap paths --view legacy-flows` SHALL display a
   `remoting-endpoint` or `remoting-registration` terminal using safe labels and
   source provenance.
2. WHEN a static path reaches channel construction, channel registration, config
   channel, config provider, Remoting API usage, or `MarshalByRefObject` object
   evidence THEN the report SHALL display it according to the terminal
   precedence rules in the design, either as intermediate `remoting-channel`,
   `remoting-api`, or `remoting-object` evidence or as a lower-precedence
   selected terminal.
3. WHEN a path reaches only `MarshalByRefObject` inheritance with no channel,
   registration, activation, config, or call/object context THEN it SHALL be no
   stronger than `NeedsReviewStaticPath` and SHALL say this is object-shape
   evidence only.
4. WHEN Remoting activation and service registration facts share only hashed
   URL/object-URI-like values or type names THEN the composer SHALL NOT merge
   them into a client-to-server proof unless an existing deterministic static
   edge or same-source symbol/call relationship supports the connection.
5. WHEN Remoting facts and WCF facts appear in the same path report THEN they
   SHALL remain separate sibling evidence families with distinct node kinds,
   fact types, rule IDs, and limitations.
6. WCF operation terminal behavior SHALL remain unchanged. Remoting terminals
   are additional terminal kinds and SHALL NOT create outbound traversal through
   service implementation unless a future spec defines explicit continuation
   evidence.

### Requirement 3: Conservative Classification And Gaps

**User Story:** As a user, I want Remoting path labels to make review value clear
without overstating what static evidence proves.

#### Acceptance Criteria

1. WHEN a path is supported by semantic or structural root evidence plus
   Remoting registration/config evidence with no unresolved ambiguity and full
   required coverage THEN it MAY be classified as `ProbableStaticPath` at
   strongest.
2. WHEN any required Remoting link is syntax-only, name-only, based on
   `MarshalByRefObject` object shape, high fan-out, ambiguous, generated-code
   uncertain, or partially covered THEN the path SHALL be capped at
   `NeedsReviewStaticPath`, `ReducedCoverage`, or `AnalysisGap`.
3. Remoting evidence alone SHALL NOT produce `StrongStaticPath` because static
   Remoting facts do not prove runtime channel setup, process boundary,
   activation, lifetime, deployment, or endpoint reachability.
4. WHEN channel registration cannot be linked to a channel declaration using
   existing supporting fact IDs or deterministic same-source evidence THEN the
   report SHALL preserve both facts independently and include an unsupported-link
   gap or note.
5. WHEN activated service/client registration details, dynamic arguments,
   config includes, encrypted sections, transforms, factories, reflection,
   dependency injection, or machine.config are required to understand a Remoting
   boundary THEN the report SHALL emit an analysis gap rather than infer the
   missing link.
6. WHEN no Remoting evidence is found under full Remoting extractor availability
   THEN the report SHALL emit a Markdown note and JSON summary status stating
   that no Remoting evidence was found under available coverage. It SHALL NOT
   claim the repository does not use Remoting at runtime.
7. WHEN Remoting extractor availability is missing or reduced THEN absence SHALL
   be reported as `ExtractorUnavailable: legacy-remoting`,
   `SchemaMissing: legacy-remoting`, `ReducedCoverage`, or `AnalysisGap`, not
   clean absence.

### Requirement 4: Query And Display Behavior

**User Story:** As a maintainer, I want Remoting evidence to fit the existing
legacy flow command and selector model.

#### Acceptance Criteria

1. WHEN a user runs `tracemap paths --view legacy-flows` or
   `tracemap paths --include-legacy-roots` against an index with Remoting facts
   THEN Markdown and JSON outputs SHALL summarize Remoting evidence counts,
   terminal counts, gaps, classifications, and representative paths.
2. WHEN `--to-surface remoting-endpoint`, `--to-surface remoting-registration`,
   or `--to-surface remoting-channel` is provided THEN the selector SHALL match
   the corresponding safe Remoting terminal or intermediate evidence.
3. WHEN `--surface-name` is used with Remoting evidence THEN matching SHALL use
   safe type names, safe fact IDs, or display hashes through existing
   exact/wildcard selector behavior only; raw URL, object URI, config value, or
   local path matching SHALL NOT be required.
4. WHEN Remoting facts are selected by fact ID or symbol-backed safe identity
   THEN the report SHALL keep each source-local candidate separate and SHALL NOT
   stitch across sources by short name alone.
5. Markdown wording SHALL use phrases such as "static Remoting evidence",
   "possible static path", and "Remoting boundary candidate". It SHALL NOT say
   that a Remoting channel opened, an object was activated remotely, a process
   boundary exists, a service is deployed, or a call impacted a remote process.
6. JSON output SHALL remain versioned and deterministic. If adding Remoting
   fields is non-breaking for `legacy-flow.v1`, keep the existing schema version;
   if consumers would misinterpret the shape, define a new schema version.

### Requirement 5: Redaction And Public Artifact Safety

**User Story:** As a maintainer, I want Remoting flow output to be safe for
reviewed public artifacts.

#### Acceptance Criteria

1. Remoting flow Markdown, JSON, logs, and display fields SHALL NOT include raw
   Remoting URLs, object URIs, channel ports, channel names, provider
   properties, config values, local absolute paths, private repo names, raw
   remotes, source snippets, secrets, or connection strings.
2. Unsafe values SHALL be omitted or represented by stable hashes already
   produced by source facts, with `legacy.flow.redaction.v1` included when the
   flow report performs additional display redaction.
3. File paths SHALL be repository-relative paths only when safe; source labels
   from combined indexes SHALL be neutralized when they look private or
   unreviewed.
4. Raw selector values that look like URLs, paths, config strings, SQL,
   connection strings, or secrets SHALL NOT be echoed in logs or error messages.
5. Public claim level SHALL remain hidden in implementation state and docs for
   this spec until a later reviewed promotion task changes it.

### Requirement 6: Tests And Validation

**User Story:** As a reviewer, I want focused tests that prove Remoting
composition is deterministic, conservative, and redacted.

#### Acceptance Criteria

1. Tests SHALL cover Remoting service/client registration terminals, activation
   terminals, channel intermediate nodes, config section/channel/service/client
   facts, `MarshalByRefObject` object-shape evidence, and Remoting-related
   analysis gaps.
2. Tests SHALL cover paths from WebForms or API/service roots to Remoting
   evidence when connected by existing call, object creation, symbol, or fact
   evidence.
3. Tests SHALL prove Remoting evidence cannot produce `StrongStaticPath`.
4. Tests SHALL prove WCF and Remoting evidence remain distinct when both appear
   in one index and one path report.
5. Tests SHALL cover selector behavior for `remoting-endpoint`,
   `remoting-registration`, `remoting-channel`, safe fact IDs, safe type names,
   display hashes, selector no-match gaps, and ambiguous source-local matches.
6. Tests SHALL prove raw URLs, object URIs, config values, ports, local absolute
   paths, private labels, raw remotes, snippets, and secrets do not appear in
   Markdown, JSON, logs, or SQLite-derived display fields.
7. Tests SHALL prove deterministic ordering and byte-stable JSON for identical
   input rows, including row-order permutation cases.
8. Implementation validation SHALL include the legacy static flow validation
   commands from `docs/VALIDATION.md`, Remoting smoke guidance where relevant,
   `dotnet build`, `dotnet test`, private-path checks, `git diff --check`, and
   explicit deferral of unavailable public Remoting smoke baselines.
