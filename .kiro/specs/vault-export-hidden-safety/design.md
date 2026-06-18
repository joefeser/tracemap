# Vault Export Hidden Safety Design

## Overview

This design refines vault export safety validation by separating output claim
level from value context. Public-safe and demo-safe output remains strict:
secret-like strings continue to reject or filter. Hidden/local output gets a
deterministic context-aware safety transform so legitimate names such as
repo-relative paths, member names, model names, route/action names, symbol
display names, and evidence locations do not fail the whole export merely
because they contain a secret-like word.

The exporter still never renders raw secrets, connection strings, local
absolute paths, raw remotes, raw URLs, raw SQL, snippets, captured credentials,
private sample identifiers, or production data.

## Existing Behavior To Preserve

The existing `evidence-graph-vault-export` spec and implementation define:

- `tracemap vault export` as a static export, not a scanner or reducer.
- `hidden`, `demo-safe`, and `public-safe` claim levels.
- generated Markdown frontmatter sentinels and content hashes.
- canonical `graph.json` with `contentHash`.
- generated file collision protection.
- public/demo claim-level filtering and hidden-evidence omission gaps.
- sanitized diagnostics such as `UnsafeValueRejected`,
  `GeneratedFileStale`, and `UserFileCollision`.

This spec does not replace those rules. It adds hidden/local context handling
before final generated-output validation.

## Safety Classifier Shape

Introduce a deterministic exporter safety classifier with three inputs:

1. `claimLevel`: `hidden`, `demo-safe`, or `public-safe`.
2. `valueContext`: a closed context enum identifying how the value will be used.
3. `value`: the string candidate after source-specific normalization.

Suggested contexts:

| Context | Hidden/local handling | Public/demo handling |
| --- | --- | --- |
| `RepoRelativePath` | Preserve if normalized and bounded; otherwise omit/gap. | Strict existing rejection for secret-like values unless already filtered out. |
| `EvidenceLocation` | Preserve safe relative path/span or render a category label for the location. | Strict existing rejection for secret-like values. |
| `SymbolDisplayName` | Preserve bounded safe name, render a category label, hash, or gap. | Strict existing rejection for secret-like values. |
| `RouteActionModelMemberName` | Preserve bounded safe name, render a category label, hash, or gap. | Strict existing rejection for secret-like values. |
| `StableTraceMapId` | `AllowRaw` only for already-stable internal IDs produced by TraceMap after source validation. When an ID is constructed from source values, those source values SHALL be validated under their semantic context before ID construction. Unstable external IDs reject. | Same. |
| `RuleId` | Must be closed rule ID vocabulary. | Same. |
| `ClosedVocabulary` | Must be closed vocabulary. | Same. |
| `DiagnosticCategory` | Must be sanitized category only. | Same. |
| `RawExternalOrDataValue` | Hard fail or omit from source evidence before render. | Hard fail or omit. |

The classifier returns one of:

- `AllowRaw`: safe for the selected claim level and context.
- `AllowHash`: render only a deterministic context hash.
- `AllowCategory`: render only a closed category label.
- `OmitWithGap`: omit from node/edge text and emit a safety gap.
- `Reject`: hard fail before writing output.

### Stable ID Construction From Validated Components

When constructing a stable TraceMap ID from one or more source values:

1. Each source component SHALL be validated under its semantic context before
   ID construction.
2. Components with `AllowRaw` or `AllowHash` MAY contribute their approved
   normalized value or hash input to hidden-mode ID construction.
3. Components with `AllowCategory` or `OmitWithGap` SHALL contribute only the
   safe category or hash representation chosen by the classifier.
4. If any component produces `Reject`, the entire node or edge requiring that
   ID SHALL be omitted, and the exporter SHALL emit a safety gap that references
   only the failed component context and sanitized output location.
5. The exporter SHALL NOT build fallback IDs from raw rejected components.

## Final Validation Integration

