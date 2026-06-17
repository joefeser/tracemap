# Vault Export Hidden Safety Requirements

## Introduction

`tracemap vault export` is a deterministic local navigation export over
existing TraceMap static evidence. The current exporter rejects any generated
Markdown or `graph.json` string leaf that looks secret-like. That is correct
for public-safe and demo-safe output, but it is too blunt for hidden/local
exports from legacy or private codebases where safe context values can contain
words such as `secret`, `token`, `password`, or `key` as part of legitimate
relative file paths, symbol names, route/action/model/member names, or evidence
locations.

This spec defines a safe hidden/local export policy that can render category
labels, hash, omit, or gap unsafe-looking values without failing the entire
export when the value is not raw secret material. Public-safe and demo-safe
validation stays strict. The exporter remains deterministic and static-only.

Public claim level: hidden until implemented and validated with safe fixtures.

## Scope

In scope:

- A deterministic safety policy for hidden/local vault exports.
- Context-aware handling of secret-like tokens in safe contexts:
  repo-relative paths, member names, model names, route/action names, symbol
  display names, and evidence locations.
- Clear hard-fail rules for raw secrets and unsafe data categories.
- Exporter-created safety gaps with rule IDs, evidence tiers, and documented
  limitations.
- Markdown and `graph.json` determinism expectations after redaction, hashing,
  category labels, omission, and gap emission.
- Tests for hidden/local success, public/demo strictness, raw-secret rejection,
  local absolute path rejection, deterministic output, and generated file
  collision behavior.
- Documentation and rule catalog update requirements for implementation.

Out of scope:

- Product-code implementation in this spec PR.
- Changes to scanner, reducer, language adapters, or combined-index schema.
- Site files or site specs.
- Runtime proof, browser execution, service calls, database calls, config
  transform execution, or environment probing.
- LLM calls, embeddings, vector databases, prompt-based classification, or
  model-driven safety decisions.
- Publishing hidden/local exports or weakening public-safe/demo-safe gates.
- Storing raw source snippets, raw SQL, connection strings, captured
  credentials, production data, raw remotes, raw URLs, local absolute paths, or
  private sample identifiers.

## Requirements

### Requirement 1: Preserve Strict Public And Demo Safety

**User Story:** As a TraceMap maintainer, I want public-safe and demo-safe vault
exports to keep rejecting unsafe-looking content so existing publication safety
does not regress.

#### Acceptance Criteria

1. WHEN `--minimum-claim-level public-safe` or `--minimum-claim-level demo-safe`
   is used THEN generated Markdown and `graph.json` string leaves SHALL keep
   rejecting local absolute paths, raw remotes, raw URLs, raw SQL, config
   values, connection strings, source snippets, analyzer diagnostics, stack
   traces, credentials, secrets, tokens, private sample identifiers, production
   data, and secret-like values.
2. WHEN public/demo validation rejects a value THEN the diagnostic SHALL include
   a sanitized category, rule ID, evidence tier, and JSON pointer or Markdown
   file/line location, and SHALL NOT echo the unsafe value.
3. WHEN a value is accepted in hidden/local mode only because its context is
   safe THEN the same value SHALL NOT become accepted in public/demo output
   unless a later, explicit public/demo policy allows that exact context.
4. WHEN claim-level filtering removes hidden evidence from a public/demo export
   THEN the exporter SHALL emit existing sanitized omission gaps rather than
   rendering hidden values.
5. WHEN public/demo output has no visible non-gap graph evidence after filtering
   THEN the exporter SHALL fail as it does today; hidden/local fallback SHALL
   NOT make the output publishable.

### Requirement 2: Allow Hidden/Local Safe Secret-Like Contexts

**User Story:** As an operator scanning a legacy local codebase, I want hidden
vault export to succeed when legitimate static evidence names contain
secret-like words but are not raw secret material.

#### Acceptance Criteria

1. WHEN the selected output classification is `hidden` THEN the exporter MAY
   preserve, render a category label, hash, omit, or gap secret-like values
   according to their source context instead of failing the whole export.
