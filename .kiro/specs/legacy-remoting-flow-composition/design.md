# Legacy Remoting Flow Composition Design

## Overview

Add existing .NET Remoting facts to the legacy static flow/path composition
model. The feature reads already-emitted Remoting facts from indexes, projects
them into the `tracemap paths --view legacy-flows` graph, and reports them as
static service-boundary evidence alongside WCF.

Intended static evidence shape:

```text
WebForms/API/service root
  -> call, creation, symbol, projection, or parameter-forward evidence
  -> Remoting API/channel/registration/config/activation/object evidence
  -> remoting terminal or intermediate node in legacy-flow output
```

Every edge remains static. A Remoting path says that code or config contains
evidence near a possible Remoting boundary. It does not prove runtime channel
setup, process boundary, remote object activation, object lifetime, deployment,
endpoint reachability, production traffic, security exposure, exploitability, or
impact.

## Relationship To Existing Specs

This spec builds on:

- `.kiro/specs/legacy-remoting-detection/`
- `.kiro/specs/legacy-flow-composition-reporting/`
- `.kiro/specs/legacy-wcf-metadata-normalization/`
- `.kiro/specs/legacy-webforms-event-flow/`

The Remoting detector owns scanner facts and source rule IDs. The legacy flow
composer owns graph projection, terminal selection, classification, output
wording, and redaction. This phase should not change Remoting extraction unless
tests reveal a reader compatibility bug that cannot be handled in reporting.

WCF and Remoting are sibling evidence families. Shared legacy-flow output may
group both under "legacy service boundary evidence", but facts, rule IDs, node
kinds, terminals, and limitations remain distinct.

## Existing Remoting Inputs

Read these fact types when present:

| Fact type | Flow role |
| --- | --- |
| `RemotingApiUsageDeclared` | Intermediate context or weak terminal only when directly selected |
| `RemotingMarshalByRefObjectDeclared` | Object-shape intermediate or review-tier terminal |
| `RemotingChannelDeclared` | Intermediate channel setup evidence |
| `RemotingChannelRegistered` | Intermediate or terminal channel registration evidence |
| `RemotingServiceTypeRegistered` | Service-side registration terminal |
| `RemotingClientTypeRegistered` | Client-side registration terminal |
| `RemotingClientActivationDeclared` | Client activation terminal |
| `RemotingConfigSectionDeclared` | Config context |
| `RemotingConfigChannelDeclared` | Config channel intermediate or terminal |
| `RemotingConfigServiceDeclared` | Config service terminal |
| `RemotingConfigClientDeclared` | Config client terminal |
| `RemotingConfigProviderDeclared` | Provider intermediate evidence |
| `AnalysisGap` with `legacy.remoting.*` rule IDs | Gap propagation and classification cap |

Expected source rule IDs:

- `legacy.remoting.api.v1`
- `legacy.remoting.marshal-by-ref.v1`
- `legacy.remoting.channel.v1`
- `legacy.remoting.registration.v1`
- `legacy.remoting.config.v1`

No new scan-time Remoting fact types are proposed. If the flow implementation
needs a report-only note, prefer existing `legacy.flow.*` gap/note mechanisms
over persisting new scanner facts.

## Node And Surface Model

Add Remoting node kinds to the legacy-flow view:

| Node kind | Source facts | Terminal? |
| --- | --- | --- |
| `remoting-endpoint` | service/client registration, activation, config service/client | Yes |
| `remoting-registration` | service/client registration, channel registration, configure evidence | Yes, when selected or path-ending |
| `remoting-channel` | channel construction/registration/config channel/provider | Usually intermediate; terminal when selected |
| `remoting-object` | `MarshalByRefObject` evidence | Intermediate or review-tier terminal |
| `remoting-api` | API usage evidence | Intermediate or selected review-tier terminal |
| `gap` | Remoting `AnalysisGap` | Report gap, not traversal node by default |

Suggested `--to-surface` additions:

- `remoting-endpoint`
- `remoting-registration`
- `remoting-channel`

`remoting-endpoint` is a display category for static registration, activation,
or config declaration evidence. It does not mean a network endpoint exists at
runtime.

Do not create a Remoting-specific graph engine. Extend the existing legacy-flow
graph builder and output model used by `tracemap paths --view legacy-flows`.

## Terminal Semantics

