# Package Admission and Revocation Correlation Requirements

GitHub issue: #690 (`High: correlate package admission and revocation evidence across portfolio paths`).

Status: implemented. Joe authorized implementation after the specification merged in #698; grouped implementation PRs PR1–PR5 shipped the record reader, correlation engine, composition and adapter evidence slices, comparison mode, external advisory claims, deployment references, and closure. `tasks.md` tracks the shipped slices; task 4.4 (reviewed pinned npm-lockfile smoke validation) remains open under issue #690.

## Summary

TraceMap must correlate externally governed package admission, rejection, and revocation decision records with the exact package artifacts, source snapshots, portfolio inventories, dependency paths, and existing static evidence that TraceMap already owns. 88mph (or another trusted producer) decides whether an exact registry artifact is quarantined, admitted, rejected, or revoked. TraceMap inventories package and dependency surfaces and combines evidence across repositories. TraceMap must answer "which exact snapshots, applications, dependency paths, and deployable evidence reference the affected artifact" without becoming a malware scanner, a package authority, or a deployment control.

This spec extends the evidence model of `.kiro/specs/package-dependency-surfaces/` (the `PackageReferenced` / `package-config` contract) and the composition mechanics of `.kiro/specs/package-upgrade-impact/`, `.kiro/specs/multi-index-portfolio-report/`, `.kiro/specs/combined-dependency-paths/`, and `.kiro/specs/reverse-impact-query/`. It does not change those contracts' identity or limitation promises (see design.md "Relationship to existing specs").

## Definitions

- **External decision record**: a versioned, producer-authored record (`package-decision.v1`) that states an admission, rejection, revocation, or non-terminal quarantine state about one exact package artifact. TraceMap reports the supplied state but does not enforce it.
- **Artifact identity**: the tuple that uniquely identifies a package artifact: ecosystem, normalized package name, exact resolved version, registry origin, and artifact digest. A name and version alone are never artifact identity.
- **Evidence ladder**: the closed correlation classification set defined in Requirement 4. Exact and possible matches are distinct rungs and are never collapsed.
- **Producer**: the external authority that authored the decision record (for example the 88mph package governance service). Producer identity is bounded metadata, not a trust claim TraceMap verifies.
- **Snapshot**: one TraceMap scan of one repository at one full commit SHA, represented by `scan-manifest.json` and `index.sqlite`, or a combined index over several such scans.

## Goals

1. Admit deterministic `package-decision.v1` records with strict, closed-set validation and explicit failure classification for malformed, unsupported, duplicate, conflicting, or unverifiable records.
2. Correlate each admitted record against single, combined, and portfolio manifests of TraceMap indexes, read-only, producing an exact-versus-possible evidence ladder that never collapses digest-proven matches with name/version matches.
3. Bind every correlation row to source snapshot identity: source label, scan ID, repo identity (or its hash), full commit SHA, file path, line span, rule ID, evidence tier, and extractor identity, reusing the existing package-evidence fact chain.
4. Report direct versus transitive dependency relationships and optional dependency-path and reverse-impact context only where existing evidence proves them; emit explicit gaps everywhere else.
5. Detect before/after artifact replacement (same name and version, different artifact digest) across two portfolio snapshots, labeled with per-side evidence strength.
6. Render bounded, versioned external advisory claims (framework-implied exposure profiles) as external rule claims with exact producer and version provenance, never as TraceMap-proven facts.
7. Render optional external build/deployment references as runtime-unproven references; never claim a deployed runtime actually loaded a package.
8. Preserve the established privacy, redaction, hashing, and deterministic-serialization guarantees.

## Implementation delivery grouping

The 13 implementation slices in `tasks.md` remain independently testable, independently gated, and independently updateable. They are grouped into roughly five implementation PRs to keep review and merge overhead bounded; grouping is a delivery plan, not permission to combine evidence contracts, skip a slice's validation gate, or defer a required limitation update.

1. **PR 1 — record reader, correlation engine, CLI, and synthetic fixtures:** slices 1–2, including the record contract, single-index correlation, command surface, exit-code behavior, and the initial fixture assertions.
2. **PR 2 — npm exact-artifact evidence and composition consumers:** slices 3–4 and 9, covering combined/portfolio inputs, npm lockfile integrity, and bounded path/reverse context.
3. **PR 3 — NuGet and Swift adapter evidence:** slices 5–6, with each adapter's catalog, limitations, and validation gate kept distinct.
4. **PR 4 — Python and JVM adapter evidence:** slices 7–8, with format-specific digest eligibility and stop conditions preserved.
5. **PR 5 — comparison, advisory/deployment references, and closure:** slices 10–13, covering before/after replacement, external advisory claims, runtime-unproven references, documentation, acceptance, and final gates.