2. WHEN a secret-like token appears in a repo-relative file path, member name,
   model name, route/action name, symbol display name, or evidence location THEN
   the exporter SHALL treat the context as potentially safe only after
   validating that the value is bounded, printable, non-absolute, non-URL,
   non-remote, non-SQL, and not credential material.
3. WHEN a repo-relative path is accepted in hidden mode THEN it SHALL remain
   repo-relative, normalized to forward slashes, free of drive roots, free of
   home/user/temp prefixes, and free of `..` traversal segments.
4. WHEN a symbol, route, action, model, or member display name is accepted in
   hidden mode THEN it SHALL be bounded by a documented length, printable, and
   used only as local evidence display text or evidence metadata.
5. WHEN a safe-context value fails hidden-mode validation but does not match any
   Requirement 3 hard-fail category THEN the exporter SHALL omit the field or
   render a category label and emit an exporter safety gap instead of failing
   the entire export.
6. WHEN preserving raw hidden/local display text is unnecessary for navigation
   THEN the exporter SHALL prefer stable context-separated hashes or
   category labels over raw display. Safe repo-relative paths and evidence
   locations MAY remain raw in hidden mode when they are needed for local
   navigation and pass final context-aware validation.
7. WHEN safe display names contain raw SQL action words such as update or
   delete but are not raw SQL text THEN hidden mode SHALL treat them as
   safe-context display names instead of raw SQL, after the same bounded
   printable validation.

### Requirement 3: Hard Fail On Raw Unsafe Data

**User Story:** As a project owner, I want hidden/local export to avoid leaking
actual secrets or machine/private data even when I ask for local evidence.

#### Acceptance Criteria

1. WHEN generated output would contain raw credentials, API keys, access tokens,
   private keys, authorization headers, passwords, captured secret values,
   connection strings, raw remotes, raw URLs, raw SQL, source snippets, local
   absolute paths, production data, or private sample identifiers THEN the
   exporter SHALL hard fail or reject the unsafe field before writing output.
2. WHEN the unsafe value is a local absolute path or raw remote/URL THEN hidden
   mode SHALL NOT hash the raw value or render a category label for it into
   generated output; it SHALL fail or replace the field from already-safe source
   evidence.
3. WHEN the unsafe value is a credential-like or high-risk secret pattern THEN
   hidden mode SHALL NOT hash it, display it, use it in note names, use it in
   stable IDs, or include it in diagnostics.
4. WHEN hard failure occurs THEN diagnostics SHALL be sanitized and include the
   safety category, rule ID, evidence tier, and output location only.
5. WHEN a generated file already exists and a later safety failure is detected
   THEN the exporter SHALL leave existing files unchanged.
6. WHEN hard-fail detection is implemented THEN it SHALL cover absolute path
   variants and credential patterns beyond the current substring checks,
   including traversal segments, temp roots, UNC paths, drive-rooted paths,
   home shorthand, environment-home prefixes, API keys, private keys,
   authorization headers, and session identifiers.

### Requirement 4: Emit Rule-Backed Safety Gaps For Omitted Local Evidence

**User Story:** As a reviewer, I want omitted hidden evidence to be visible as a
rule-backed gap rather than silently disappearing.

#### Acceptance Criteria

1. WHEN the exporter omits, hashes, or category-labels a hidden/local field
   because it is unsafe-looking but not hard-fail raw secret material THEN it
   SHALL emit a safety gap or limitation unless the omission is purely cosmetic
   and does not affect graph interpretation.
2. WHEN an exporter-created safety gap is emitted THEN it SHALL include a
   `vault-export.*.v1` rule ID, `Tier4Unknown` evidence tier unless a stronger
   tier is justified by documented static evidence, a gap kind, source scope
   where safe, evidence location category where safe, and documented
   limitations.