Remoting service/client registration and activation facts are terminal
boundaries in v1. Traversal stops there unless the path was already traversing
through stronger direct call/object/symbol evidence to reach another terminal.
The composer must not treat a service registration as proof that service-side
implementation code executes, and must not continue from a Remoting endpoint to
downstream SQL/HTTP/service evidence without explicit future continuation rules.

Terminal precedence is deterministic:

1. Service/client registration, client activation, config service, and config
   client facts are always `remoting-endpoint` or `remoting-registration`
   terminals when reached by a path.
2. Channel registration, config channel, and config provider facts are terminals
   only when the path has no stronger terminal from item 1 and the user selected
   `--to-surface remoting-channel`; otherwise they are intermediate context.
3. Remoting API usage and `MarshalByRefObject` facts are terminals only when the
   path has no stronger terminal from items 1 or 2 and a future explicit selector
   supports those surfaces; otherwise they are intermediate or review notes.

When multiple terminals of the same precedence are reached from one root, emit
separate candidate results ordered by terminal kind, safe display key or hash,
and fact ID. "Connected" means reachable through the bounded directed traversal,
not merely co-present in the same index.

`MarshalByRefObject` evidence is object-shape only. It can support review value
near a path, but by itself it cannot prove hosting, registration, activation,
lifetime, reachability, or process boundaries.

## Edge Construction

Use only existing deterministic evidence:

- fact-symbol attachments;
- call edges;
- object creation edges;
- symbol relationships where already supported by legacy-flow composition;
- parameter-forward edges when available;
- supporting fact IDs already emitted by Remoting channel registration facts;
- same-source direct relationships already represented in the index;
- `WebFormsEventFlowProjected` fallback edges, without confidence upgrade.

Do not add edges based on:

- matching raw or hashed URLs alone;
- matching object URI hashes alone;
- matching short type names across sources;
- matching config values;
- remote channel protocol names alone;
- private source labels;
- arbitrary string literals;
- reflection, dependency injection, factories, machine.config, transforms, or
  external config includes.

When the source facts contain `supportingFactIds`, use them as corroboration for
same-source channel declaration to registration context. Consumers must tolerate
semicolon-delimited and comma-delimited historical formats, as the Remoting
detection design requires.

Supporting fact ID parsing rules:

- Treat `null`, missing, or whitespace-only `supportingFactIds` as no
  supporting IDs.
- If the field contains semicolons and no commas, split on semicolons.
- If the field contains commas and no semicolons, split on commas for
  backward-compatible reads.
- If the field contains both semicolons and commas, do not guess; emit a
  `MalformedSupportingFactIds` gap and ignore the malformed supporting-ID field
  for edge construction.
- Trim ASCII whitespace from each token.
- Drop empty tokens after trimming.
- De-duplicate IDs with ordinal comparison and sort them before graph use.
- Tests must cover semicolon format, comma format, empty fields, duplicate IDs,
  and mixed-delimiter malformed input.

## Classification

Remoting paths use the existing legacy-flow classifications:

- `StrongStaticPath`
- `ProbableStaticPath`
- `NeedsReviewStaticPath`
- `NoBackendEvidence`
- `ReducedCoverage`
- `AnalysisGap`

Remoting-specific cap rules:

| Evidence shape | Maximum classification |
| --- | --- |
| Full root plus structural Remoting config/registration terminal, no ambiguity | `ProbableStaticPath` |
| Semantic root plus syntax-only Remoting registration or activation | `NeedsReviewStaticPath` |
| `MarshalByRefObject` only | `NeedsReviewStaticPath` |
| Channel construction without linked registration | `NeedsReviewStaticPath` |
| Remoting API usage only | `NeedsReviewStaticPath` |
| Dynamic registration/config shape, unsupported activated-registration detail, missing config include, encrypted section, transform-dependent evidence | `AnalysisGap` or `ReducedCoverage` |
| Missing Remoting extractor/table availability | `AnalysisGap` or report-level availability gap |

Remoting evidence must not produce `StrongStaticPath`. Strong static paths
require end-to-end static evidence that Remoting cannot provide without runtime
or deployment proof. Even when the root and graph edges are semantic, the
Remoting terminal itself caps the result at `ProbableStaticPath` because source
Remoting rules explicitly do not prove runtime channel setup, process boundary,
object lifetime, activation success, endpoint reachability, deployment, or
production usage. Any future runtime or deployment proof must introduce new
documented rule IDs and evidence contracts rather than weakening this static
Remoting cap.

