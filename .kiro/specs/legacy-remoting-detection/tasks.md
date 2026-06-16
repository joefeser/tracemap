# Legacy Remoting Detection Tasks

## Implementation Tasks

- [ ] 1. Add Remoting rule catalog entries and extractor constants. Requirements: 1, 2, 3, 4, 5, 6.
  - [ ] Add `legacy.remoting.api.v1`.
  - [ ] Add `legacy.remoting.marshal-by-ref.v1`.
  - [ ] Add `legacy.remoting.channel.v1`.
  - [ ] Add `legacy.remoting.registration.v1`.
  - [ ] Add `legacy.remoting.config.v1`.
  - [ ] Document limitations for static evidence, missing semantics, alias ambiguity, dynamic config, runtime reachability, deployment, network availability, production usage, security posture, and unsupported remoting shapes.
  - [ ] Do not add a report-only rule in v1 unless report code emits independent Remoting summary facts; report summaries should cite source Remoting extraction rules.
  - [ ] Add or update extractor version constants for Remoting evidence.

- [ ] 2. Define Remoting fact types and storage mappings. Requirements: 1, 2, 3, 4, 5, 6.
  - [ ] Add fact type constants for Remoting API usage, `MarshalByRefObject`, channels, channel registration, service registration, client registration, client activation, config section, config channels, config services, and config clients.
  - [ ] Preserve stable JSON and SQLite property encoding using existing fact serialization conventions.
  - [ ] Include rule ID, evidence tier, repository-relative path, line span, commit SHA, extractor version, and safe properties on every emitted fact.
  - [ ] Store linked supporting fact IDs in `supportingFactIds`, using semicolon-delimited values for new Remoting output while preserving consumer compatibility with existing comma-delimited legacy output.
  - [ ] Ensure JSON/index export paths round-trip new Remoting fact types and `supportingFactIds` values.
  - [ ] Add `AnalysisGap` coverage for malformed config, missing semantic model, ambiguous aliases, dynamic registration, external includes, encrypted sections, and unsupported remoting shapes.

- [ ] 3. Implement syntax fallback for Remoting API usage. Requirements: 1, 6.
  - [ ] Detect `System.Runtime.Remoting` namespace imports and fully qualified references.
  - [ ] Detect known Remoting API type references such as `RemotingConfiguration`, `ChannelServices`, `WellKnownObjectMode`, `ObjRef`, and `RemotingServices`.
  - [ ] Use Roslyn syntax nodes and exclude comments, string literals, XML doc text, and inactive preprocessor regions from evidence.
  - [ ] Cap ambiguous syntax-only matches at `Tier3SyntaxOrTextual` with documented limitations.
  - [ ] Add tests proving syntax evidence is emitted when MSBuild or semantic loading is unavailable, and that comments/strings/inactive regions do not emit Remoting facts.

- [ ] 4. Implement semantic Remoting symbol recognition. Requirements: 1, 2, 3, 4, 6.
  - [ ] Resolve known framework Remoting symbols where Roslyn semantic analysis succeeds.
  - [ ] Upgrade only compiler-resolved framework symbol evidence to `Tier1Semantic`.
  - [ ] Deduplicate or correlate semantic and syntax evidence deterministically.
  - [ ] Add tests proving project-defined lookalike names do not become strong Remoting evidence.

- [ ] 5. Extract `MarshalByRefObject` boundary candidates. Requirements: 2, 6.
  - [ ] Detect direct syntax inheritance from `MarshalByRefObject`.
  - [ ] Detect direct semantic inheritance from `System.MarshalByRefObject`.
  - [ ] Detect indirect semantic inheritance only when same-repository, same-file, or explicit registration/config Remoting context exists; otherwise omit or gap the broad indirect inheritance evidence.
  - [ ] Preserve safe type identity, partial/abstract/generated metadata where available, and supporting line spans.
  - [ ] If existing generated-code detection does not cover a code shape, omit `isGenerated` and do not fabricate a value; document the gap or limitation where appropriate.
  - [ ] Do not infer hosting, activation, reachability, or production usage from inheritance alone.
  - [ ] Add tests for direct semantic inheritance, gated indirect semantic inheritance, WinForms/framework-style indirect inheritance without Remoting context, syntax fallback, partial classes, and ambiguous base names.

