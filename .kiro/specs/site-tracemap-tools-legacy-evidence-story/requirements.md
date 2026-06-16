# site-tracemap-tools-legacy-evidence-story

## Status

Ready for implementation. Not started.

## Public claim level

concept

## Summary

Add a bounded public concept story for `tracemap.tools` that explains how
TraceMap may present legacy-adjacent static evidence once public-safe proof is
available. The story should connect themes such as WCF and service references,
.NET Remoting, WebForms event flow, legacy data metadata, build diagnostics, and
flow composition without promising shipped behavior.

This is a site/content spec only. It must not implement scanner or reducer code,
edit site source in this PR, or publish claims that outrun evidence available on
`main`.

## Shared site principle

No public conclusion without evidence.

Public copy must stay tied to rule IDs, evidence tiers, coverage labels,
limitations, commit/source provenance, and public-safe artifacts. If supporting
capability exists only on `dev`, or has not been verified on `main` with
checked-in public-safe artifacts, the site must label it as `dev-only`,
`concept`, or `hidden`, or omit it entirely.

## Main/dev wording boundary

- `main`: Public copy may describe only behavior promoted to `main` and backed
  by checked-in public-safe demos, generated summaries, or linked documentation.
- `dev`: Public copy may mention `dev` capability only with explicit `dev-only`
  or `concept` wording and must not imply shipped behavior on `main`.
- Hidden core specs: Public copy may describe the evidence theme and safety
  boundary, but not claim current support results until redacted validation or
  public fixtures exist.
- Promotion gate: Any future upgrade from `concept` to `demo` requires fresh
  validation against public-safe artifacts and an implementation-state update.

## Current theme claim ledger

This ledger captures the starting claim posture for implementation. The future
implementation must recheck promotion state before publishing copy.

| Theme | Current public label | Proof requirement | Allowed wording | Out-of-bounds wording |
| --- | --- | --- | --- | --- |
| WCF/service-reference mapping | hidden | Redacted validation summary or checked-in public fixture on `main`. | Public results remain hidden pending redacted validation. | Shipped WCF mapping, reachable services, deployed endpoints. |
| WCF metadata normalization | hidden | Redacted validation summary or checked-in public fixture on `main`. | Public results remain hidden pending redacted validation. | Shipped metadata normalization, downloaded WSDL proof, binding compatibility. |
| .NET Remoting detection | hidden | Redacted validation summary or checked-in public fixture on `main`. | Hidden legacy evidence theme, or omit. | Remoting support, hosted services, exploitability, production remoting usage. |
| WebForms event flow | hidden | Redacted validation summary or checked-in public fixture on `main`. | Hidden legacy evidence theme, or omit. | Runtime UI reachability, event execution, simulated ViewState/postback behavior. |
| Legacy data metadata | hidden | Redacted validation summary or checked-in public fixture on `main`. | Public results remain hidden pending redacted validation or public fixtures. | Database existence, query execution, schema compatibility, production data usage. |
| Build environment diagnostics | hidden | Redacted validation summary or checked-in public fixture on `main`. | Hidden diagnostic theme, or omit. Note that public results remain hidden until reviewed. | Clean repo after failed build, required install instructions, runtime compatibility. |
| Flow composition reporting | hidden | Redacted validation summary or checked-in public fixture on `main`. | Public results remain hidden pending redacted validation or public fixtures. | Runtime flow tracing, full impact proof, release safety. |

The page or section itself may remain `concept`, but each theme must keep its
own stricter label when the source capability is hidden. `Concept` may describe
the public story shape and evidence model; it must not turn hidden capability
into public support language.

## Canonical content-safety rules

The future implementation must use this as the single normative source for
rendered public content safety. Other documents should reference this section
instead of redefining shorter lists.

### Hard leak tokens and patterns

These tokens and patterns are always forbidden in the rendered legacy story
output:

- Local-only artifact contents and paths: combined SQLite file paths, analyzer
  log paths, raw scan directory paths, generated scan output roots, and
  references that imply publishing raw artifact contents.
- Internal source paths or names: `.kiro/specs/...`, unpublished private spec
  names, local absolute paths, raw repository remotes, private sample names, and
  private repository names.
- Raw sensitive values: source snippets, SQL text, endpoint addresses, config
  values, connection strings, secrets, tokens, and credential-like literals.

Bare artifact filenames such as `facts.ndjson`, `index.sqlite`,
`scan-manifest.json`, `report.md`, and `logs/analyzer.log` may be used in
documentation when they identify TraceMap output types without exposing raw
contents, generated output roots, or private paths.

### Affirmative overclaim phrases

These topics are forbidden only when rendered as affirmative claims:

- runtime proof
- production traffic
- deployment state
- endpoint performance
- exploitability
- database existence
- package compatibility
- incident cause
- release approval
- release safety