The current generated-output validator is context-free: it scans every JSON
string leaf and Markdown line after rendering. This spec requires that final
validation become claim-level and context aware. Implementation SHALL replace
or extend the current string-leaf validator so each generated value carries, or
can be resolved to, one of the closed `valueContext` entries and the
classifier's decision.

Required behavior:

- public/demo leaves continue to use strict validation for every rendered
  string;
- hidden leaves that were classified as `AllowRaw` pass final validation only
  for their approved context;
- hidden leaves classified as `AllowHash`, `AllowCategory`, or `OmitWithGap`
  must render only the approved hash, category, or gap fields;
- `Reject` values fail before rendering or before any file is written;
- closed labels, gap kinds, rule IDs, frontmatter values, and diagnostic
  categories must themselves be safe strings that do not trip final validation;
- no unclassified JSON leaf or Markdown line may bypass final validation.

All closed-vocabulary labels, gap kinds, rule IDs, frontmatter enum values, and
diagnostic categories SHALL be pre-validated by the same context-free safety
check used for public/demo scalar values before the exporter can emit them. A
label that would trip that check is invalid spec vocabulary and must be
renamed, not allowlisted case by case in generated output.

This can be implemented with a typed graph model carrying safety metadata, a
JSON-pointer/Markdown-location validation map built during rendering, or an
equivalent deterministic mechanism. The implementation must not rely on a raw
substring allowlist that silently accepts arbitrary hidden text.

## Hard-Fail Categories

These categories reject in every mode:

- raw credentials, passwords, API keys, tokens, private keys, authorization
  headers, session identifiers, or captured secret values;
- connection strings or config values that include credentials or endpoints;
- local absolute paths, drive-rooted paths, home paths, temp paths, UNC paths,
  or `file://` paths;
- raw repository remotes, raw URLs, hostnames with scheme, query strings, or
  URL credentials;
- raw SQL text, SQL batches, or stored-procedure bodies;
- raw source snippets, analyzer diagnostics, stack traces, or tool logs;
- private sample identifiers or production data.

Hard-fail diagnostics are sanitized and include rule ID, evidence tier,
category, and JSON pointer or Markdown location. Example:

```text
vault-export.validation.unsafe-value-rejected.v1 [Tier4Unknown]: raw-sensitive-value at $.nodes[3].displayName
```

The diagnostic includes category and location only. It does not echo the value.
The hard-fail detector must be stronger than the current substring checks and
cover traversal segments, temp roots, UNC paths, drive-rooted paths,
home shorthand, environment-home prefixes, API key patterns, private key
markers, authorization headers, and session identifiers.

## Hidden/Local Safe Context Rules

### Repo-Relative Paths

Hidden/local output may preserve a repo-relative path when all are true:

- it is not absolute on Unix, Windows, UNC, or URI forms;
- it normalizes to forward slashes;
- it contains no empty segment, `.` segment, `..` segment, home fragment,
  drive root, temp root, or user profile segment;
- it is bounded by a documented maximum length;
- it contains printable path characters only;
- it does not include raw URL, raw remote, SQL, connection-string, credential,
  or snippet patterns.

If the only issue is a secret-like word in an otherwise safe relative path,
hidden mode may render the relative path. If the path is too revealing or
ambiguous but not raw secret material, render a category or hash and emit a
gap.

### Evidence Locations

Evidence locations should prefer structured fields:

```json
{
  "path": "Controllers/TokenReviewController.cs",
  "startLine": 12,
  "endLine": 18
}
```

Hidden mode may preserve safe relative path plus line span. If path text is
unsafe-looking but not hard-fail material, use:

```json
{
  "locationCategory": "repo-relative-path-sensitive-word",
  "locationHash": "evidence-location-sha256:<truncated-hex>",
  "span": "line-span-present"
}
```

The hash input is the normalized relative path and span with a context prefix,
not any local absolute path.

### Symbol, Route, Action, Model, And Member Names

Hidden mode may preserve bounded display names when they:

- are printable;
- are not URLs, remotes, SQL, snippets, connection strings, or credentials;
- contain no newlines or tab characters and have no run of more than one space;
- fit a documented length bound;
- are attached to static evidence with rule ID, evidence tier, and source span
  or safe evidence location.

When a display name is secret-like but otherwise safe, hidden mode SHALL use
this default representation:

- use `displayCategory: "sensitive-word-safe-name"` or
  `displayHash: "symbol-display-sha256:<truncated-hex>"` for symbol, route,
  action, model, and member display names unless raw local display is clearly
  needed for navigation;
- omit the display name and emit a safety gap when the value is too revealing
  but not hard-fail material.

This default makes tests assertable while preserving room for category/hash
choice by context. Legitimate action or member names containing SQL action
words such as "Update" or "Delete" follow the same safe display-name path when
they are not raw SQL text.
Repo-relative paths containing those action words follow the repo-relative path
rules and are not raw SQL unless they contain SQL syntax, statement structure,
or source text rather than a path segment.

## Public/Demo Strictness

Public/demo validation stays conservative. A value that contains secret-like
tokens in a path or symbol still rejects unless it is filtered out before render
or represented only by an existing public/demo-safe category that does not
include the token.

This intentionally means hidden/local output may succeed for safe-context
secret-like names while `--minimum-claim-level demo-safe` and
`--minimum-claim-level public-safe` fail or produce omission gaps.

## Safety Gaps

Exporter-created hidden/local safety gaps use the `vault-export.*.v1`
namespace. Reuse existing catalog entries where their limitations fit, and add
new entries only for genuinely new gap behavior:

| Rule ID | Tier | Purpose | Limitation |
| --- | --- | --- | --- |
| `vault-export.gap.unsafe-symbol-omitted.v1` | `Tier4Unknown` | Existing rule for omitted unsafe symbol display. Extend its limitation text if used for hidden display-name category/hash behavior. | Omission or hash proves only local evidence identity, not public safety. |
| `vault-export.validation.unsafe-value-rejected.v1` | `Tier4Unknown` | Existing rule for rejected unsafe generated values, including raw sensitive values and local paths. Extend its categories rather than adding duplicate validation rules. | Diagnostic omits the value, so local debugging requires inspecting source artifacts separately. |
| `vault-export.gap.hidden-safe-context-omitted.v1` | `Tier4Unknown` | New rule only if needed for non-symbol hidden safe-context values omitted or rendered as category labels by the exporter. | The exporter does not prove whether the omitted name is semantically important. |
| `vault-export.gap.evidence-location-category-only.v1` | `Tier4Unknown` | New rule only if evidence location is reduced to category/span because path display is unsafe-looking. | Location may be less useful for manual navigation. |
| `vault-export.gap.unsafe-id-component-omitted.v1` | `Tier4Unknown` | New rule only if a node or edge is omitted because a required stable-ID component was rejected before ID construction. | The exporter does not prove whether the omitted graph element would have been navigable with safe identity evidence. |

Do not create `vault-export.validation.raw-secret-rejected.v1` or
`vault-export.validation.local-path-rejected.v1` unless the implementation
proves the existing `vault-export.validation.unsafe-value-rejected.v1` cannot
express the categories. Every emitted gap or validation finding must be
documented before tests assert it.
Rule reuse priority: prefer extending an existing rule's limitation text over
creating a new rule ID. Create a new `vault-export.gap.*` rule only when the
evidence tier differs and cannot be reconciled, the gap kind differs
semantically such as omission versus transformation versus validation failure,
or the limitation describes a materially different analysis constraint users
need to track separately. If the gap differs only in the affected value category
such as symbol, path, or evidence location, use a single rule ID with
category-specific limitation text.

Gap records should include:

- stable gap ID;
- rule ID;
- evidence tier;
- gap kind;
- claim level;
- source scope when safe;
- output location category or JSON pointer/Markdown line;
- safe limitation text;
- supporting node or edge IDs when available.