- [ ] 6. Extract channel construction and registration. Requirements: 3, 6.
  - [ ] Detect `TcpChannel`, `HttpChannel`, `IpcChannel`, and server/client channel variants.
  - [ ] Detect `ChannelServices.RegisterChannel` calls.
  - [ ] Link channel construction to registration only for inline construction or same-method single-unreassigned-local registration.
  - [ ] Emit independent facts plus an `AnalysisGap` for unsupported cross-method, field/property, reassigned-local, factory-helper, dependency-injection, reflection, or collection-based links.
  - [ ] Hash or omit unsafe constructor arguments, channel properties, provider values, URLs, names, and ports.
  - [ ] Emit review-tier evidence or gaps for dynamic factories, reflection, config-backed values, or unresolved channel variables.
  - [ ] Add tests for construction, registration, linked registration, dynamic gaps, and redaction.

- [ ] 7. Extract Remoting registration and activation API calls. Requirements: 4, 6.
  - [ ] Detect `RegisterWellKnownServiceType`.
  - [ ] Detect `RegisterWellKnownClientType`.
  - [ ] Detect `RegisterActivatedServiceType` and `RegisterActivatedClientType`; if full argument support is deferred, visible calls SHALL emit `AnalysisGap` with `legacy.remoting.registration.v1` and limitation `activated-type-registration-v1-deferred`.
  - [ ] Detect `RemotingConfiguration.Configure`.
  - [ ] Detect `Activator.GetObject` only when semantic analysis resolves the requested type to a `MarshalByRefObject`-derived type or the same file has channel/registration evidence; otherwise emit a review gap for potentially relevant calls rather than a Remoting activation fact.
  - [ ] Assert `Activator.CreateInstance` without semantic Remoting activation proof emits no Remoting fact and no Remoting gap.
  - [ ] Preserve safe target type identity and enum mode; hash or omit URLs, object URIs, config values, and arbitrary strings.
  - [ ] Add tests for common overloads, missing arguments, dynamic arguments, literal `Singleton`, literal `SingleCall`, variable/expression `WellKnownObjectMode` limitations, activated-registration deferred gaps, isolated `Activator.GetObject`, gated `Activator.GetObject`, and redaction.

- [ ] 8. Extract `<system.runtime.remoting>` config evidence. Requirements: 5, 6.
  - [ ] Reuse a `LoadSafeXml`-style helper with DTD/external entity protection.
  - [ ] Do not use bare `XDocument.Load` without `XmlReaderSettings` for Remoting config files.
  - [ ] Detect `<system.runtime.remoting>` sections in config files.
  - [ ] Extract safe structured facts for channels, service well-known declarations, activated service declarations, client declarations, and providers.
  - [ ] Preserve line spans where XML line info is available.
  - [ ] Hash or omit raw URLs, ports, object URIs, application names, provider properties, config values, local paths, and secret-like values.
  - [ ] Emit `AnalysisGap` for malformed XML, unsupported custom sections, external includes, encrypted sections, and unresolved transforms.
  - [ ] Add tests for supported config shapes, malformed config, unsupported config, and privacy guards.

- [ ] 9. Update scanner report summaries safely. Requirements: 6.
  - [ ] Register `LegacyRemotingExtractor.Extract` or the equivalent scanner hook in `ScanEngine.cs` alongside existing legacy extractors.
  - [ ] Add Remoting counts and static limitations to scan report output where legacy boundary summaries are rendered.
  - [ ] Keep Remoting fact types and rules distinct from WCF/SVC facts.
  - [ ] Ensure wording says "static evidence", "candidate", or "needs review" rather than runtime host, reachable endpoint, exploit, production usage, or proven impact.
  - [ ] Add full scan-engine integration coverage proving Remoting facts appear in `ScanResult`.
  - [ ] Add report snapshot or assertion tests for safe wording and redaction.

