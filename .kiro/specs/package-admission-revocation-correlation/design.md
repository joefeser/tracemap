# Package Admission and Revocation Correlation Design

Spec: `.kiro/specs/package-admission-revocation-correlation/`. Issue: #690. Base research commit: `dev` at `5ca869d9f7e8e73191985423b8453633eaac426c`.

This is a design for future implementation. Nothing here is implemented; `tasks.md` tracks the slices.

## 1. Overview

A new read-only CLI command, `tracemap package-decision`, admits a `package-decision.v1` record file from an external governance producer (88mph today), correlates each record's artifact identity against package evidence in one or many TraceMap indexes, and emits a deterministic, bounded impact report with a strict evidence ladder. The command composes in memory over read-only SQLite connections, exactly like portfolio reporting; it persists nothing and mutates nothing.

The design reuses, without modification:

- The `PackageReferenced` fact family and `package-config` surface projection (`.kiro/specs/package-dependency-surfaces/`).
- Portfolio manifest schema v1.0, source expansion, duplicate-identity handling, and safe-rendering helpers (`.kiro/specs/multi-index-portfolio-report/`, `src/dotnet/TraceMap.Reporting/PortfolioReport.cs`).
- The combined path graph inventory and reverse traversal with their bounds (`.kiro/specs/combined-dependency-paths/`, `.kiro/specs/reverse-impact-query/`, `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs`, `CombinedReverseQuery.cs`).
- Canonical digest `sha256-canonical-json-v1` and constant-time comparison (`.kiro/specs/sql-validation-summary-ingestion/`, `src/dotnet/TraceMap.Reporting/SqlValidationSummary.cs`).
- Envelope-first validation and closed-set input failure codes (`.kiro/specs/access-source-neutral-design-evidence-ingestion/`, `src/dotnet/TraceMap.Access/AccessDesignEvidenceReader.cs`).
- Safe-value, hashing, and stable-JSON helpers (`LegacyDataSafeValues`, `FactFactory.Hash`, `CombinedReportHelpers`, `JsonOptions.Stable`).

## 2. Decision record contract (`package-decision.v1`)

### 2.1 File shape

```json
{
  "version": "package-decision.v1",
  "records": [
    {
      "decisionId": "dec-1a2b3c4d5e6f78901234",
      "decisionKind": "revoke",
      "ecosystem": "npm",
      "packageName": "@example/lib",
      "artifactVersion": "2.14.0",
      "registryOrigin": "registry.npmjs.org",
      "artifactDigestAlgorithm": "sha512-base64",
      "artifactDigest": "base64-value",
      "producer": {
        "id": "88mph-package-governance",
        "policyVersion": "2026-08"
      },
      "decisionTimeUtc": "2026-08-18T00:00:00Z",
      "supersedesDecisionId": "dec-09f8e7d6c5b4a3021011",
      "provenance": {
        "sourceRepo": "https://example.invalid/example/lib.git",
        "sourceCommitSha": "0123456789abcdef0123456789abcdef01234567"
      }
    }
  ]
}
```

Field rules:

- `version`: required, exactly `package-decision.v1`. Anything else: `DecisionInputSchemaUnsupported`, whole input rejected.
- `records`: required, non-empty array, each element an object. Unknown top-level or record properties: `DecisionInputMalformed` for that record. No free-text fields exist in the schema; there is no `notes`, `description`, or `message` field, so there is nothing to sanitize or trust.
- `decisionId`: required, `^[a-z0-9][a-z0-9._:-]{3,80}$`. Producer-scoped; uniqueness is enforced per `(producer.id, decisionId)`.
- `decisionKind`: required, closed set `admit | reject | revoke | quarantine` (v1). `quarantine` is a non-terminal externally supplied state. TraceMap validates, correlates, and reports it, but never enforces, blocks, approves, or treats it as terminal admission or revocation. The other three values retain their external semantics; TraceMap does not make the decision.
- `ecosystem`: required, closed to the documented adapter values (`nuget`, `npm`, `python`, `maven`, `gradle`, `swift`). Unknown ecosystem: `DecisionInputMalformed` (the record cannot correlate).
- `packageName`: required, must pass the safe-identifier policy (`LegacyDataSafeValues.IsSafeIdentifier` shape plus npm scope support `@scope/name`). Path-shaped names, URL-bearing names, credential fragments: `DecisionInputIdentityUnsafe`.
- `artifactVersion`: required, exact resolved version string, must pass the existing safe-version shape; ranges (`^`, `~`, `>=`), URLs, `git+...`, `${...}`: `DecisionInputMalformed`. A decision is always about one exact artifact version.
- `registryOrigin`: optional, host-only (`^[A-Za-z0-9.-]+(:[0-9]+)?$`). Scheme, credentials, path, query: `DecisionInputIdentityUnsafe`. `unknown` allowed as an explicit value.
- `artifactDigestAlgorithm`: optional, closed set `sha256` (lowercase hex, 64 chars) | `sha512-base64` (npm integrity base64 body, no `sha512-` prefix). The pair must both be present or both absent. An unsupported algorithm string: `DecisionInputMalformed`.
- `producer`: required object with `id` (`^[a-z0-9][a-z0-9._-]{1,64}$`) and `policyVersion` (`^[A-Za-z0-9][A-Za-z0-9._+-]{0,31}$`).
- `decisionTimeUtc`: required, RFC3339 UTC. Used only for staleness context and `--as-of` evaluation; never compared to wall clock.
- `supersedesDecisionId`: optional, same shape as `decisionId`. TraceMap records the chaining but does not resolve supersession semantics in v1 (the report shows both records side by side; see §14).
- `provenance`: optional object; `sourceRepo` is hashed on render (host-only plus hash, existing `SafeRepoName`-style treatment), `sourceCommitSha` must be full 40- or 64-hex. Provenance is lineage metadata; it never affects rungs (Requirement 3, "digests prove integrity, not authority").

