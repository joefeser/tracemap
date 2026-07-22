# SQL Validation Summary Ingestion Design

## Boundary

```text
explicit JSON file(s)
  -> strict allowlist parser + canonical digest verification
  -> repo/commit/context/validator/freshness policy
  -> accepted observations + structured gaps
  -> SQL runbook observed section
  -> release-review observed section and packet gaps
```

No step opens a database connection or executes supplied content.

## V1 contract

The contract contains `schemaVersion`, `artifactId`, `repository`, `commitSha`,
`observedAt`, `expiresAt`, `targetContext`, `validator`, `artifact`,
`publicClaimLevel`, `assertions`, and `limitations`. All objects reject unknown
properties. Strings use tight safe-token vocabularies; limitations use a closed
allowlist rather than arbitrary prose.

V1 supports validator `tracemap.sql-validation-harness` version `1.0.0`, engine
`postgresql`, closed categorical context values, and assertion statuses
`observed-pass`, `observed-fail`, `observed-indeterminate`, and `not-run`.
Assertion codes describe narrow checks, not overall safety or approval.

`artifact.digest` is lowercase SHA-256 over recursively key-sorted compact JSON
after replacing the digest value with the empty string. Array order remains
significant. This makes tampering detectable without signing or trusting prose.
V1 does not claim signer identity or non-repudiation.

## Deterministic evaluation

The CLI requires an explicit RFC3339 `--sql-validation-as-of` instant whenever
a summary is supplied. Release review selects the source whose repository and
commit match the summary and applies the same explicit instant. This avoids
machine-clock-dependent output while allowing validation to occur after the
static scan.

The target context must equal one runbook step group. A summary that cannot be
bound to exactly one compatible source is rejected with a gap.

## Duplicate policy

Inputs are parsed and sorted by safe artifact identity. Exact repeated artifacts
yield one accepted copy and `DuplicateSummary`. Reused IDs with different
digests yield `ConflictingSummary` and no accepted copy. Multiple artifacts for
the same source/context/assertion with different status yield
`ConflictingAssertion` and remove that assertion from accepted output.

## Output model

The runbook packet adds `observedValidation` while retaining all static fields.
Each observation has its own observation rule ID and explicitly says that it is
validator-produced observed evidence, not a static evidence tier. Ingestion
gaps are appended to the runbook `gaps` collection with safe derived provenance.

Release review adds `sqlValidationObservations` as a distinct section. The
existing `sqlEvidence` section remains static-only. Ingestion gaps also flow to
the top-level release-review gaps collection through the observed section.

## Threat model and limitations

- Strict parsing prevents raw output and unexpected narrative from becoming an
  output channel.
- Digest verification detects accidental or malicious post-production edits;
  it does not authenticate the producer.
- The approved validator ID/version is a compatibility policy, not proof that a
  binary was trustworthy or ran in an approved environment.
- Categorical target context deliberately omits server, database, account,
  ticket, and environment names.
- Observations are time-bounded and assertion-specific. They do not establish
  continuing state, execution safety, release approval, or DBA attestation.
