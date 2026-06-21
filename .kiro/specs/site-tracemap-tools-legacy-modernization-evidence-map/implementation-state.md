# Site TraceMap Tools Legacy Modernization Evidence Map Implementation State

Status: implemented
Readiness: ready-for-review
Last verified: 2026-06-20
Public claim level: concept

## Branch

Implementation branch: `codex/impl-site-legacy-modernization-evidence-map`
Base: `origin/dev`
Target PR base: `dev`
Worktree: dedicated implementation worktree; local path intentionally omitted to
satisfy the private absolute-path guardrail.

## Scope

This implementation adds a bounded public concept route for the TraceMap site:
`/legacy-modernization/evidence-map/`.

Owned files changed:

- `.kiro/specs/site-tracemap-tools-legacy-modernization-evidence-map/tasks.md`
- `.kiro/specs/site-tracemap-tools-legacy-modernization-evidence-map/implementation-state.md`
- `site/src/legacy-modernization/evidence-map/index.html`
- `site/src/_site/pages.json`
- `site/src/_site/discovery.json`
- `site/src/legacy-evidence/index.html`
- `site/src/legacy-validation/index.html`
- `site/src/roadmap/index.html`
- `site/scripts/legacy-modernization-evidence-map.mjs`
- `site/scripts/legacy-modernization-evidence-map.test.mjs`
- `site/scripts/validate.mjs`

Generated static output was built for validation but not edited by hand.

## Route Decision

Selected placement: standalone concept route
`/legacy-modernization/evidence-map/`.

Rationale:

- The standalone route matches the spec's recommended placement and keeps the
  page focused on reviewer questions for modernization planning.
- The route does not enter primary navigation. Discovery is through sitemap,
  route metadata, discovery metadata, and bounded links from adjacent concept
  pages.
- The page describes static repository evidence from repository snapshots and
  checked-in artifacts. It does not imply a shipped modernization assessment
  product, repository upload flow, runtime service, telemetry collector, AI/LLM
  workflow, or migration approval surface.

Rejected alternatives:

- Section on `/legacy-evidence/`: rejected because that route is the sibling
  legacy evidence story and claim ledger. This implementation needs a
  reviewer-question evidence map without superseding the story.
- Section on `/legacy-validation/`: rejected because that route describes the
  validation plan for messy legacy repositories. This implementation maps
  modernization planning questions to current public-safe evidence boundaries.
- Section on `/capabilities/`, `/limitations/`, `/validation/`, or
  `/manager-packet/`: rejected because those pages already own capability
  maturity, non-claims, validation status, and manager summary framing. The new
  route links to them instead of restating their roles.

The route uses `/roadmap/` and `/review-claim-checklist/` for claim-governance
context. No `/claims/` or `/claim-ledger/` route exists in this snapshot, so no
links to those candidate routes were added.

## Public Copy Boundary

The page includes visible `Public claim level: concept` and the shared site
principle `No public conclusion without evidence`.

The page explicitly states that TraceMap organizes deterministic static
repository evidence for modernization planning, not runtime behavior,
production telemetry, migration safety, release safety, operational safety,
database existence, package compatibility, AI/LLM analysis, or complete product
coverage.

The page does not publish raw facts, raw SQLite, analyzer details, raw source
snippets, raw SQL, config values, secrets, local paths, raw remotes, generated
scan directories, private sample names, connection strings, service addresses,
endpoint values, credentials, database contents, or hidden validation details.

## Evidence Map Rows

General static-evidence-model rows are labeled `concept`:

| Row | Category | Public status | Decision |
| --- | --- | --- | --- |
| Old frameworks and toolchains | General model | `concept` | Public evidence-model framing only. |
| Project load and build gaps | General model | `concept` | Reduced coverage, never clean-repo wording. |
| Syntax fallback | General model | `concept` | Useful weaker evidence, not semantic proof. |
| Config and project metadata | General model | `concept` | Static metadata framing only; no service binding, service-reference, endpoint, or connection-value extraction claim. |

Legacy-surface detection rows are labeled `hidden`:

| Row | Ledger result | Public status | Decision |
| --- | --- | --- | --- |
| WCF and service references | Sibling ledger hidden | `hidden` | Named hidden row only. |
| WCF metadata normalization | Sibling ledger hidden | `hidden` | Named hidden row only. |
| .NET Remoting | Sibling ledger hidden | `hidden` | Named hidden row only. |
| WebForms events, routes, and navigation | Event-flow theme hidden; route/navigation aspect unledgered | `hidden` | Named hidden row with route/navigation gap recorded. |
| Legacy data metadata | Sibling ledger hidden | `hidden` | Named hidden row only. |
| Build environment diagnostics detection | Sibling ledger hidden | `hidden` | Named hidden row only. |
| Flow composition reporting | Sibling ledger hidden | `hidden` | Named hidden row only. |
| ASMX and SOAP services | Absent from sibling ledger | `hidden` | Named hidden row; follow-up ledger gap recorded. |
| WinForms navigation and events | Absent from sibling ledger | `hidden` | Named hidden row; follow-up ledger gap recorded. |

No row uses `demo-backed`, `main`, `shipped`, `dev-only`, or `omitted`. No
specific demo rows were added because no row-specific checked-in public-safe
proof path was identified for this concept page.

