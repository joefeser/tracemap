# SQL Secret-Bearing Step Safety Design

## Overview

Secret safety is a classifier and projection boundary around SQL parsing:

```text
source bytes
  -> streaming/token-aware high-risk detector
  -> category-only finding + sanitized gap
  -> downstream property allowlist
```

Source bytes remain available only to the local scanner's normal file-reading
path. High-risk values never enter the generic fact-property bag, diagnostics,
or report model.

## Proposed Rule Model

Implementation must reconcile names with the live catalog.

| Rule ID | Fact | Tier ceiling | Limitation |
| --- | --- | --- | --- |
| `database.sql.secret-bearing-step.v1` | `SecretBearingSqlStep` or existing safe finding type | `Tier2Structural` | Detects known structural containers; cannot prove all secrets are found. |
| `database.sql.secret-text-candidate.v1` | `SecretBearingSqlStep` | `Tier3SyntaxOrTextual` | Keyword/category candidate only; false positives and negatives are possible. |
| `database.sql.secret-safety-gap.v1` | `AnalysisGap` | `Tier4Unknown` | Safety could not be established; no secret-free claim. |

Recommended category codes include `credential-option`, `connection-material`,
`user-mapping`, `remote-query-input`, `subscription-connection`,
`scheduled-command`, `secret-reference`, and `dynamic-secret-boundary`.

## Detection Architecture

Run a lightweight high-risk detector before richer SQL properties are created.
It identifies token ranges for known constructs and marks their values as
non-projectable. A PostgreSQL-aware pass handles quoted strings, dollar-quoted
bodies, comments, and option lists conservatively. If tokenization fails near a
high-risk construct, the whole statement becomes unresolved/high-risk for
rendering.

The classifier result is independent from whether a credential is literal,
placeholder, function call, or concatenation. Literal extraction is never
needed.

## Allowlist Projection

The secret finding model is constructed from an allowlist, not by redacting a
generic property dictionary after the fact. Allowed fields:

- classification and category code;
- statement ordinal and repo-relative span;
- rule, tier, coverage, limitation, extractor version, and commit provenance;
- a shape hash only when its input normalization excludes all value-bearing
  token ranges and shared policy explicitly permits it.

Do not include option keys when custom keys could reveal private vocabulary;
map recognized keys to category codes. Do not hash raw values, because hashes
of credentials or low-entropy values are unsafe and unnecessary.

## Downstream Enforcement

NDJSON serialization, SQLite properties, Markdown reports, exports, combine,
diff, and runbook generation each apply the same allowlist defensively. Logs
receive safe error codes and spans, never parser token contents. Exceptions
crossing CLI boundaries are sanitized.

The runbook reducer maps every non-`not-established` high-risk result to a
stop/owner-review condition. It may describe the protected-material category,
but cannot emit a runnable template.

## False Positives and Coverage

Known high-risk syntax is intentionally conservative. Dynamic SQL, custom
wrappers, external includes, encoded values, procedural bodies, and future
dialects can escape structural detection. These are documented limitations and
may emit reduced-coverage gaps. No report uses the phrase `secret-free`.

## Test Strategy

Build sentinel fixtures with entirely synthetic values. A shared leak assertion
reads text artifacts, SQLite text columns/properties, captured logs, exceptions,
and combined/export output. It checks complete sentinels and distinctive
substrings. Separate tests prove that no digest of planted raw secret values is
present when implementations use hashes for safe statement shapes.

Negative controls keep ordinary uses of words such as `user` or `key` from
becoming structural credential proof. Ambiguous cases can remain
`possible-secret` without exposing text.

## Future Engines

Engine adapters may add structural recognizers behind the same category-only
contract. They must not weaken the shared allowlist. Live vault or database
integration is not an extension of this spec and requires separate authority
and design.
