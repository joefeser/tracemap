# Implementation State

Status: implemented
Public claim level: demo

Branch: `codex/site-swift-api-client-article`

Scope:

- Publish `/blog/how-tracemap-reads-swift-api-clients/` as a public site article.
- Keep the story demo-level even though Swift v0 is shipped, because the article explains checked-in public evidence and claim-reading behavior rather than runtime proof.
- Reuse the existing blog generation path so sitemap and discovery metadata are produced by `npm run build`.
- Add `swift-api-client-article` validation that checks required blocks, required Swift evidence-shape language, required next links, private-material boundaries, and unsupported runtime/AI claims.

Claim boundary:

- Safe: TraceMap surfaces static Swift API-client candidates and attaches rule ID, evidence tier, coverage label, limitation, and non-claim context.
- Safe: The evidence can support review, migration planning, endpoint inventory, and change-risk conversations.
- Not safe: endpoint reachability, backend compatibility, request success, auth-flow correctness, production traffic, API correctness, runtime tracing, production behavior, complete Swift semantic analysis, or AI impact analysis.

Validation:

- `npm test`
- `npm run validate`
- `npm run build`
- `git diff --check`
- `./scripts/check-private-paths.sh`
- Desktop browser sanity: `site/output/playwright/swift-api-client-article-desktop.png`
- Mobile browser sanity: `site/output/playwright/swift-api-client-article-mobile.png`

Follow-up:

- None known.