### 2.2 Record integrity identity

TraceMap computes, for each admitted record, `recordDigest = sha256-canonical-json-v1(record)` using the existing algorithm from `SqlValidationSummary.Canonicalize`: recursively key-sorted compact JSON, UTF-8, lowercase hex, array order significant. The `(producer.id, decisionId, recordDigest)` triple is the record's identity in outputs. If the file also carries a `recordDigest` field (optional self-attestation), TraceMap recomputes and compares in constant time (`CryptographicOperations.FixedTimeEquals`); mismatch: `DecisionInputDigestMismatch`. This detects tampering; it does not authenticate anyone.

### 2.3 Admission pipeline

1. Read file; UTF-8 decode failure or JSON parse failure: whole input `DecisionInputReadFailed`.
2. Envelope checks: `version` exact, `records` non-empty array, count ≤ 200 records (whole-input `DecisionInputLimitReached`).
3. Per-record validation in file order; each failure yields an input gap row carrying: gap id (stable hash of classification + producer + decisionId + discriminator), classification, rule ID `package.decision.record.v1`, and bounded metadata (echoed safe fields only).
4. Duplicate resolution: identical `(producer.id, decisionId)` with identical digests deduplicates (gap `DecisionInputDuplicateConflict` with reason `duplicate-identical` on the second and later copies); differing digests rejects every copy of that identity (reason `duplicate-conflict`).
5. Only fully admitted records proceed to correlation. Rejected records still render in the Decision Records section with their rejection classification; they are never silently dropped.

### 2.4 Input failure classifications (closed set)

`DecisionInputSchemaUnsupported`, `DecisionInputMalformed`, `DecisionInputDigestMismatch`, `DecisionInputDecisionKindUnsupported`, `DecisionInputIdentityUnsafe`, `DecisionInputDuplicateConflict`, `DecisionInputLimitReached`, `DecisionInputReadFailed`.

`DecisionInputDecisionKindUnsupported` remains distinct from `DecisionInputMalformed` so unsupported future vocabulary is a clean, countable compatibility signal rather than generic garbage. V1's accepted `quarantine` value is non-terminal and does not change correlation rungs or grant TraceMap admission/revocation authority.

## 3. Advisory profile contract (`advisory-profile.v1`, optional input)

```json
{
  "version": "advisory-profile.v1",
  "producer": { "id": "example-advisory-producer", "version": "2026.08.1" },
  "claims": [
    {
      "claimId": "claim-next-rsc-001",
      "ecosystem": "npm",
      "packageName": "next",
      "versionPredicate": { "kind": "exact", "version": "14.2.3" },
      "claimKind": "framework-implied-server-surface",
      "claimParams": { "framework": "next-rsc" }
    }
  ]
}
```

- `versionPredicate.kind`: closed grammar v1: `exact` (exact string) | `any` (all versions). No range grammar in v1 — ranges would require semver interpretation, which TraceMap does not do. Multiple `exact` claims express coverage.
- `claimKind`: closed set v1: `framework-implied-server-surface`. Any other value: the whole profile is rejected with the record-style input gap classifications (same closed set as §2.4).
- `claimParams`: closed per `claimKind`; v1 allows only `framework` (`^[a-z0-9][a-z0-9._-]{1,40}$`).
- No severity, no CVE references, no prose. TraceMap renders claims verbatim-as-structured with producer provenance and profile digest, in a dedicated section, under `package.decision.advisory.v1`, and never merges them into correlation rungs or facts.

