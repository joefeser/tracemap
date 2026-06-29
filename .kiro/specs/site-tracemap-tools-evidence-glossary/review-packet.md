# TraceMap Kiro Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

Review the `site-tracemap-tools-evidence-glossary` spec for spec-review
findings first. This is a spec-only site phase; it should not implement site
code.

## Review Orientation

Branch: `codex/spec-site-evidence-glossary`
Base: `origin/dev`
Target PR base: `dev`

Local review artifacts are not committed and should be saved under
`.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/`.

Remaining open questions after review: none. Medium or higher findings from
available Kiro reviews were patched or dispositioned in
`implementation-state.md`.

## Scope

The future page would create a public-safe evidence glossary/reference page,
likely `/glossary/` or `/docs/evidence-glossary/`, that explains TraceMap
vocabulary for engineers, reviewers, managers, architects, and agents before
they repeat public claims.

The glossary is vocabulary and claim-boundary guidance only. It does not
implement scanner, reducer, adapter, or site code in this phase. It does not
make raw facts, raw SQLite indexes, analyzer logs, raw source snippets, raw
SQL, config values, secrets, local paths, raw remotes, generated scan
directories, private sample names, or hidden validation details public.

Please inspect:

- `.kiro/specs/site-tracemap-tools-evidence-glossary/requirements.md`
- `.kiro/specs/site-tracemap-tools-evidence-glossary/design.md`
- `.kiro/specs/site-tracemap-tools-evidence-glossary/tasks.md`
- `.kiro/specs/site-tracemap-tools-evidence-glossary/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-evidence-glossary/review-packet.md`

Review focus:

- Are `Status: not-started`, `Readiness`, and
  `Public claim level: concept` present and consistent?
- Does the spec remain spec-only without implementing site code?
- Are future implementation tasks unchecked?
- Does the spec require an explicit route or placement decision, including
  `/glossary/`, `/docs/evidence-glossary/`, and folded-placement alternatives?
- Does it require the visible public claim level and
  `No public conclusion without evidence`?
- Does it define the required vocabulary: rule ID, evidence tier, proof path,
  coverage label, limitation, analysis gap, commit/source context, extractor
  version, supporting IDs, public claim level, and local-only artifact family?
- Do definitions include limitations and avoid implying terms are fully shipped
  everywhere?
- Does it forbid AI/LLM impact-analysis claims, runtime behavior claims,
  production traffic claims, endpoint performance claims, outage cause claims,
  release safety claims, operational safety claims, and complete coverage
  claims?
- Does it forbid raw facts, raw SQLite, analyzer logs, raw source snippets,
  raw SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, and hidden validation details?
- Does it require public-safe links and link resolution to existing routes
  without overclaiming concept or demo material?
- Does it require metadata, discovery, sitemap, validation, `npm test`,
  `npm run validate`, `npm run build`, `git diff --check`,
  `./scripts/check-private-paths.sh`, and browser sanity when route/layout
  changes are made?
- Is `implementation-state.md` sufficient for a future agent to resume without
  guessing?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