If a path contains both WCF and Remoting terminals, keep them as separate
candidate results unless a deterministic static path truly includes both. Do not
merge WCF operation terminal identity with Remoting endpoint identity.

## Gap Kinds

Use existing `legacy.flow.gap-propagation.v1` and
`legacy.flow.input-availability.v1` mechanisms for report-level gaps. Suggested
gap/note codes:

- `ExtractorUnavailable: legacy-remoting`
- `SchemaMissing: legacy-remoting`
- `RemotingReducedCoverage`
- `UnsupportedRemotingChannelLink`
- `UnsupportedRemotingActivationDetail`
- `DynamicRemotingRegistration`
- `UnresolvedRemotingObjectShape`
- `RemotingRuntimeProofUnavailable`
- `AmbiguousRemotingTerminal`
- `RemotingSelectorNoMatch`
- `MalformedSupportingFactIds`

These are report gap codes, not new scanner rule IDs. They should include the
source Remoting rule IDs when a source fact exists.

Availability detection:

- Full Remoting availability requires the index to expose the generic `facts`
  storage used by current TraceMap indexes, current fact properties, scan
  manifest or source metadata that proves the scanner/schema version includes
  Remoting fact support, and no schema errors while querying all known
  `Remoting*` fact types.
- A current index with zero matching Remoting facts and no Remoting-related
  `AnalysisGap` facts is full availability with zero evidence when the scan
  manifest, schema version, or extractor-version metadata proves Remoting fact
  support was available to the scanner. The composer must not require
  Remoting-specific emitted facts to prove zero-evidence availability.
- If the index predates the manifest/schema metadata needed to prove Remoting
  support, cannot expose required fact properties, or cannot be queried for
  Remoting fact types, emit `SchemaMissing: legacy-remoting`.
- If extractor metadata is present and proves Remoting support was not available,
  emit `ExtractorUnavailable: legacy-remoting`.
- If Remoting-related `AnalysisGap` facts exist, preserve them and mark Remoting
  availability as reduced for classification.

Gap and note placement:

- Full availability plus zero Remoting facts emits a Markdown note and JSON
  summary status: "No Remoting evidence found under available Remoting extractor
  coverage; this does not prove Remoting is unused at runtime."
- Reduced or unavailable Remoting availability emits a JSON gap and Markdown
  limitation using `legacy.flow.input-availability.v1` or
  `legacy.flow.gap-propagation.v1`.
- Path-specific Remoting gaps appear on the affected result; report-level
  availability gaps appear once in the top-level gaps section.

## Display And Redaction

Allowed display fields:

- fact IDs;
- rule IDs;
- evidence tiers;
- safe fact type names;
- safe type names and namespaces already emitted by source facts;
- safe assembly names when source facts already allow them;
- repository-relative paths and line spans;
- stable hashes such as `urlHash`, `objectUriHash`, `valueHash`, or
  `applicationNameHash`;
- neutral source labels.

Forbidden display fields:

- raw Remoting URLs;
- raw object URIs;
- raw channel ports;
- raw channel names when source policy treats them as config values;
- provider property values;
- config values;
- local absolute paths;
- private repository names or clone labels;
- raw remotes;
- source snippets;
- connection strings;
- secrets or secret-looking tokens.

`--surface-name` should match only safe display labels, fact IDs, safe type
identities, or display hashes. A user should not need to pass a raw URL or
object URI to find Remoting evidence.

Hash display and selector format:

- Markdown and JSON display hashes as `<kind>-<prefix>`, for example
  `url-1a2b3c4d`, `objectUri-9f8e7d6c`, `value-0123abcd`, or
  `application-abcdef12`.
- The prefix is the first eight lowercase hex characters of the existing full
  source hash. The full hash remains available to the reader when stored by the
  source fact, but output uses the display prefix.
- Display hashes are safe generated surface identities. `--surface-name` uses the
  existing legacy-flow exact/wildcard behavior against those display identities;
  it does not introduce a Remoting-only implicit prefix matcher. A user may pass
  an exact display hash such as `url-1a2b3c4d`, or use the existing wildcard form
  when wildcard matching is desired.
- Exact fact IDs and safe type names sort before wildcard display-hash matches.
- If a wildcard display-hash selector matches multiple facts, return all matches
  deterministically and add an ambiguity note; do not choose one.