Sanctioned negated disclaimers are allowed. The implementation may satisfy this
deterministically by allowing an explicit list of approved disclaimer sentences.
A proximity-based negation heuristic may be used only as a fallback and must
document its false-positive and false-negative limitations.

Detection semantics and limitations:

- The guard is a deterministic string or regular-expression check over rendered
  public HTML or text output.
- Matching must be case-insensitive and must normalize ordinary whitespace and
  Unicode format characters before evaluating hard leak tokens.
- Automated hard-leak detection must at minimum cover `.kiro/specs` paths, local
  absolute paths, generated output root patterns, connection-string key/value
  forms, credential or token assignment patterns, raw repository remote patterns,
  and private/local URL patterns such as localhost, RFC1918 addresses, or
  file-system URLs.
- Sensitive values that cannot be detected by deterministic patterns remain a
  documented manual-review limitation.
- The guard must include positive and negative fixtures so a known forbidden
  example fails and clean concept copy passes.
- The guard must include a negated-disclaimer pass fixture and an affirmative
  overclaim fail fixture.
- If the implementation uses a proximity-based negation fallback, the guard must
  include a negation false-positive fixture where a nearby negation token does
  not negate the overclaim and the rendered text fails.
- The guard must include path-leak, connection-string, credential-assignment,
  private/local URL, and raw-remote fail fixtures.
- The guard must include a fixture proving legitimate artifact-name
  documentation passes when it does not expose raw contents or private paths.
- The guard must include a theme-enumeration fixture proving rendered hidden
  theme evidence terms fail when an adjacent `hidden`, `hidden pending
  validation`, or omission label is absent.
- The guard must include a scope fixture proving spec files and fixture source
  files are excluded from scanning.
- The guard must include an empty-output fixture proving zero rendered HTML
  files fail the scan, plus a target-page assertion that the rendered legacy
  story page or section is included in the scanned set.
- The guard must include a build-ordering assertion that it scans freshly built
  output, not stale `site/dist` contents. The preferred mechanism is wiring the
  guard into `npm run validate` after `buildSite()` or building into an isolated
  temporary output directory and scanning that directory.
- The guard is a leak-prevention check, not semantic proof that every public
  claim is safe.
- Legitimate legacy terms such as `WCF`, `.svc`, `ASMX`,
  `MarshalByRefObject`, `DBML`, and `EDMX` must not fail by themselves.
- Boundary fixtures must also prove those legitimate legacy terms do not mask
  adjacent hard leak tokens or affirmative overclaims on the same rendered page.
- Public spec source means a published documentation page or public URL, never
  an internal `.kiro/specs/...` path.

## Source Material

Implementation references only. These paths must not appear in rendered public
site copy.

- `.kiro/specs/site-tracemap-tools-legacy-validation-concept/requirements.md`
- `.kiro/specs/site-tracemap-tools-capability-matrix/requirements.md`
- `.kiro/specs/site-tracemap-tools-evidence-packets/requirements.md`
- `.kiro/specs/legacy-wcf-service-reference-mapping/requirements.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/requirements.md`
- `.kiro/specs/legacy-remoting-detection/requirements.md`
- `.kiro/specs/legacy-webforms-event-flow/requirements.md`
- `.kiro/specs/legacy-data-metadata-extraction/requirements.md`
- `.kiro/specs/legacy-build-environment-diagnostics/requirements.md`
- `.kiro/specs/legacy-flow-composition-reporting/requirements.md`

## Requirements

### Requirement 1: Legacy evidence story concept

The site shall define a future legacy evidence story page or section using
`Public claim level: concept`.

Acceptance criteria:

- The story states that it is a concept unless checked-in public-safe artifacts
  justify a stronger claim level.
- The story explains legacy static evidence as a reviewer orientation layer, not
  as runtime proof or shipped support for arbitrary legacy systems.
- The story's substantive public content is the evidence model, current claim
  ledger, promotion gate, and safety boundaries; theme names must not read as
  shipped or near-shipped support.
- The story uses TraceMap's deterministic evidence vocabulary: rule IDs,
  evidence tiers, coverage labels, limitations, generated artifacts, repo or
  commit provenance, and extractor versions.
- The story does not introduce AI, LLM, embedding, vector-database, or
  prompt-classification claims.

### Requirement 2: Legacy evidence themes

The story shall cover legacy-adjacent evidence themes only at the maturity level
supported by public-safe proof.

Acceptance criteria:

- The evidence types below describe future scope and evidence-model themes, not
  a public feature list; hidden-theme copy must place the hidden or omission
  label adjacent to any evidence enumeration that is rendered.
- WCF/service-reference copy covers config endpoints, service contracts,
  operation evidence, generated clients, `.svc` or ASMX hosts, and metadata
  normalization only as hidden or omitted themes unless public demo proof exists
  on `main`.