## 4. Deployment reference contract (`package-deployment-reference.v1`, optional input)

```json
{
  "version": "package-deployment-reference.v1",
  "producer": { "id": "example-ci-producer", "version": "1" },
  "references": [
    {
      "referenceId": "ref-build-0042",
      "referenceKind": "build-attachment",
      "ecosystem": "npm",
      "packageName": "@example/lib",
      "artifactVersion": "2.14.0",
      "registryOrigin": "registry.npmjs.org",
      "artifactDigestAlgorithm": "sha512-base64",
      "artifactDigest": "base64-value",
      "sourceRepo": "https://example.invalid/org/app.git",
      "commitSha": "0123456789abcdef0123456789abcdef01234567"
    }
  ]
}
```

- `referenceKind`: closed set v1: `build-attachment` | `deployment-manifest`. Runtime-load claims are rejected as `DecisionInputMalformed` (Requirement 9.3): TraceMap never renders "a runtime loaded this package" as anything it cannot prove, and it cannot prove runtime loads.
- All references render as `RuntimeUnprovenReference` rows with the fixed limitation "TraceMap did not verify this build, deployment, or any runtime load." They join correlation by artifact identity (exact digest join when both sides carry digests, else name+version join labeled possible), but they never count as exact matches and never upgrade rungs.
- `sourceRepo` hashed on render; `commitSha` may optionally join to a portfolio source's commit SHA to attach the reference to a known snapshot, rendered as provenance only.

## 5. TraceMap-side artifact identity

### 5.1 Additive `PackageReferenced` properties

Adapter slices add optional properties to `PackageReferenced`-family facts. `properties_json` is a free-form string map, so these are additive with no schema migration:

| Property | Meaning | Source |
| --- | --- | --- |
| `resolvedVersion` | Exact resolved version proven by a checked-in lockfile | Lockfile entry |
| `lockfilePath` | Repository-relative lockfile path | Lockfile |
| `lockfileHash` | `sha256(lockfile bytes)` prefix (32 hex) — lockfile identity | Lockfile file |
| `registryOrigin` | Host-only registry host from lockfile resolution URL | Lockfile `resolved` URL, host-only, hashed if not host-shaped |
| `artifactDigestAlgorithm` | `sha256` or `sha512-base64` | Lockfile integrity field |
| `artifactDigest` | Digest value in the algorithm's canonical encoding | Lockfile integrity field |
| `dependencyRelation` | `direct` or `transitive` | Direct = declared in a root manifest; transitive = lockfile-only |
| `dependencyPathDepth` | Lockfile nesting depth where the format records it | Lockfile structure |

Each adapter slice updates its rule's catalog entry (`typescript.package.v1`, `project.file.v1`, `jvm.buildfile.v1`, `python.package.metadata.v1`, `swift.*`) with the new emitted properties and limitations, and updates `docs/VALIDATION.md`. The correlation engine consumes these properties defensively: absent properties degrade the rung, never the validity of the row.

Lockfiles are only read, never solved: no package-manager execution, no restore, no network. A lockfile format that cannot yield a deterministic artifact digest without execution yields `resolvedVersion` plus a `LockfileDigestUnavailable` gap (Requirement 10.2).

### 5.2 Digest caveats per format (documented, enforced)

- npm `package-lock.json` integrity (`sha512-...`) is the registry tarball integrity: eligible for exact matches. npm lockfiles are the first exact-digest-capable adapter implementation.
- NuGet `packages.lock.json` has resolved versions but no content hash: `resolvedVersion` yes, digest gap always.
- Gradle `gradle.lockfile` has resolved versions without hashes; `gradle/verification-metadata.xml` carries artifact SHA-256s and is the JVM digest source when checked in (slice 8 evaluates; stop condition if parsing proves non-deterministic).
- Python `uv.lock`/`poetry.lock` carry source hashes in current formats (slice 7 evaluates the exact field semantics; a hash over source distribution is only eligible for exact match if the decision record uses the same artifact form — recorded as a limitation, otherwise rendered with `matchBasis` possible).
- Swift `Package.resolved` has no digests; `Podfile.lock` SPEC CHECKSUMS are podspec checksums, not artifact digests, and MUST be stored as `specChecksum` only, never as `artifactDigest`.