- Never require or accept raw URL/object URI/config values for selector matching
  when a corresponding hash exists.

Source label neutralization:

- Reuse the existing legacy-flow source-label guard when possible.
- Replace labels with `source-<stable-hash-prefix>` when they contain path
  separators, raw remotes, domain-like values, IP-like values, non-ASCII
  characters, or case-insensitive tokens such as `internal`, `private`, `corp`,
  `customer`, `prod`, or `client`.
- Preserve simple neutral labels only when they pass the existing safe-display
  policy.
- Include `legacy.flow.redaction.v1` when neutralization changes a source label.

If the output writer hashes or omits a value beyond what the source fact already
did, include `legacy.flow.redaction.v1` in the result notes.

## JSON And Markdown

Prefer the existing `legacy-flow.v1` report shape if Remoting nodes can be added
as additive node kinds and surface kinds. Keep deterministic ordering by:

1. classification severity/order;
2. source label;
3. root node kind and safe display key/hash;
4. terminal kind and safe display key/hash;
5. fact ID;
6. path ID.

If adding Remoting surface values would cause v1 consumers to misinterpret
output, introduce a successor schema and document the break before
implementation. The preferred path is additive compatibility.

Markdown sections should include:

- separate WCF and Remoting service-boundary subsections or clearly labeled
  grouped sections; the two families must not be merged into one terminal row;
- summary counts by Remoting node kind and source rule ID;
- representative Remoting static paths;
- roots with Remoting-adjacent evidence but no safe terminal;
- Remoting gaps and reduced-coverage notes;
- limitations that explicitly deny runtime channel, process boundary, object
  lifetime, deployment, and reachability proof.

JSON should keep WCF and Remoting path results as separate result objects with
distinct terminal kinds and rule ID families. If one root reaches both a WCF
operation and a Remoting endpoint through distinct call chains, emit two
separate path results. Classification is per path; a path cannot become stronger
because unrelated WCF and Remoting evidence are both present in the report.

Forbidden wording examples:

- "channel opened"
- "remote object activated"
- "cross-process call"
- "deployed endpoint"
- "proves reachability"
- "impacted remote service"
- "runtime endpoint"

## Validation Strategy

Focused tests should extend `LegacyFlowCompositionTests` or the current
equivalent path-reporting test suite.

Fixture coverage:

- WebForms or API root calling a type/method with Remoting client activation.
- Root reaches `RemotingServiceTypeRegistered` through call/object/symbol
  evidence.
- Config-only Remoting service/client/channel evidence appears as terminal or
  availability context without requiring a build.
- Channel declaration and registration with `supportingFactIds` create a
  connected channel context.
- Channel declaration without supported registration link stays independent and
  review-tier.
- `MarshalByRefObject` object-shape evidence alone cannot exceed
  `NeedsReviewStaticPath`.
- WCF and Remoting facts in one index produce separate terminals and rule IDs.
- Missing Remoting tables/extractor versions produce availability gaps.
- Selectors for `remoting-endpoint`, `remoting-registration`,
  `remoting-channel`, safe fact ID, safe type name, display hash, and no-match.
- Byte-stable JSON under row-order permutation.
- Redaction of URLs, object URIs, ports, config values, local paths, private
  labels, remotes, snippets, connection strings, and secrets.

Implementation validation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests
dotnet test src/dotnet/TraceMap.sln --filter LegacyRemotingExtractorTests
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Also run or explicitly defer relevant smoke guidance from `docs/VALIDATION.md`:

- Legacy Static Flow Reporting Smoke.
- Legacy Remoting Smoke.

No public Remoting smoke baseline exists yet. Any public Remoting baseline or
claim promotion must be a separate reviewed task.

## Deferred Follow-Ups

- Public Remoting validation baseline.
- Service-side continuation from Remoting registration into implementation
  methods. Deferred because current Remoting registration facts do not attach
  registered service types to callable remote methods or define continuation
  edges. Users still get root-to-Remoting-terminal evidence in this phase.
  Future work should add explicit scanner facts and traversal rules before
  continuing through service-side implementation evidence.
- Contract-change reducer integration beyond static path context.
- Richer activated-registration overload support.
- Machine.config, config transform, encrypted section, or external include
  resolution.
- Visual graph UI for legacy Remoting paths.
- Public site copy or claim promotion.