3. WHEN a gap references an unsafe field THEN it SHALL use a closed category
   whose rendered label is itself safe under final validation, such as
   `sensitive-word-safe-name`, `hidden-display-name-hashed`,
   `repo-relative-path-sensitive-word`, `evidence-location-category-only`,
   `raw-sensitive-value-rejected`, or `local-path-rejected`, and SHALL NOT echo
   the raw value.
4. WHEN a hidden/local graph becomes partial because fields were omitted THEN
   `graph.json` settings and Markdown index pages SHALL mark the export partial.
5. WHEN a public/demo export omits hidden evidence because of claim-level
   filtering THEN existing hidden-evidence omission gaps SHALL remain distinct
   from hidden/local safety gaps.

### Requirement 5: Deterministic Markdown And Graph JSON

**User Story:** As a maintainer, I want hidden/local safety handling to produce
reviewable byte-stable output.

#### Acceptance Criteria

1. WHEN the same hidden/local inputs and options are exported twice THEN
   generated Markdown and `graph.json` SHALL be byte-stable.
2. WHEN a field is hashed THEN the hash SHALL use a documented
   context-separated prefix, UTF-8 input, stable normalization, lowercase hex
   output, and a documented truncation length.
3. WHEN a field is category-labeled THEN labels SHALL come from a closed
   vocabulary and sort ordinally with the rest of the graph.
4. WHEN a field is omitted or gap records are emitted THEN node, edge, gap,
   limitation, link, tag, frontmatter, and array ordering SHALL remain
   deterministic.
5. WHEN generated Markdown is written THEN content hashes and frontmatter
   ordering SHALL remain self-consistent after hidden/local safety transforms.
6. WHEN `graph.json` is written THEN its `contentHash` SHALL be computed after
   all safety transforms with only the hash field blanked, matching existing
   canonical JSON behavior.
7. WHEN output path or checkout root changes but input evidence is otherwise
   equivalent THEN accepted repo-relative paths, hashed labels, gap IDs, and
   generated file names SHALL remain stable.

### Requirement 6: Generated File Collision Safety

**User Story:** As a vault user, I want safety changes to preserve the existing
protection against overwriting user notes or stale generated files.

#### Acceptance Criteria

1. WHEN hidden/local safety handling changes generated content THEN existing
   valid generated files MAY be replaced only after the newly generated content
   passes all safety checks.
2. WHEN an existing generated file has a missing, invalid, or stale content hash
   THEN export SHALL fail with a sanitized `GeneratedFileStale` diagnostic
   unless `--force` is supplied.
3. WHEN `--force` is supplied THEN it SHALL NOT bypass public/demo strictness,
   hidden/local raw-secret rejection, local absolute path rejection, claim-level
   filtering, schema validation, or private-path gates. It SHALL only allow
   replacement of stale generated files after the newly generated content has
   passed every safety validation.
4. WHEN an existing non-generated file would collide with a generated file THEN
   export SHALL fail with a sanitized `UserFileCollision` diagnostic in every
   claim level.
5. WHEN a collision failure occurs THEN the diagnostic SHALL NOT include local
   absolute output paths or unsafe values.

### Requirement 7: Documentation And Rule Catalog

**User Story:** As an implementer, I want docs and rule catalog entries that
explain the new hidden/local safety behavior and its limits.

#### Acceptance Criteria

1. WHEN implementation lands THEN `docs/VAULT_EXPORT.md` SHALL document the
   distinction between strict public/demo validation and hidden/local
   category label/hash/omit/gap behavior.
2. WHEN implementation lands THEN documentation SHALL list raw categories that
   always hard fail, including raw secrets, connection strings, local absolute
   paths, raw remotes, raw URLs, raw SQL, snippets, captured credentials,
   private sample identifiers, and production data.
3. WHEN implementation lands THEN documentation SHALL describe safe-context
   handling for repo-relative paths, symbols, routes/actions/models/members, and
   evidence locations without showing private sample names or paths.
4. WHEN new exporter-created rule IDs are emitted THEN `rules/rule-catalog.yml`
   SHALL document each rule's purpose, evidence tier, and limitations before
   tests assert it.