## Sibling Ledger Reconciliation

The `site-tracemap-tools-legacy-evidence-story` theme claim ledger was
rechecked on the implementation base before row labels were assigned. It still
pins WCF/service-reference mapping, WCF metadata normalization, .NET Remoting
detection, WebForms event flow, legacy data metadata, build diagnostics, and
flow composition reporting as hidden pending validation.

The hidden `legacy-story-reconciliation` packet was cross-checked as an
internal coexistence reference. Its public claim level remains hidden, so it was
not used to promote any public site row.

Unledgered defaults recorded:

- ASMX/SOAP services default to `hidden` until a future sibling-ledger entry and
  public-safe proof exist.
- WinForms navigation/event surfaces default to `hidden` until a future
  sibling-ledger entry and public-safe proof exist.
- WebForms route/navigation surfaces beyond the narrower WebForms event-flow
  ledger entry default to `hidden` until a future ledger update adds public-safe
  proof.

## Cross-Links

Inbound links added:

- `/legacy-evidence/` links to the modernization evidence map from the hero and
  link grid.
- `/legacy-validation/` links to the modernization evidence map from the hero
  and link grid.
- `/roadmap/` adds a concept card, next-proof-upgrade note, and source-material
  link for the modernization evidence map.

The new page links only to public-safe existing routes verified by site
validation: `/legacy-evidence/`, `/legacy-validation/`, `/capabilities/`,
`/limitations/`, `/validation/`, `/proof-paths/`, `/manager-packet/`,
`/roadmap/`, `/review-claim-checklist/`, and `/adoption/`.

Demo result and proof-upgrade routes were not used as proof paths for evidence
map rows because this page has no demo-backed row.

## Validation

Commands run:

```text
git diff --check
git diff --cached --check
cd site && npm test
cd site && npm run validate
cd site && npm run build
./scripts/check-private-paths.sh
```

Results:

- `npm test`: passed, 197 tests before PR review; passed with 199 tests after
  review-thread fixes; passed with 200 tests after leak-error redaction.
- `npm run validate`: passed before PR review and again after review-thread
  fixes; built the site and validated 44 HTML files, 1349 internal references,
  43 sitemap URLs, 1 legacy story safety target, and 13 legacy modernization
  evidence-map rows.
- `npm run build`: passed before PR review and again after review-thread fixes.
  A parallel validate/build rerun caused a transient generated-output race;
  sequential `npm run validate` and `npm run build` both passed afterward.
- `git diff --check`: passed.
- `git diff --cached --check`: passed before staging and again on the staged diff.
- `./scripts/check-private-paths.sh`: passed with `Private path guard passed.`

Browser sanity:

- Started local preview on alternate port because the default preview port was
  already in use.
- Desktop check at 1280px: route, title, H1, top navigation, required anchors,
  table marker, 13 rows, 4 concept rows, 9 hidden rows, and no page-level
  horizontal overflow were confirmed.
- Mobile check at 390px: H1 visible, 11 nav links present, 13 rows present,
  first hidden row labeled `hidden`, proof-link grid present, no page-level
  horizontal overflow, and the wide evidence table stayed inside its horizontal
  scroll wrapper.
- Temporary preview server was stopped after the check.

## Review Findings

Implementation self-review finding:

- Initial site patch application landed in the root checkout instead of the
  dedicated implementation worktree. The exact site patch was moved into the
  dedicated worktree and reversed from the root checkout. Unrelated root
  checkout changes were left untouched.

Automated focused validation finding:

- The first run of `npm test` caught a title-check bug in the new validator. The
  validator used an impossible word-boundary match before the `<title>` tag.
  The guard was patched and the full site test suite passed afterward.

PR review-loop findings patched:

- Hardened local absolute path detection for lowercase Windows drive letters
  and forward-slash Windows paths.
- Made hidden-row slicing case-insensitive for closing table-row tags.
- Added tight tag stripping so sensitive tokens split across HTML tags are
  scanned.
- Adjusted tag stripping so unquoted apostrophes inside tag attributes do not
  hide later rendered text.
- Added `&apos;` decoding to the HTML entity normalization helper.
- Redacted matched leak evidence from validator error messages so sensitive
  values are not echoed into logs.

## Oddities

- The evidence map intentionally names public framework-family rows as hidden
  rows. This satisfies the spec requirement to name the family without turning
  hidden validation into public support language.
- The page does not use `omitted` because naming WCF, ASMX/SOAP, Remoting,
  WinForms, WebForms, and legacy data metadata does not itself leak private
  validation details in this public-safe framing.
- The mobile table uses the existing horizontal table wrapper. This preserves
  dense reviewer-oriented columns without causing page-level horizontal
  overflow.

## Follow-Ups

- Add sibling-ledger entries for ASMX/SOAP, WinForms navigation/event surfaces,
  and WebForms route/navigation aspects before any future public promotion.
- Upgrade any hidden row only after checked-in public-safe proof exists, with
  rule IDs, evidence tiers, coverage labels, limitations, and implementation
  state updated in the same change.
- Keep concept rows from smuggling hidden detection claims when future copy is
  edited.
