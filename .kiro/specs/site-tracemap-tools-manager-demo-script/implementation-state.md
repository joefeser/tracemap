# Site TraceMap Tools Manager Demo Script Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Current Scope

Spec-only packet for a future public-site manager/teammate demo script. This
branch creates only files under
`.kiro/specs/site-tracemap-tools-manager-demo-script/`.

No site source, scripts, generated output, scanner code, reducer code, or
existing specs are intentionally changed in this phase.

## Branch And Base

- Branch: `codex/spec-site-manager-demo-script`.
- Base: `origin/main`.
- PR target: `main`.
- Implementation status: not started.

## Placement State

Preferred future placement: `/demo/manager-script/`.

Rejected alternatives recorded in the spec:

- `/demo/briefing/`.
- Section on `/demo/runbook/`.
- Section on `/manager-brief/`.
- Replacing or merging with `/manager-packet/`.

Future implementation must re-check current site information architecture and
record the final placement decision before editing site files.

## Route Verification State

Spec-time review reported that the ten intended route stops currently exist in
`site/src`, but future implementation must still verify generated site output
before linking. The implementation-time verification set is `/`,
`/capabilities/`, `/proof-paths/`, `/proof-source-catalog/`, `/demo/result/`,
`/demo/runbook/`, `/questions/`, `/limitations/`, `/validation/`, and
`/static-vs-runtime/`.

Before referencing a named evidence field on a route, future implementation
must also verify that the field is visibly rendered on the target page or
soften the script wording to `where present`.

## Scope Decisions

- Keep this as `Public claim level: concept` because it is a presentation guide
  over existing public pages, not new demo evidence.
- Require visible `No public conclusion without evidence`.
- Treat the route list as intended public stops that must be verified during
  implementation before linking.
- Distinguish the script from manager brief, manager FAQ, manager packet, demo
  runbook, questions, use cases, capabilities, and blog pages.
- Preserve strict non-claim boundaries for runtime, production, release,
  incident, operational, endpoint-performance, completeness, and AI/LLM
  language.
- Keep raw facts, SQLite, logs, source, SQL, config, secrets, local paths,
  remotes, generated scan directories, private names, and hidden validation
  details out of future public output.

## Review Log

Initial `claude-opus-4.8` review command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-demo-script --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

Result: completed with reduced coverage because Kiro reported denied tool
access. Artifacts:

- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T023545-886Z-spec-claude-opus-4.8.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T023545-886Z-spec-claude-opus-4.8.meta.json`

Finding patched: Medium finding that the 900-1,600 word count bound likely
conflicted with the required script block volume. Resolution: raised the future
visible-copy validation bound to 900-2,400 words.

Low findings addressed while patching: clarified evidence fields are required
where visibly present, named rendered page label and site metadata as the
claim-level source of truth for links, scoped forbidden word checks to authored
conclusions and non-claim sections, and added an inbound-link expectation with
a recorded direct-navigation escape hatch.

Initial `claude-sonnet-4.6` review command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-demo-script --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Result: completed with reduced coverage because Kiro reported denied tool
access. Artifacts:

- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T023830-327Z-spec-claude-sonnet-4.6.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T023830-327Z-spec-claude-sonnet-4.6.meta.json`

Finding patched: Medium finding that forbidden-claim validation did not
explicitly cover metadata, discovery output, sitemap output, tests, fixtures,
and generated pages. Resolution: expanded the validation scope in
`requirements.md` and `design.md`.

Sonnet re-review command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-demo-script --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Result: full coverage with no High or Medium findings. Artifacts:

- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T024132-703Z-re-review-claude-sonnet-4.6.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T024132-703Z-re-review-claude-sonnet-4.6.meta.json`

Low findings addressed after re-review: recorded route verification state and
added an explicit requirement to verify visible evidence fields before the
script references them.

Opus re-review command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-demo-script --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

Result: completed with reduced coverage because Kiro reported denied tool
access. Artifacts:

- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T024329-366Z-re-review-claude-opus-4.8.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/2026-06-21T024329-366Z-re-review-claude-opus-4.8.meta.json`

No High or Medium findings were reported. Low findings patched: aligned the
design placement table with the requirements and implementation-state
rejection list, clarified that the 2-minute tour links use the same
generated-output verification as the full route sequence, and required chosen
inbound-link sources to be verified before adding links.

All Medium+ review findings are patched or dispositioned. Readiness is
`ready-for-implementation` after local spec-only validation completed.

## Validation Log

Completed:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Focused spec text checks for required files, labels, route references,
  script block names, forbidden unsupported claims, raw/private material
  boundaries, word-count bound, and readiness state: passed.

## Future Implementation Validation

The future site implementation should run and record:

- `git diff --check`.
- `./scripts/check-private-paths.sh`.
- `npm test` from `site/`.
- `npm run validate` from `site/`.
- `npm run build` from `site/`.
- Desktop browser sanity check.
- Mobile browser sanity check.

## Follow-Up Items

- Future implementation must decide whether the script is a standalone route
  or a section, then update metadata/discovery/sitemap only when applicable.
- Future implementation must verify route availability before linking.
- Future implementation must confirm required evidence fields are visibly
  present on target routes before telling presenters to point at those fields.
- Future implementation must keep the route concept-level unless a separate
  evidence-backed claim-level upgrade is recorded.