5. WHEN documentation includes commands or examples THEN it SHALL use generic
   placeholders only.

### Requirement 8: Tests And Validation

**User Story:** As a maintainer, I want focused tests that prevent both export
failures on safe hidden names and safety regressions in public/demo output.

#### Acceptance Criteria

1. WHEN implementation finishes THEN tests SHALL prove hidden/local vault export
   succeeds when safe repo-relative paths, member names, model names,
   route/action names, symbol names, and evidence locations contain
   secret-like words.
2. WHEN implementation finishes THEN tests SHALL prove public-safe and
   demo-safe outputs still reject or filter the same secret-like names according
   to strict public/demo policy.
3. WHEN implementation finishes THEN tests SHALL prove raw secret material is
   rejected in hidden/local and public/demo modes without echoing the raw value.
4. WHEN implementation finishes THEN tests SHALL prove local absolute paths are
   rejected in hidden/local and public/demo modes.
5. WHEN implementation finishes THEN tests SHALL prove raw remotes, raw URLs,
   raw SQL, source snippets, connection strings, captured credentials, private
   sample identifiers, and production data are not rendered.
6. WHEN implementation finishes THEN tests SHALL prove deterministic Markdown
   and `graph.json` bytes across reruns and across different output roots.
7. WHEN implementation finishes THEN tests SHALL prove generated file collision
   behavior for valid generated replacement, stale generated files,
   `--force`, and non-generated user files.
8. WHEN implementation finishes THEN tests SHALL prove every exporter-created
   safety gap carries a documented `vault-export.*.v1` rule ID and evidence
   tier, with the rule documented in `rules/rule-catalog.yml` before the test
   is committed.
9. WHEN implementation finishes THEN `git diff --check`,
   `./scripts/check-private-paths.sh`, and focused vault export tests SHALL
   pass.
10. WHEN implementation touches .NET code THEN `dotnet test
    src/dotnet/TraceMap.sln --filter VaultExport` SHALL pass; broader
    `dotnet test src/dotnet/TraceMap.sln` SHOULD run unless explicitly
    deferred with a reason.
11. WHEN implementation finishes THEN tests SHALL prove classifier-approved
    hidden raw values survive final generated-output validation and appear, or
    are represented by the required hash/category, in both Markdown and
    `graph.json`.
12. WHEN implementation finishes THEN tests SHALL prove safety gap categories
    and labels do not self-trigger unsafe-value validation.
13. WHEN implementation finishes THEN tests SHALL prove traversal segments,
    temp roots, UNC paths, drive-rooted paths, home shorthand,
    environment-home prefixes, API keys, private keys, authorization headers,
    and session identifiers are rejected.
14. WHEN implementation finishes THEN tests SHALL prove safety omissions mark
    the hidden/local export partial when graph interpretation is affected.
15. WHEN implementation finishes THEN tests SHALL prove hidden-evidence
    omission gaps remain distinct from hidden/local safety gaps when both
    appear in one export.
16. WHEN implementation finishes THEN tests SHALL prove hidden safe display
    names containing SQL action words are not misclassified as raw SQL.
17. WHEN implementation finishes THEN tests SHALL prove hidden safe
    repo-relative paths containing SQL action words are not misclassified as
    raw SQL.
18. WHEN implementation finishes THEN tests SHALL prove rejected stable-ID
    source components omit the affected node or edge and emit a sanitized safety
    gap without using the rejected raw value.
19. WHEN implementation finishes THEN tests SHALL prove empty, whitespace-only,
    and non-printable display names are rejected or represented by an approved
    category/gap.
20. WHEN implementation finishes THEN tests SHALL prove path normalization,
    hash input encoding, claim-level transitions, and safety gap ordering are
    deterministic.

## Deferred Follow-Ups

- Public/demo relaxation for narrowly proven safe contexts, if ever needed.
- A richer safety policy shared across evidence pack and vault exporters.
- Optional local-only debug logs outside generated vault output.
- Site consumption of public-safe summaries from future specs.
