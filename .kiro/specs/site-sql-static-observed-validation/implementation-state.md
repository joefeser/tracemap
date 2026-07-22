# Site SQL Static/Observed Validation State

Branch: `codex/site-sql-static-observed-validation`

Scope: issue #522. Public route, discovery metadata, focused validator, tests,
and adjacent links only. No harness, ingestion, or database capability changes.

Validation:

- Focused route/safety tests: 4/4 passed.
- Full site tests: 703/703 passed.
- Site build and validation: passed; 94 HTML files, 3,249 internal references,
  and 93 sitemap URLs validated.
- Desktop 1440x900 and mobile 390x844 browser checks: passed with no
  horizontal overflow and no browser warnings/errors.
- Private-path guard and `git diff --check`: passed.

PR state: [#523](https://github.com/joefeser/tracemap/pull/523) is open. ACK
stopped before GitHub review processing with
`environment_blocked / LOCAL_BUILD_STALE`; no merge authority was inferred.

Review follow-up: current Qodo findings were addressed with quote-aware
tag-collapsing for forbidden-text checks, formatting-tolerant inbound-link
validation, regression tests for both cases, and synchronized PR/ACK task
wording.

Deferred: source/archive operator templates remain in #521; disposable
PostgreSQL validation remains in #519; production observations and the
PostgreSQL schema/migration adapter remain separate.