The specification PR is separate from these implementation groupings. No grouping changes rule ownership or permits package downloads, execution, runtime claims, admission enforcement, or other non-goals.

## Non-goals

- No package download, restore, execution, or installation. TraceMap never fetches from a registry.
- No malware, vulnerability, SAST, DAST, SBOM, or exploitability analysis or claims.
- No admission, rejection, or revocation decisions by TraceMap. TraceMap correlates; the producer decides.
- No automatic patching, build blocking, rollback, shutdown, deployment, approval, or remediation commands.
- No claim that a deployed runtime actually loaded a package.
- No signature verification, key management, notarization, or producer authentication. Digests prove integrity, not authority; TraceMap does not infer trust from provenance.
- No registry credentials, raw registry response content, raw malicious package content, private paths, local absolute paths, or customer identity in any output.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No dependency on 88mph implementation internals. The decision record contract is a public, versioned, standalone schema.
- No dependency on issue #689 (general external static-analysis evidence admission). #689 owns scanner-result admission and focused path review; this spec owns package-decision records only. If a shared external-evidence envelope is later extracted, that is a follow-up, not a blocker.
- No mutation of input indexes, no new persisted database tables in v1, and no wall-clock-dependent output.

## Requirements

### Requirement 1: Decision record input contract

1.1. TraceMap SHALL accept a decision record file declared as `"version": "package-decision.v1"` containing a non-empty `records` array. Each record SHALL be self-contained and carry, at minimum: `decisionId`, `decisionKind`, `ecosystem`, `packageName`, `artifactVersion`, `producer.id`, `producer.policyVersion`, and `decisionTimeUtc`.

1.2. `decisionKind` SHALL be a closed vocabulary. V1 accepts `admit`, `reject`, `revoke`, and `quarantine`. `quarantine` is a non-terminal state supplied by the external producer: TraceMap MAY validate, correlate, and report it, but SHALL NOT enforce, block, approve, or treat it as terminal admission or revocation. `admit`, `reject`, and `revoke` retain their respective external semantics; TraceMap reports them without making the decision.