- Remoting copy covers namespace/type usage, `MarshalByRefObject`, channel
  registration, registration APIs, and remoting config only as static evidence
  themes with hidden wording or omission unless public demo proof exists on
  `main`.
- WebForms copy covers markup event bindings, code-behind handler resolution,
  designer fields, and possible event-to-backend paths using hidden wording or
  omission until redacted validation or public fixtures exist.
- Legacy data metadata copy covers DBML, EDMX, typed DataSet, TableAdapter,
  provider, and connection-name metadata without claiming database existence,
  query execution, schema compatibility, or production usage.
- Build diagnostics copy covers target framework, project style, toolset, SDK,
  restore, generated-file, and coverage diagnostics without telling users a
  repository is clean after a failed build or project load.
- Flow composition copy covers bounded static paths over existing evidence and
  must preserve reduced-coverage and analysis-gap labels.

### Requirement 3: Claim boundaries and artifact safety

The story shall make public-safe and unsafe claims explicit.

Acceptance criteria:

- The story does not make affirmative claims for the topics listed in the
  canonical affirmative overclaim phrases, including runtime behavior or UI
  reachability.
- The story does not publish content forbidden by the canonical content-safety
  rules.
- Public-safe examples use generated summaries, label-only counts, safe
  descriptors, hashes, rule IDs, evidence tiers, coverage labels, supporting
  IDs, and limitations.
- Any page copy that references reduced analysis must state that partial
  evidence is useful but labeled partial.
- Public copy must not link to, cite, or expose internal `.kiro/specs/...`
  paths or private spec names; source-material paths remain implementation
  guidance only.

### Requirement 4: Promotion and omission rules

The implementation shall require conservative labeling for capabilities whose
promotion state is uncertain.

Acceptance criteria:

- WHEN a capability is verified only on `dev` THEN the site SHALL label it
  `dev-only` or keep it in concept wording.
- WHEN a capability is hidden in a core spec until validation exists THEN the
  site SHALL omit detailed support claims or state that public results remain
  hidden pending redacted validation.
- WHEN a capability has not landed on `main` THEN the site SHALL NOT describe it
  as shipped, available, or demonstrated.
- WHEN public-safe checked-in artifacts become available on `main` THEN a future
  implementation MAY upgrade the relevant row or section to `demo` after
  updating implementation state and validation notes.
- WHEN a theme is upgraded to `demo` THEN the implementation SHALL record the
  exact checked-in public-safe artifact path or generated summary that supports
  the stronger label.
- WHEN a theme is upgraded to `demo` THEN the implementation SHALL record the
  supporting rule IDs from the source legacy spec or generated public-safe
  evidence.
- WHEN the future implementation completes the promotion check THEN it SHALL
  record the per-theme result in `implementation-state.md`, including negative
  results such as "confirmed not on `main`" or "public proof not available."

### Requirement 5: Future implementation scope

The future implementation shall be discoverable without changing scanner or
reducer behavior.

Acceptance criteria:

- The implementation uses existing static site patterns and does not introduce a
  new runtime service.
- The implementation may add a concept page, a section on an existing legacy
  page, or linked cards from existing site surfaces, but must keep the first
  implementation small enough for review.
- Before implementing the guard, the implementation SHALL pin the concrete
  rendered target: either a page file/glob for a standalone page or a specific
  section anchor/extraction strategy for a section on an existing page.
- A new standalone page SHALL satisfy the existing top-navigation validation.
  Adding a new top-nav entry mutates all rendered pages and should be avoided
  unless the implementation deliberately accepts that broader site change.
- Discovery links, sitemap metadata, and browser/layout checks are included only
  in the future implementation PR, not this spec-prep PR.
- The implementation-state note records branch, scope, claim boundaries,
  validation, and follow-up items when implementation begins.
- The future implementation SHALL add a new automated rendered-content safety
  check using the canonical content-safety rules and wire it into `npm test` or
  `npm run validate`; current `npm run validate` does not perform this
  content-safety scan.
- The safety check SHALL run after the site build against the rendered legacy
  story page or containing page only. Existing rendered pages are out of scope
  unless the future implementation modifies them for this story.
- The safety check SHALL exclude `.kiro/**`, spec source files, test fixture
  definitions, and other non-rendered source files. These exclusions protect
  fixture definitions and spec text from self-scanning if a future harness uses
  source-side fixtures.
- The safety check SHALL fail if zero rendered HTML files are found and SHALL
  assert that the rendered legacy story page or section is included in the
  scanned set.
- The preferred wiring is `npm run validate` after `buildSite()` or an isolated
  temp-output test that builds the site and scans that fresh output. IF the
  safety check is wired into `npm test`, THEN the test SHALL build into an
  isolated temp directory or otherwise guarantee it is scanning fresh output and
  does not mutate or race the shared `site/dist`.
- The future implementation SHALL ship the guard in the same PR as the rendered
  legacy story page or section, not before the target page exists.