Adapter delivery order is npm lockfiles first, then NuGet, followed by Swift, Python, and JVM slices. This is an implementation ordering decision, not a claim that an adapter can prove exact identity before its slice ships; until each slice lands, its audited capabilities and explicit gaps in Requirement 10 remain authoritative.

## 6. Correlation algorithm

### 6.1 Name and version normalization

Per-ecosystem `normalizedName` derivation, applied to both record and evidence sides; outputs render the original safe name plus the normalizer tag:

- `nuget`: case-insensitive (NuGet IDs); compare `OrdinalIgnoreCase`.
- `npm`: lowercase, scopes preserved verbatim.
- `python`: PEP 503 (lowercase, `-`/`_`/`.` folded).
- `maven`/`gradle`: `groupId:artifactId`, case-sensitive.
- `swift`: identity string, case-sensitive.

Version equality is exact string equality after trimming insignificant whitespace. No semver parsing, no range resolution, no coercion. This is the existing house rule ("TraceMap does not interpret semantic versioning") applied strictly.

### 6.2 Per (record × source) evaluation

For each admitted record and each source snapshot, gather all package-evidence rows whose ecosystem matches and whose normalized name matches. Then, in fixed order:

1. **Digest join**: if the record carries a digest and any row carries `artifactDigest` with a compatible algorithm: constant-time compare. Equal digest and exact `resolvedVersion` equality → `ExactArtifactMatch` (one row per supporting fact; the pairing rolls up exact). Equal digest but version strings differ → treat as evidence conflict, `AmbiguousIdentity` with caveat `digest-version-conflict`. Different digest with equal name+version → `ArtifactDigestMismatch`.
2. **Name+version join**: no digest equality available. If any row's `resolvedVersion` equals the record version → `PossibleNameVersionMatch`, `matchBasis=resolved-version`. Else if any row's `version` is a literal exact pin equal to the record version → `PossibleNameVersionMatch`, `matchBasis=declared-exact`. Origin mismatch (both present, different) caps here with caveat `registry-origin-mismatch`.
3. **Name-only join**: name matches but versions are ranges, hash-only, dynamic, or absent → `AmbiguousIdentity` with the evidence's version shape as caveat (`range-declared`, `version-hash-only`, `version-unknown`, `dynamic-declaration`).
4. **No name match**: if the source has full credible coverage (succeeded build, no reduced analysis, known commit SHA, no duplicate identity, and the adapter has package capability for the record's ecosystem) → `ExcludedSource`. Any coverage weakness → `UnknownAnalysisGap` with the coverage reason.
5. **Staleness overlay**: if `decisionTimeUtc` and the scan manifest `scannedAt` are both present and `scannedAt < decisionTimeUtc`, the pairing gains `StaleScanReference` context (row `snapshotPredatesDecision: true`, summary listed in the stale section). With `--as-of <RFC3339>` supplied, record effectiveness is additionally evaluated against `--as-of`: a record is `not-yet-effective` when `decisionTimeUtc > asOf`, rendered as a caveat on the record row, never as a rung change. Staleness never changes the rung: a stale exact match still proves the artifact was in that snapshot; it only warns that later remediation is not represented.

The rung is per (record × source) for summary rollups, and per supporting fact for evidence rows; evidence rows never outrank their pairing rung.

### 6.3 Determinism

- All arrays sorted: classification precedence (§6.4), then ecosystem, normalizedName, artifactVersion ordinal, producer id, decisionId, source label ordinal, then stable evidence fields (file path, start line, fact ID). `StringComparer.Ordinal` throughout.
- Pairing and row IDs: `pd:{sha256(recordKey + sourceKey)[20]}` and `pdr:{sha256(recordKey + sourceKey + factKey)[20]}`, echoing the portfolio namespaced-hash pattern; never SQLite row ids, timestamps, or enumeration order.
- Repeated runs with identical inputs and flags produce byte-identical JSON and Markdown.

### 6.4 Reporting precedence (fixed)

`ExactArtifactMatch`, `ArtifactDigestMismatch`, `PossibleNameVersionMatch`, `AmbiguousIdentity`, `StaleScanReference`, `RuntimeUnprovenReference`, `ExcludedSource`, `UnknownAnalysisGap`. (Excluded/Unknown are bulk sections; the order keeps the report review-first.)

### 6.5 Direct/transitive and paths

- `dependencyRelation` passes through from evidence rows; sources without lockfile-derived evidence get a per-pairing gap `DirectTransitiveUnavailable`.
- `--include-paths` runs the existing graph inventory over combined sources and attaches bounded forward paths from endpoint roots to each matched `package-config` surface; `--include-reverse` attaches reverse paths from the surface toward endpoints/symbols/sources. Both reuse existing caps, classifications (`StrongStaticPath` etc.), rule IDs, and `TruncatedByLimit` behavior. Path context renders in its own section and never upgrades rungs (Requirement 6.3).

### 6.6 Before/after mode

With `--before-manifest`/`--after-manifest` (portfolio manifest v1.0 both sides, label-paired per portfolio comparison rules including `IdentityAmbiguous` downgrades):

- For each record and label pair, evaluate both sides per §6.2.
- Both sides digest-bound and digests differ with same name+version → `ArtifactReplaced` row pair (exact-label on both sides).
- Either side digest-absent and version evidence changed → `possible-only` change row; no replacement claim.
- All rows carry the fixed cross-snapshot wording ("cross-snapshot portfolio evidence, not a single coherent release state").

## 7. Rule ownership decision

Decision: **three new rules plus a neutral shared envelope; no overloading of existing rules.**

| Rule | Owns | Emits | Tier |
| --- | --- | --- | --- |
| `package.decision.record.v1` | Decision-record admission, integrity identity, input gaps | `PackageDecisionRecord`, `PackageDecisionInputGap` | External claim metadata; gaps `Tier4Unknown` |
| `package.decision.correlation.v1` | Correlation rows, rungs, pairing gaps, before/after change rows | `PackageDecisionCorrelation`, `PackageDecisionGap`, `PackageDecisionArtifactChange` | Inherits weakest supporting evidence tier |
| `package.decision.advisory.v1` | External advisory claims | `PackageDecisionAdvisoryClaim` | External claim; capped at syntax/textual equivalent |

Why not extend existing rules:

- `package.upgrade.impact.v1` matches a name/version delta against declaration evidence; its limitations say TraceMap does not interpret versions and its identity contract has no digests. Exact-artifact correlation with a digest ladder and authenticity boundary is a different contract; overloading would falsify its documented limitations.
- `portfolio.*`, `combined.*` rules are composition mechanics; the decision semantics would be buried in their limitations.
- Adapter rules keep their declared-manifest identity; they gain additive capability properties and limitation updates in their own slices only.

The neutral shared envelope is the `package-decision.v1` format itself. Later specs (release-review composition, `package-impact` deltas derived from decisions) consume it through their own rules without extending this set. Planned catalog entries (added in the first implementation PR with `status: active` and the limitations from requirements.md "Limitations must include"; until then, if pre-registered, `status: deferred` per the Swift convention):

```yaml
- id: package.decision.record.v1
  name: External package decision record admission
  description: Validates versioned package-decision.v1 admission, rejection, revocation, and non-terminal quarantine records from an external governance producer, computes canonical record digests, and classifies rejected input without trusting producer claims.
  evidenceTier: Tier4Unknown
  emits:
    - PackageDecisionRecord
    - PackageDecisionInputGap
  limitations: [... at minimum the authenticity-boundary and vocabulary limitations from requirements.md ...]

- id: package.decision.correlation.v1
  name: Package decision exact-artifact correlation
  description: Correlates admitted package decision records with PackageReferenced/package-config evidence across single, combined, and portfolio snapshots using a fixed exact-versus-possible evidence ladder.
  evidenceTier: Inherits weakest supporting evidence tier
  emits:
    - PackageDecisionCorrelation
    - PackageDecisionGap
    - PackageDecisionArtifactChange
  limitations: [... rung, coverage, and staleness limitations from requirements.md ...]

- id: package.decision.advisory.v1
  name: External framework-implied exposure advisory claims
  description: Renders bounded, versioned external advisory claims (for example framework-implied server surfaces) with producer provenance; never converts them into TraceMap-proven facts or vulnerability claims.
  evidenceTier: Tier3SyntaxOrTextual
  emits:
    - PackageDecisionAdvisoryClaim
  limitations: [... external-opinion limitations from requirements.md ...]
```

## 8. CLI design

```text
tracemap package-decision
  --decision <package-decision.json>          (required)
  (--index <path> [--label <label>])...       \ one of these three
  --manifest <portfolio.json>                  > input modes,
  (--before-manifest <p.json> --after-manifest <p.json>) / mutually exclusive
  [--advisory-profile <advisory-profile.json>]
  [--deployment-references <package-deployment-reference.json>]
  [--as-of <RFC3339 UTC>]
  [--out <path>] [--format <markdown|json>]
  [--source <label>] [--ecosystem <name>] [--decision-id <id>]
  [--classification <rung>]
  [--include-paths] [--include-reverse]
  [--max-findings <n>] [--max-gaps <n>]
  [--max-depth] [--max-paths] [--max-frontier] [--max-roots] [--max-paths-per-root]
  [--exit-code]
```

Defaults follow house conventions: `--max-findings 200`, `--max-gaps 1000`, path/reverse defaults from the existing commands, Markdown default with both files for directory output, SQLite opened `Mode=ReadOnly`, `--out` must not alias any input.

Exit codes: 0 normally; with `--exit-code`, 1 only when an `ExactArtifactMatch` correlates to an external `reject` or `revoke` record. Possible/ambiguous matches, digest mismatches, stale or runtime-unproven references, gaps, and non-terminal `quarantine` records remain review evidence and do not cause non-zero by default. Validation/parse/file/schema/connection errors return 1 regardless of the option. Any broader future policy selector is deferred to an explicit contract.

## 9. Output contracts

### 9.1 JSON top level

```text
reportType: "package-decision-correlation"
version: "1.0"
mode: "DecisionSnapshotV1" | "DecisionComparisonV1"
query:            (echoed selectors and caps)
decisionRecords:  [PackageDecisionRecord]  (admitted + rejected with classification)
advisoryClaims:   [PackageDecisionAdvisoryClaim] | null
sources:          [PackageDecisionSource]  (label, container/original labels, language,
                    repoName/repoIdentityHash, commitSha, scanId, scannerVersion,
                    analysisLevel, buildStatus, coverageStatus, capabilityTiers)
exactMatches:     [PackageDecisionCorrelation]
digestMismatches: [PackageDecisionCorrelation]
possibleMatches:  [PackageDecisionCorrelation]
ambiguousReferences: [PackageDecisionCorrelation]
excludedSources:  [PackageDecisionExclusion]
staleReferences:  [PackageDecisionStaleReference]
runtimeUnprovenReferences: [PackageDecisionExternalReference]
artifactChanges:  [PackageDecisionArtifactChange] | null (comparison mode only)
pathContext:      {...} | null  (status: available | not_requested | unavailable | truncated)
reverseContext:   {...} | null
gaps:             [PackageDecisionGap]
summary:          {per-rung counts kept separate; truncated flags; coverage}
limitations:      [string]
```

### 9.2 Correlation row (exact/possible/ambiguous/mismatch)

```text
rowId, classification, matchBasis (resolved-version | declared-exact | null),
decisionId, decisionKind, ecosystem, packageName (safe), artifactVersion,
registryOriginJoin (exact | origin-mismatch | absent | unknown),
sourceLabel, sourceIndexId, scanId, repoIdentityHash, commitSha,
dependencyRelation (direct | transitive | unknown),
evidence: {factId, originalFactId, factType, ruleId, evidenceTier,
           extractorId, extractorVersion, filePath, startLine, endLine,
           resolvedVersion?, lockfilePath?, lockfileHash?,
           artifactDigestAlgorithm?, artifactDigest?, version?, versionHash?},
snapshotPredatesDecision: bool, notes: [{code, message}]
```

### 9.3 Markdown sections (fixed order)

Summary; Decision Records; Exact Matches; Possible Matches; Artifact Identity Mismatches; Ambiguous References; Excluded Sources; Stale and Runtime-Unproven References; Advisory Claims (External); Optional Path and Reverse Context; Before/After Artifact Changes; Gaps; Limitations.

Focused-review rendering (Requirement 11.4) is a table under Exact and Possible Matches listing: package, version, digest (when exact), source, commit SHA, relation, file:line span, and the dependency-path link when `--include-paths` produced one. No remediation text, no commands.

## 10. Storage and consumers

- v1 persists nothing: read-only single/combined/portfolio composition in memory (portfolio pattern). Derived persistence (`--write-derived`) is a deferred follow-up per house convention.
- Combined index reuse: expand `index_sources` → sources; `combined_facts` filtered to `PackageReferenced`/`package-config` projection via the existing `CombinedDependencyReporter.BuildSurfaces` path that `package-impact` already uses, preserving source labels, scan IDs, commit SHAs, rule IDs, tiers, and spans.
- Single index reuse: the same first-class single-index reader portfolio and package-impact use (`default` label).
- `package-impact` remains untouched. A later spec may compose decision-derived deltas into `release-review`; that composition owns its own rules.
- Portfolio reporting remains untouched; `tracemap portfolio` does not gain decision sections in this spec.

## 11. Adapter capability matrix (audited at `5ca869d9`)

| Capability | .NET/NuGet | npm/TypeScript | JVM/Maven/Gradle | Python/pip | Swift |
| --- | --- | --- | --- | --- | --- |
| Manifests parsed (content) | `.csproj`, `packages.config` | `package.json` | `pom.xml`, literal Gradle | `pyproject.toml`, `setup.cfg`, `requirements*.txt` | `Package.swift`, `Package.resolved` (v1/v2), `Podfile`, `Podfile.lock`, `Cartfile(.resolved)` |
| Lockfile content parsing | no (`packages.lock.json` presence only) | no | no | no (`Pipfile` inventoried, ignored; `poetry.lock`/`uv.lock` unknown) | yes (three managers) |
| Declared version ranges | yes (Tier2) | yes (Tier2) | yes (Tier2; dynamic → gap + hash) | yes (Tier2; markers/extras dropped) | yes (Tier3 manifest) |
| Exact resolved versions | no | no | no | no | partial: Carthage semver literal; SwiftPM resolved reduced to status flags |
| Lockfile identity (path+hash) | no | no | no | no | partial: `manifestHash` for `Package.swift` only |
| Registry origin | no | no | no | no | no (URLs hashed/omitted) |
| Artifact digest/integrity | no | no (npm integrity unparsed) | no (`verification-metadata.xml` unread) | no | no (SPEC CHECKSUMS not artifact digests) |
| Direct/transitive | no | no | no | no | no |
| Import/usage linkage | no | no | framework-tier upgrade only | internal heuristic tier upgrade, no usage fact | no |
| Name normalization | raw case | raw case (scopes preserved) | raw case | lowercased | safe-label charset or hash |

Implication for v1 of the correlation command: before adapter slices land, no source can produce `ExactArtifactMatch`; every digest-capable record honestly yields `PossibleNameVersionMatch` (literal pins) or `AmbiguousIdentity` (ranges) with explicit `LockfileDigestUnavailable` capability gaps. That is the required behavior, not a deficiency to paper over (issue #690: emit an explicit gap rather than matching a range to a supposedly known-good tarball).

## 12. Fixture matrix (`samples/package-decisions/`)

| # | Fixture | Proves |
| --- | --- | --- |
| F1 | `records/admit-a.json` + digest-bound lockfile evidence for artifact A | `ExactArtifactMatch`; direct relation |
| F2 | `records/revoke-b.json` (supersedes admit of A's neighbor B) + lockfile evidence | later-revoked artifact correlation; supersession rendering |
| F3 | same name+version, different digest (evidence vs record) | `ArtifactDigestMismatch`; rung does not collapse to name/version |
| F4 | transitive-only lockfile entry | `dependencyRelation=transitive`; path context attaches |
| F5 | semver-only manifest, no lockfile | stays `AmbiguousIdentity` with `range-declared` |
| F6 | lockfile row without integrity field | `resolved-version` possible match + `LockfileDigestUnavailable` gap |
| F7 | git/path dependency (hashed version) | `AmbiguousIdentity` (`dynamic-declaration`); no raw URL in output |
| F8 | stale scan (`scannedAt` < `decisionTimeUtc`) | `StaleScanReference` overlay; rung unchanged |
| F9 | two-repo portfolio manifest | per-source rollups; excluded vs gap per coverage |
| F10 | before/after manifests, digest changes, name+version fixed | `ArtifactReplaced` exact-label row pair |
| F11 | malformed record set: bad version, bad digest shape, unknown kind, duplicate conflict, tampered self-digest, unsafe origin | closed-set input gaps; whole-input or per-record rejection per §2.3 |
| F12 | advisory profile with one `framework-implied-server-surface` claim | external claim section; never merged into facts/rungs |
| F13 | adversarial values: credential URLs, path-shaped names, `git+ssh://user:pass@host` | rejected (`DecisionInputIdentityUnsafe`); values absent from all outputs |
| F14 | repeat run of the full matrix | byte-identical JSON/Markdown (determinism test) |
| F15 | deployment-reference file with `build-attachment` | `RuntimeUnprovenReference`; never exact; fixed limitation |

Fixtures use `example.invalid` hosts, synthetic digests, and synthetic SHAs; no real credentials, private paths, or real registry responses (checked by `./scripts/check-private-paths.sh` and negative greps in tests).

## 13. Privacy, safety, determinism

- All record/profile/reference fields are validated to safe shapes at admission; unsafe values are rejected, not sanitized-then-rendered. There is no free-text field anywhere in the three input contracts.
- Rendering reuses: `SafeRepoName`/host-only + hash for repos, `SafeCommit` full-hex check, `SafePath` relative-or-hash, `versionHash` for unsafe evidence versions, `Cell` Markdown escaping, `SortedMetadata` ordering.
- No wall clock: `--as-of` is the only time input when supplied; `decisionTimeUtc`/`scannedAt` are input data.
- Outputs byte-stable; ordering per §6.3; IDs are namespaced hashes; no timestamps, process IDs, or enumeration order.

## 14. Remaining owner decisions

The four decisions requested for this specification patch are resolved in §§2, 5, 8, and 17. These independent follow-ups remain open:

1. **Gradle `verification-metadata.xml`**: adopt only if parsing is deterministic and offline; stop condition in slice 8.
2. **Python lockfile scope** (`uv.lock` vs `poetry.lock` vs both) and whether source-distribution hashes are eligible for exact matching or only `matchBasis` context. Decide at slice 7 with format evidence.
3. **Supersession semantics**: v1 renders chain context only; computing "currently effective decision" per artifact is a producer-side concern unless owners want TraceMap to resolve chains (adds `effective-decision` rollups).
4. **Deployment-reference input timing**: contract is designed here (§4) but may be deferred to a late slice or dropped if owners want a separate spec.
5. **Command naming**: `package-decision` chosen to parallel `package-impact`; alternative `decision-impact` rejected to keep package-tooling verbs together. Confirm at slice 1.

## 15. Relationship to existing specs and issues

- `package-dependency-surfaces`: unchanged contract; additive optional properties only (§5.1). Its non-goal "no registry lookups, signing verification, SBOM" is preserved — TraceMap still does none of those; it ingests producer-authored records and never fetches.
- `package-upgrade-impact`: untouched. Different identity contract (delta name/version vs artifact identity); no rule overloading (§7).
- `multi-index-portfolio-report`: manifest format and source-expansion semantics reused read-only; no portfolio schema changes.
- `combined-dependency-paths` / `reverse-impact-query` / `combined-dependency-diff`: graph inventory and reverse traversal reused read-only with existing bounds; no schema changes; safe-metadata allowlists may gain the new lockfile property names in their own slices only if those reports render them.
- Issue #689 (external scanner evidence): explicitly not a dependency. #689 owns scanner-result admission and focused path review; this spec's envelope discipline mirrors it, and a shared envelope extraction is a possible later refactor, not a blocker.
- 88mph issues #575/#576/#577 own admission/quarantine, inspection, and mirror/revocation response. TraceMap consumes their public record format only; no 88mph internals, APIs, or code are referenced.

## 16. Test strategy

- Unit: record reader validation matrix (every §2.4 classification), duplicate/conflict policy, canonical digest determinism, name/version normalization table.
- Correlation engine: synthetic fact sets covering every rung, origin-mismatch capping, staleness overlay, coverage-driven exclusion vs gap.
- Composition: single index, combined index, portfolio manifest, comparison mode; duplicate identity; unknown commit SHA.
- CLI: flag validation, mutually exclusive input modes, output format rules, exit codes, truncation caps.
- Fixtures: F1–F15 with committed expected-output assertions; determinism test runs the full matrix twice.
- Vocabulary/exit behavior: accepted `quarantine` records render as non-terminal external state and do not produce a non-zero `--exit-code`; only exact `reject`/`revoke` matches do.
- Privacy: negative greps for adversarial values across all outputs; `./scripts/check-private-paths.sh`.
- Rule catalog: entries present with limitations before any emission; emitted-rule-to-catalog resolution test (house pattern).

## 17. Implementation delivery grouping

The specification PR is separate. The 13 implementation slices in `tasks.md` remain independently testable and retain their own validation gates, stop conditions, rule-catalog updates, and limitation updates. For delivery, they are grouped into roughly five implementation PRs:

1. **PR 1 — record reader, correlation engine, CLI, and synthetic fixtures:** slices 1–2.
2. **PR 2 — npm exact-artifact evidence and combined/portfolio/path consumers:** slices 3–4 and 9.
3. **PR 3 — NuGet and Swift:** slices 5–6.
4. **PR 4 — Python and JVM:** slices 7–8.
5. **PR 5 — before/after, advisory/deployment references, docs, and closure:** slices 10–13.

This grouping reduces review and merge overhead without combining evidence contracts or skipping gates. In particular, adapter capability work remains ecosystem-specific, the three new rule contracts retain their ownership boundaries, and no PR may introduce package downloads or execution, malware/vulnerability claims, admission enforcement, automatic remediation, runtime-load claims, private or secret material, or LLM-based analysis.