1.3. Each record SHALL carry the artifact identity block when the producer knows it: `registryOrigin` (host-only string), `artifactDigestAlgorithm` (closed set: `sha256`, `sha512-base64`), and `artifactDigest` (value matching the declared algorithm's canonical encoding). A record without a digest SHALL remain admissible and SHALL be correlated at no stronger than the possible rung forever, by construction.

1.4. Each record MAY carry `supersedesDecisionId` (a prior `decisionId` from the same producer) and a provenance relationship: `provenance.sourceRepo` and `provenance.sourceCommitSha` (full 40- or 64-hex). Provenance SHALL be rendered hashed or host-only, never raw.

1.5. Records SHALL NOT contain free-text prose fields, remediation instructions, executable content, or arbitrary nested objects. Unknown properties SHALL be rejected, not ignored.

1.6. TraceMap SHALL compute a canonical digest over each record file using the existing `sha256-canonical-json-v1` algorithm (recursively key-sorted compact JSON, array order significant, digest-relevant fields blanked) and SHALL use that digest as the record's integrity identity in all outputs. A supplied digest field SHALL be verified in constant time; mismatch rejects the record.

### Requirement 2: Record admission and failure handling

2.1. Validation SHALL complete for the whole input before any correlation, and SHALL be envelope-first, record-second, mirroring the Access design-evidence ingestion pattern.

2.2. Every rejected record SHALL produce an input gap row with a closed-set classification from: `DecisionInputSchemaUnsupported`, `DecisionInputMalformed`, `DecisionInputDigestMismatch`, `DecisionInputDecisionKindUnsupported`, `DecisionInputIdentityUnsafe`, `DecisionInputDuplicateConflict`, `DecisionInputLimitReached`, `DecisionInputReadFailed`. No record is silently dropped.

2.3. Two records with the same `(producer.id, decisionId)` and identical computed digests SHALL be deduplicated with a gap noting the duplicate. The same identity with different digests SHALL reject both as `DecisionInputDuplicateConflict`; no partial admission.

2.4. Input caps SHALL be enforced at whole-envelope level (record count, per-field length, digest counts) with `DecisionInputLimitReached` rejecting the whole input, following the existing whole-envelope limit convention.

### Requirement 3: Authenticity boundary

3.1. TraceMap SHALL treat the producer identity, policy version, and decision vocabulary as bounded metadata only. Producer conformance to the schema SHALL NOT be read as authority, trustworthiness, or correctness of the decision.

3.2. Digest verification SHALL detect post-production modification of the record file and of artifact identity; it SHALL NOT authenticate the producer, prove the record was issued by the named authority, or establish non-repudiation. Every report SHALL state this limitation.

3.3. Signature and authority verification belongs upstream (the trusted producer and its distribution channel, for example the 88mph ledger). TraceMap SHALL NOT add signature verification, key discovery, or trust-store handling in this spec's scope. If an owner later wants transport-level verification, that is a separate spec.

### Requirement 4: Correlation identity and evidence ladder

4.1. Correlation SHALL compare each admitted record against package evidence (`PackageReferenced`-family facts projected as `package-config` surfaces) in every source snapshot, using per-ecosystem normalized name matching (design.md "Name and version normalization") and exact-string version equality only. TraceMap SHALL NOT interpret semantic versioning, resolve ranges, or solve dependencies to manufacture a match.

4.2. The correlation classification SHALL be a closed set, in fixed precedence for reporting:

- `ExactArtifactMatch`: ecosystem, normalized name, exact resolved version, and artifact digest all present on both sides and equal (constant-time digest compare). Digest absent on either side can never produce this rung.
- `ArtifactDigestMismatch`: ecosystem, normalized name, and exact version match, both sides carry digests, and the digests differ. This is evidence the reference is probably a different artifact than the decided one, and is never reported as a match or as excluded; it demands review.
- `PossibleNameVersionMatch`: normalized name and exact version string match, but no digest equality exists (digest absent on either side, or registry origins differ without digest equality). This rung carries a `matchBasis` detail of `resolved-version` (lockfile-proven) or `declared-exact` (literal manifest pin) and is never labeled exact.
- `AmbiguousIdentity`: the name matches but the version evidence is a range, hash-only, dynamic, or missing; or name normalization differs across shapes. The reference may or may not be the decided artifact; TraceMap SHALL NOT count it as a match.
- `ExcludedSource`: the source was scanned with full credible coverage, has package-evidence capability for that ecosystem, and contains no matching package evidence. This proves only "not referenced in this static snapshot", never "safe".
- `UnknownAnalysisGap`: the source has reduced coverage, unknown commit SHA, unsupported ecosystem capability, duplicate source identity, or other analysis gap; absence of a match is not evidence of absence.
- `StaleScanReference` (source-level, overlay): the record's `decisionTimeUtc` and the scan manifest's `scannedAt` are both present and the scan precedes the decision. The scan cannot reflect post-decision remediation. `scannedAt` is producer-declared and non-authoritative; outputs SHALL say so.
- `RuntimeUnprovenReference`: an optional external build/deployment reference names the artifact, but TraceMap has not verified any runtime load. See Requirement 9.
- Query-level gap kinds `SelectorNoMatch` and `TruncatedByLimit` follow the existing report conventions.

4.3. Exact and possible matches SHALL NEVER be merged, counted together, or labeled equivalently in any summary, exit-code semantics, Markdown section, or rollup.

4.4. When both sides carry `registryOrigin` and the origins differ, correlation SHALL cap the rung at `PossibleNameVersionMatch` unless digests are present and equal (identical bytes are origin-independent; the origin difference SHALL be noted).

4.5. For ecosystems whose adapters cannot prove exact resolved artifact identity today (see Requirement 10 matrix), correlation SHALL emit explicit capability gaps rather than matching a declared range to a supposedly known-good artifact.

### Requirement 5: Source inventory scope

5.1. The command SHALL accept, read-only: a single `index.sqlite`, a combined index, repeatable `--index`/`--label` pairs, or a portfolio manifest compliant with the existing `multi-index-portfolio-report` manifest schema v1.0, reusing its label uniqueness, path-resolution, and identity-hint rules.

5.2. Every correlation row SHALL carry source snapshot identity: source label, source index ID, scan ID, repo identity or its hash, full commit SHA, and the evidence chain (fact ID, original fact ID, rule ID, evidence tier, extractor ID and version, file path, line span) from the underlying `PackageReferenced` fact.

5.3. Combined indexes SHALL be expanded through `index_sources` preserving container and original source labels, exactly as portfolio reporting does. Duplicate source identity (same scan ID, or same repo identity plus commit SHA) SHALL emit `UnknownAnalysisGap` for the duplicate pairing, following the portfolio duplicate-identity convention.

5.4. Sources with missing or unknown commit SHA SHALL be classified `UnknownAnalysisGap`, never excluded.

### Requirement 6: Direct/transitive relationships and dependency paths

6.1. Direct versus transitive SHALL be reported only from evidence: a package row with `dependencyRelation=direct` (declared in a root manifest) or `dependencyRelation=transitive` (present only in lockfile evidence). Where lockfile-derived evidence does not exist, the relationship SHALL be `unknown` and the report SHALL state that direct/transitive distinction is unavailable for that source, as a gap.

6.2. Optional `--include-paths` and `--include-reverse` context SHALL reuse the existing combined path graph inventory and its bounds (`--max-depth`, `--max-paths`, `--max-frontier`, and the reverse equivalents), attaching bounded paths to the matched `package-config` surfaces with their existing classifications, rule IDs, and tiers.

6.3. Path context SHALL NOT upgrade a correlation rung. A possible match with a strong path is still a possible match.

### Requirement 7: Before/after artifact changes

7.1. The command SHALL accept `--before-manifest` and `--after-manifest` portfolio manifests (paired, mutually exclusive with single-snapshot inputs, matching portfolio comparison semantics) and SHALL report per-source artifact identity changes across the two snapshots.

7.2. When both sides carry artifact digests and the digests differ while name and version are unchanged, the report SHALL emit an `ArtifactReplaced` row pair with both sides' evidence. When either side lacks digest evidence, the change row SHALL be labeled possible-only and SHALL NOT claim artifact replacement.

7.3. Cross-snapshot rows SHALL state they are cross-snapshot evidence, not a single coherent release state, reusing the portfolio comparison wording.

### Requirement 8: External advisory claims (framework-implied exposure)

8.1. An optional `--advisory-profile` input (`advisory-profile.v1`, a separate versioned file) MAY supply bounded, versioned claims that a package version implies an undeclared surface (for example a Next.js/RSC package version implying a server request surface). Claims SHALL carry producer identity, profile version, profile digest, ecosystem, package name, a bounded version predicate from a closed grammar, and a `claimKind` from a closed set (v1: `framework-implied-server-surface`).

8.2. Advisory claims SHALL be rendered as external rule claims with exact producer/version/digest provenance under a dedicated rule, tier-capped at the syntax/textual equivalent, listed in a separate report section, and never merged into TraceMap-proven facts or correlation rungs.

8.3. TraceMap SHALL NOT invent, enrich, or extrapolate advisory claims, SHALL NOT assign severity, and SHALL NOT convert a claim into a vulnerability or exploitability statement.

### Requirement 9: Build and deployment references

9.1. An optional external reference input MAY list build/deployment records that name an artifact identity. Each reference SHALL carry a reference ID, artifact identity block, `referenceKind` from a closed set (v1: `build-attachment`, `deployment-manifest`), producer identity, and optional repo identity and commit SHA (hashed when rendered).

9.2. Every such reference SHALL render as `RuntimeUnprovenReference` with the limitation that TraceMap did not verify the build, the deployment, or any runtime load. No reference SHALL upgrade a correlation rung or be counted as an exact match.

9.3. Runtime observation claims (for example "the host loaded this package") are outside the v1 reference vocabulary and SHALL be rejected as unsupported.

### Requirement 10: Adapter capability matrix and gap policy

10.1. The report SHALL be honest about per-adapter capability. The matrix below was originally audited at `dev` commit `5ca869d9` and has been maintained as adapter slices shipped (npm in PR2, NuGet/Swift in PR3, Python/JVM in PR4); it reflects the shipped capability as of the PR5 closure:

| Capability | .NET/NuGet | npm/TypeScript | JVM/Maven/Gradle | Python/pip | Swift |
| --- | --- | --- | --- | --- | --- |
| Manifests parsed (content) | `.csproj`, `packages.config` | `package.json` | `pom.xml`, literal Gradle | `pyproject.toml`, `setup.cfg`, `requirements*.txt` | `Package.swift`, `Package.resolved` (v1/v2), `Podfile`, `Podfile.lock`, `Cartfile(.resolved)` |
| Declared version ranges | yes | yes | yes | yes | yes (Tier3 manifest) |
| Exact resolved versions | yes (`packages.lock.json`) | yes (`package-lock.json`) | yes (`gradle.lockfile`; Maven has no standard lockfile) | yes (`uv.lock`, `poetry.lock`) | partial (safe literal pins from `Package.resolved`, `Podfile.lock`, Cartfile; revisions/branches stay hashed) |
| Lockfile content parsing | yes (`packages.lock.json` schemas 1–2) | yes (v2/v3) | yes (`gradle.lockfile`) | yes (`uv.lock`, `poetry.lock`; `Pipfile` is an explicit unsupported-metadata gap) | yes (SwiftPM resolved, Podfile.lock, Cartfile.resolved) |
| Lockfile identity (path/hash) | yes | yes | yes (gradle) | yes | partial (`manifestHash` for `Package.swift` only) |
| Registry origin | no | yes (host-only from lockfile `resolved`) | no | partial (host-only for uv registry URLs) | no (URLs hashed or omitted) |
| Artifact/tarball digest or integrity | no (`contentHash` never read as evidence) | yes (registry-declared `integrity`, sha512-base64) | no (`verification-metadata.xml` not consumed; artifact form not identifiable) | no (wheel/sdist hashes are artifact-form specific and never emitted as artifact digests) | no (Podfile.lock SPEC CHECKSUMS are podspec checksums, stored only as labeled `specChecksum`, never artifact digests) |
| Direct/transitive distinction | yes (from lockfile `type`) | yes (root manifest cross-reference) | no | yes where declarations prove it (uv groups/qualifiers, Poetry complete sibling manifests); otherwise explicit gap | no |
| Import/usage linkage to package | no | no | no (framework-tier upgrade only) | partial heuristic (internal tier upgrade; no usage fact) | no |

10.2. Correlation over a capability the adapter lacks SHALL emit an explicit gap naming the missing capability (for example `LockfileDigestUnavailable`, `DirectTransitiveUnavailable`), never silence, and never a guessed match.

10.3. Adapter capability extensions (lockfile parsing, integrity capture, direct/transitive derivation) SHALL land as focused per-ecosystem slices that update the adapter rule's catalog entry, limitations, and `docs/VALIDATION.md` smoke checks; they SHALL NOT bundle into the correlation engine PR.

### Requirement 11: Output artifacts

11.1. The command SHALL emit `package-decision-report.md` (default) and/or `package-decision-report.json` (`--format`), writing both for directory output, following the existing report conventions.

11.2. The JSON document SHALL have `reportType: "package-decision-correlation"`, `version: "1.0"`, camelCase properties, sorted arrays, empty arrays preserved, optional absent objects as `null`, no generated timestamps, and byte-stable output for identical inputs.

11.3. The Markdown report SHALL render, in order: Summary; Decision Records; Exact Matches; Possible Matches; Artifact Identity Mismatches; Ambiguous References; Excluded Sources; Stale and Runtime-Unproven References; Advisory Claims (external); Optional Path and Reverse Context; Before/After Artifact Changes; Gaps; Limitations.

11.4. Every row SHALL carry its rule ID, evidence tier or external-claim label, and provenance fields. The report SHALL include a focused-review inputs rendering: for exact and possible matches, the bounded set of files, line spans, dependency relations, paths, and commit SHAs a human reviewer needs. The report SHALL NOT contain remediation commands or operational instructions.

11.5. Summary counts SHALL separate exact, possible, mismatched, ambiguous, excluded, stale, and runtime-unproven rows; they SHALL never sum exact and possible into one number.

### Requirement 12: CLI, caps, selectors, exit codes, and coverage

12.1. The command verb SHALL be `tracemap package-decision` with `--decision <file>` (required), inputs as in Requirement 5, `--out <path>`, `--format <markdown|json>`, `--source <label>`, `--ecosystem <name>`, `--decision-id <id>`, `--classification <rung>` filter, optional `--advisory-profile`, optional deployment-reference input, optional `--as-of <RFC3339>` for deterministic freshness evaluation, and `--exit-code`.

12.2. Caps SHALL follow the existing `--max-*` convention (`--max-findings`, `--max-gaps`, plus path/reverse caps when context is requested). Truncation SHALL emit `TruncatedByLimit` and SHALL never suppress coverage gaps.

12.3. With `--exit-code`, v1 SHALL return 1 only when at least one `ExactArtifactMatch` row correlates to an external `reject` or `revoke` decision record; otherwise it SHALL return 0. Possible or ambiguous matches, digest mismatches, excluded sources, analysis gaps, stale overlays, runtime-unproven references, and `quarantine` records SHALL remain review evidence and SHALL NOT cause non-zero by default. Validation, parse, file, schema, and connection errors SHALL always return non-zero regardless of `--exit-code`. Any broader policy selector is deferred and SHALL require an explicit future contract; it SHALL NOT be inferred from this option.

12.4. `--as-of`, when supplied, SHALL be the only clock input. Absence of `--as-of` SHALL produce no time-dependent output; staleness then relies only on record-versus-scan fields per Requirement 4.2.

### Requirement 13: Rule ownership

13.1. This work SHALL own three new rules, all registered in `rules/rule-catalog.yml` with documented limitations before any row is emitted: `package.decision.record.v1` (record admission), `package.decision.correlation.v1` (correlation rows and gaps), and `package.decision.advisory.v1` (external advisory claims).

13.2. Existing rules SHALL NOT be overloaded. `package.upgrade.impact.v1` keeps its name/version delta identity contract; portfolio, paths, diff, and reverse rules keep theirs. Adapter rules gain only additive capability properties and limitation updates in their own slices.

13.3. The `package-decision.v1` file format is the neutral shared envelope; `package-impact`, portfolio, and release-review MAY consume decision-derived data later through their own specs, not by extending this rule set silently.

### Requirement 14: Privacy, redaction, and determinism

14.1. Decision record fields SHALL be validated against safe shapes at admission: package names must pass the safe-identifier policy, `registryOrigin` must be host-only (no scheme, credentials, path, or query), digests must match their algorithm encoding, and timestamps must be RFC3339. Violations reject the record (`DecisionInputIdentityUnsafe`), including adversarial values such as credential-bearing URLs, path-shaped names, and `git+ssh://user:pass@host` versions.

14.2. Outputs SHALL reuse the established safe-value policy: repo identities hashed or host-only, absolute paths hashed, no raw registry content, no snippets, no customer identity. Adversarial fixture values MUST NOT appear in any output artifact.

14.3. Identical inputs SHALL produce byte-identical outputs across repeated runs; sort orders SHALL be fixed and documented; no wall-clock, process, or environment values SHALL enter output.

### Requirement 15: Synthetic fixture matrix

15.1. The spec's fixtures SHALL cover, at minimum: admitted artifact A with digest-bound evidence; a later-revoked artifact B; a non-terminal `quarantine` record whose report does not trigger `--exit-code`; same name and version with a different artifact digest; a direct dependency; a transitive (lockfile-only) dependency; a semver-only, missing-lockfile case that stays ambiguous; missing or unsupported integrity; a non-registry/git dependency; a stale scan; a multi-repository portfolio; a before/after artifact replacement; malformed and untrusted decision records; one framework-implied advisory claim; privacy/redaction adversarial values; and a determinism check proving repeated runs are byte-identical.

15.2. Fixtures SHALL live under `samples/package-decisions/` with committed expected-output assertions in tests, and SHALL NOT contain real registry credentials, private paths, or real customer data.

## Limitations must include

- Correlation is static evidence over snapshots. It does not prove runtime loading, restore success, installed versions, deployment reachability, exploitability, or business impact.
- Exact matches prove the decided artifact identity appears in snapshot evidence; they do not prove the package executed, caused harm, or that remediation is required.
- Possible matches cannot distinguish whether the reference is the decided artifact; they bound review, they do not conclude.
- Excluded sources prove absence only within the scanned snapshot's stated coverage; reduced coverage converts exclusion into a gap.
- Record digests prove record and artifact-identity integrity; they do not authenticate the producer or confer authority. Provenance is lineage, not trust.
- `scannedAt` is producer-declared and non-authoritative; staleness flags are advisory context.
- Advisory claims are external opinions with producer provenance; TraceMap neither validates nor endorses them.
- Adapter capability varies by ecosystem; the matrix in Requirement 10 is part of the contract, and missing capability is reported as gaps.

## Smallest proof (from issue #690, restated)

1. Synthetic package with two exact tarball identities: admitted A, later revoked B.
2. A referenced directly in one fixture, transitively in another.
3. One semver-only/missing-lockfile fixture that stays ambiguous.
4. Digest-bound external decision records admitted and verified.
5. Deterministic portfolio and dependency-path correlation.
6. Changing only the tarball/artifact identity changes the rung (exact becomes digest mismatch; it never collapses to name/version).
7. One framework-implied surface rendered as an explicitly external, reduced-evidence claim.
8. Protected source, registry credentials, raw malicious content, and customer identities stay out of public output.
