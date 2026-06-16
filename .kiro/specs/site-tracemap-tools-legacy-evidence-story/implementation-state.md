# site-tracemap-tools-legacy-evidence-story implementation state

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Branch

Implementation branch: `codex/site-legacy-evidence-story`

Base: `origin/main`

## Scope

This implementation adds a bounded public legacy evidence story to
`tracemap.tools` without changing scanner, reducer, adapter, or core extractor
code.

Owned files changed:

- `site/src/legacy-evidence/index.html`
- `site/src/_site/pages.json`
- `site/src/_site/discovery.json`
- `site/src/legacy-validation/index.html`
- `site/src/roadmap/index.html`
- `site/src/docs/index.html`
- `site/src/proof-paths/index.html`
- `site/scripts/legacy-story-safety.mjs`
- `site/scripts/legacy-story-safety.test.mjs`
- `site/scripts/validate.mjs`

Generated `site/dist/` and `site/output/` were not edited by hand.

## Route Decision

First implementation surface: standalone concept route `/legacy-evidence/`.

Rendered safety target: `site/dist/legacy-evidence/index.html`.

Top navigation was not changed. Discovery uses sitemap metadata plus bounded
backlinks from existing concept/proof surfaces:

- `/legacy-validation/`
- `/roadmap/`
- `/docs/`
- `/proof-paths/`

This keeps the page discoverable without mutating every top-nav expectation.

## Promotion Check

Checked current `origin/main` state before writing public copy. Some legacy
specs, samples, and validation-adjacent artifacts exist, but this site phase did
not find public-safe checked-in proof sufficient to upgrade the public claim
level for any referenced legacy theme.

Per-theme result:

| Theme | Public label used | Promotion result |
| --- | --- | --- |
| WCF/service-reference mapping | hidden pending validation | No public-safe proof recorded for demo wording in this phase. |
| WCF metadata normalization | hidden pending validation | No public-safe proof recorded for demo wording in this phase. |
| .NET Remoting detection | hidden pending validation | Existing public sample/spec material does not by itself upgrade the public site claim. |
| WebForms event flow | hidden pending validation | No public-safe proof recorded for demo wording in this phase. |
| Legacy data metadata | hidden pending validation | No public-safe proof recorded for demo wording in this phase. |
| Build diagnostics | hidden pending validation | No public-safe proof recorded for demo wording in this phase. |
| Flow composition reporting | hidden pending validation | No public-safe proof recorded for demo wording in this phase. |

`Concept` applies only to the page/story shape. It does not upgrade hidden
capability support.

## Public Copy Boundary

The rendered route focuses on:

- evidence model vocabulary;
- current hidden claim ledger;
- promotion gate;
- public-safe artifact boundary;
- non-claims and publication safety.

The page avoids claims about shipped legacy support, runtime behavior, UI
reachability, production traffic, deployment state, endpoint performance,
exploitability, database existence, package compatibility, incident cause,
release approval, release safety, AI impact analysis, LLM calls, embeddings,
vector databases, and prompt-based classification.

## Content-Safety Guard

Implemented `site/scripts/legacy-story-safety.mjs`.

Wiring:

- `npm run validate` calls `buildSite()` first.
- The legacy story guard then scans the freshly built
  `legacy-evidence/index.html` target.
- Structural validation runs afterward through the existing `validateDist()`.

The guard is deterministic string/regular-expression validation over rendered
HTML/text. It normalizes case-insensitive patterns, ordinary whitespace, and
Unicode format characters for hard leak detection.

Fixture coverage in `site/scripts/legacy-story-safety.test.mjs` includes:

- hard leak examples;
- local paths;
- bare/internal `.kiro/specs/...` paths;
- connection strings;
- credential assignment;
- private/local URLs;
- raw repository remotes;
- affirmative overclaims;
- sanctioned negated disclaimers;
- empty output;
- missing target output;
- hidden-theme enumeration without adjacent hidden/omission labels;
- legitimate artifact-name documentation;
- clean concept copy;
- boundary legacy terms;
- boundary legacy terms adjacent to forbidden content;
- source/spec/fixture files excluded from scan scope;
- build ordering proving stale clean `dist` is replaced by fresh built output.

Documented limitation: this guard prevents deterministic rendered-content leaks
and obvious overclaims. It is not semantic proof that every public claim is safe.
Manual review is still required for sensitive values that do not match the
known patterns.

## Validation

Commands run:

```text
git diff --check
cd site && npm test
cd site && npm run validate
cd site && npm run build
./scripts/check-private-paths.sh
```

Results:

- `git diff --check`: passed.
- `npm test`: passed, 50 tests.
- `npm run validate`: passed; built the site and validated 30 HTML files, 769
  internal references, 29 sitemap URLs, and 1 legacy story safety target.
- `npm run build`: passed.
- `./scripts/check-private-paths.sh`: passed.

Browser sanity:

- Started local preview on `http://localhost:4174` because port `4173` was in
  use.
- Desktop check at 1280px: title, H1, nav, concept label, hidden labels, and
  backlinks were present; no horizontal overflow.
- Mobile check at 390px: H1 visible, 11 nav links present, 7 theme cards
  present, no horizontal overflow.
- Temporary preview server was stopped after the check.

## Follow-Ups

- Upgrade a specific theme from `hidden` only after checked-in public-safe proof
  exists on `main`, with artifact paths, supporting rule IDs, and validation
  commands recorded here.
- Keep the guard's approved disclaimer list small; prefer changing page copy
  over widening exceptions when the guard catches a phrase.
- Consider adding public-safe sample summaries later if a legacy evidence pack
  lands and passes redaction review.