Gap records sort by stable gap ID, then rule ID, then safe output location
category or JSON pointer, all using ordinal comparison.

## Deterministic Transform Order

The implementation should apply transforms in this order:

1. Read source evidence without mutating inputs.
2. Build graph model from existing rule-backed evidence.
3. Apply claim-level filtering to remove nodes, edges, and evidence that will
   not appear in the selected output. Values removed by filtering do not go
   through hidden/local safety transforms except as sanitized omission counts or
   gaps.
4. Apply hidden/local safety transforms only to eligible contexts that remain
   in the output graph.
5. Add safety gaps/limitations.
6. Render canonical `graph.json` and Markdown.
7. Validate every JSON string leaf and every Markdown line with the
   context-aware final validator described above.
8. Check generated file provenance/collisions.
9. Write files only after all validations pass.

No partially written output should result from a late validation failure.

## Hashing

Hashing is allowed only for safe categories, never for raw secrets or local
absolute paths. Suggested context prefixes:

```text
vault-export/hidden-safe-context/v1/<context>
vault-export/evidence-location/v1
vault-export/display-name/v1/<kind>
```

Hash normalization:

- UTF-8 input;
- LF line endings;
- trimmed outer whitespace where context permits;
- repo-relative paths normalized to forward slash;
- lowercase hex SHA-256;
- truncation length documented in the graph schema or exporter constants.
  Recommended defaults are 24 hex characters for display-name and
  repo-relative-path hashes, and 32 hex characters for evidence-location hashes
  that combine path and span.

Hash labels must not imply public safety. Hashes are deterministic local
identities for hidden/local navigation only; they do not sanitize or prove
public safety of the source value.

## Markdown Output

Generated Markdown should make reductions visible:

```markdown
## Safety Gaps

| Gap | Rule | Tier | Category | Limitation |
| --- | --- | --- | --- | --- |
| Hidden display name hashed | `vault-export.gap.unsafe-symbol-omitted.v1` | `Tier4Unknown` | `sensitive-word-safe-name` | Hash is local evidence identity only. |
```

Frontmatter keys remain closed and deterministic. If a display name was
rendered as a category label or hash, frontmatter should store only the safe
category or hash label.

## Graph JSON Output

`graph.json` should represent transformed values explicitly:

```json
{
  "displayName": "sensitive-word-safe-name",
  "displayNameSafety": {
    "mode": "category",
    "category": "sensitive-word-safe-name",
    "ruleId": "vault-export.gap.unsafe-symbol-omitted.v1"
  }
}
```

Alternative field names are acceptable if they fit the existing graph model and
remain deterministic. Do not add raw unsafe values behind optional debug flags
inside generated vault output.

## Test Fixture Guidance

Use checked-in synthetic names only. Do not mention or encode private sample
names, private path fragments, or private repository names.

Good fixture categories:

- repo-relative path with a secret-like directory or filename;
- controller/action/model/member names with secret-like words;
- action/member names with SQL action words such as Update and Delete that are
  not raw SQL text;
- repo-relative paths with SQL action words that are not raw SQL text;
- evidence location path with a secret-like safe segment;
- planted raw secret pattern that must reject;
- planted local absolute path that must reject;
- planted traversal, temp-root, UNC, drive-root, home shorthand, and
  environment-home path forms that must reject;
- planted API key, private key marker, authorization header, and session
  identifier patterns that must reject;
- planted raw URL, raw remote, raw SQL, and connection string that must reject.

## Documentation Updates

Implementation should update `docs/VAULT_EXPORT.md` with:

- the strict public/demo rule;
- the hidden/local safe-context rule;
- hard-fail categories;
- gap and category label behavior;
- validation commands.

Rule catalog updates are required before emitting new rule IDs.

## Open Questions

- Whether the safety classifier should be shared with evidence-pack validation.
  Recommendation: keep this PR scoped to vault export, then extract a shared
  helper only if duplication becomes meaningful.
