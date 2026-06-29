# Site TraceMap Tools Manager Demo Script Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

## Review Objective

Review this spec-only packet for a future `tracemap.tools` manager/teammate
demo script. The script should help Joe show the public site without
overclaiming runtime behavior, production completeness, release safety,
incident diagnosis, endpoint performance, operational safety, or AI analysis.

This packet intentionally changes no site source or scanner code.

## Files In Scope

- `.kiro/specs/site-tracemap-tools-manager-demo-script/requirements.md`
- `.kiro/specs/site-tracemap-tools-manager-demo-script/design.md`
- `.kiro/specs/site-tracemap-tools-manager-demo-script/tasks.md`
- `.kiro/specs/site-tracemap-tools-manager-demo-script/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-manager-demo-script/review-packet.md`

## Reviewer Questions

1. Does the spec make clear that the future page or section is concept-level
   and must visibly say `Public claim level: concept`?
2. Does it require visible `No public conclusion without evidence`?
3. Does it include all required script blocks: opening context, 2-minute tour,
   5-minute proof walkthrough, manager questions and safe answer shapes,
   engineer questions and proof routes, stop conditions, follow-up handoff,
   and non-claims?
4. Does the preferred route `/demo/manager-script/` have a clear rationale, and
   are rejected alternatives documented?
5. Does the spec distinguish this script from manager brief, manager FAQ,
   manager packet, demo runbook, questions, use cases, capabilities, and blog
   pages?
6. Does the required route sequence use only public pages and require
   implementation-time verification before linking?
7. Are forbidden claims and private/raw material boundaries explicit enough for
   future validation?
8. Are future validation expectations specific enough for required copy,
   required links, metadata, discovery metadata, sitemap metadata if
   standalone, forbidden claims, private/raw material, word count bounds, and
   desktop/mobile browser sanity?

## Required Review Commands

Run if available:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-demo-script --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-demo-script --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model or tool is unavailable, record the exact command and error in
`implementation-state.md`.

## Review Status

Initial reviews ran with `claude-opus-4.8` and `claude-sonnet-4.6`. Both
completed with reduced coverage because Kiro reported denied tool access, but
both produced review artifacts under
`.tmp/kiro-reviews/site-tracemap-tools-manager-demo-script/`.

Medium findings patched:

- Raised the visible-copy word-count validation bound from 900-1,600 to
  900-2,400 words so the required script blocks can fit without pressure to
  omit content.
- Expanded forbidden-claim validation scope to rendered HTML, metadata,
  discovery output, sitemap output, tests, fixtures, and generated pages.

Sonnet re-review completed with full coverage and no High or Medium findings.
Opus re-review completed with reduced coverage because Kiro reported denied
tool access, and it reported no High or Medium findings.

Low clarifications were patched after re-review: route verification state is
recorded, the spec requires implementation to verify visible evidence fields
before the script references them, the design placement table now matches the
requirements rejection list, 2-minute tour links use the same verification as
the full route sequence, and inbound-link sources must be verified before
linking.

All Medium+ findings are patched or dispositioned. Local spec-only validation
passed, and readiness is `ready-for-implementation`.