- [ ] 10. Add a checked-in synthetic Remoting sample. Requirements: 7.
  - [ ] Add a small synthetic sample under `samples/dotnet-remoting-sample/` or an equivalent checked-in fixture location.
  - [ ] Include redaction-safe C# and config evidence for `MarshalByRefObject`, channel registration, well-known service/client registration, `Activator.GetObject`, and `<system.runtime.remoting>`.
  - [ ] Use only synthetic local-safe values; do not include real endpoint URLs, object URIs, ports, remotes, local paths, secrets, or source from public smoke repositories.
  - [ ] Ensure the sample passes `./scripts/check-private-paths.sh`.

- [ ] 11. Add focused fixtures and determinism tests. Requirements: 1, 2, 3, 4, 5, 6, 7.
  - [ ] Add fixture coverage for syntax-only C#, semantic C#, config XML, malformed config, dynamic registration, and redaction.
  - [ ] Add partial-class `MarshalByRefObject` coverage across files without duplicate unstable identities.
  - [ ] Add mixed WCF + Remoting coverage proving fact families remain separate.
  - [ ] Add scanner integration coverage using the checked-in synthetic Remoting sample.
  - [ ] Add semantic/syntax deduplication coverage so one resolved symbol does not produce duplicate Tier1 and Tier3 facts for the same evidence.
  - [ ] Add config coverage for a valid `<system.runtime.remoting>` section with only unrecognized children; emit the section fact plus an `AnalysisGap`.
  - [ ] Assert `configFileName` stores only a filename basename, not a full or relative path, when `RemotingConfiguration.Configure` is extracted.
  - [ ] Add byte-stability or deterministic ordering tests where existing scanner tests support them.
  - [ ] Assert facts carry rule IDs, evidence tiers, file paths, line spans, commit SHA, extractor versions, and safe properties.
  - [ ] Assert no raw source snippets are stored by default.
  - [ ] Assert no raw local paths, remotes, URLs, object URIs, ports, config values, secrets, or generated public-repo artifacts are emitted.

- [ ] 12. Update documentation and validation guidance only where user-visible behavior changes. Requirements: 6, 7.
  - [ ] Update rule catalog documentation.
  - [ ] Update `docs/VALIDATION.md` only if Remoting implementation adds a new validation workflow or pinned smoke expectation.
  - [ ] Keep public claims hidden until redacted validation summaries are reviewed.
  - [ ] Document that no pinned Remoting smoke exists yet unless one is explicitly added; any public-repo baseline requires a separate reviewed baseline task or spec.

- [ ] 13. Validate implementation. Requirements: 7.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] Run `tracemap scan` against the checked-in synthetic Remoting sample and verify required artifacts are produced and Remoting facts appear.
  - [ ] Run relevant pinned smoke checks from `docs/VALIDATION.md`, or record explicit deferral with rationale in implementation state.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Suggested PR Boundaries

- PR 1: Rule catalog, fact type constants, extractor version, syntax/semantic API usage, and `MarshalByRefObject` evidence.
- PR 2: Channel construction/registration plus Remoting registration and activation API calls.
- PR 3: `<system.runtime.remoting>` config extraction and privacy guards.
- PR 4: Report summaries, determinism hardening, docs, and final validation.

## Deferred Follow-Ups

- Runtime Remoting host activation or endpoint probing.
- Security or exploitability classification.
- Machine.config, config transform, and deployment-environment resolution.
- Full reflection, dependency-injection, factory-helper, or branch-feasibility analysis.
- Multi-index Remoting flow/path composition and reducer integration.
- Public site claims or marketing copy.
- Committed baselines from public repository smoke runs.
