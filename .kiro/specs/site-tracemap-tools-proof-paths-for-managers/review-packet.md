# Site TraceMap Tools Proof Paths For Managers Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

## Review Objective

Review this implemented packet for the `tracemap.tools` manager-facing
proof-path page. The surface explains proof paths in decision terms: what
question a manager or reviewer can ask, what evidence packet exists, what
deterministic static evidence supports, what it does not prove, what the
coverage label means, and who owns the next runtime, product, release,
ownership, security, or review judgment.

This packet's implementation scope is static-site source, metadata, and
validation only. It intentionally changes no scanner code, reducer code,
generated scan outputs, runtime behavior, release workflow, or
management-decision automation.

Shared principle: No public conclusion without evidence.

## Files In Scope

- `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/requirements.md`
- `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/design.md`
- `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/tasks.md`
- `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-proof-paths-for-managers/review-packet.md`

## Reviewer Questions

1. Does the packet make clear that the implemented surface is concept-level and
   must visibly say `Public claim level: concept`?
2. Does it require visible `No public conclusion without evidence`?
3. Does it keep implementation scope limited to static-site source, metadata,
   and validation while avoiding scanner code, reducer code, generated scan
   output, runtime behavior, release workflow, or automated management
   decisions?
4. Does it record the final placement decision for
   `/proof-paths/for-managers/` and the rejected alternatives?
5. Does it distinguish the implemented surface from `/manager-brief/`,
   `/manager-faq/`, `/manager-packet/`, `/packets/`,
   `/packets/assembly/`, `/proof-paths/`, `/proof-paths/faq/`, and
   `/proof-paths/tour/`?
6. Does it require the manager question matrix fields: manager or reviewer
   question, evidence packet to inspect, what static evidence can support,
   what it does not prove, coverage-label consequence, stop condition, next
   owner, and supporting public route?
7. Does the matrix cover code-path changes, repeated claims, reduced or
   partial coverage, runtime or product behavior owner routing, release
   decisions, production traffic or endpoint performance or outage cause,
   public sharing, and missing/private-only/syntax-only/unknown evidence?
8. Does proof path anatomy preserve claim, public claim level, proof path,
   rule ID or rule family, evidence tier, coverage label, commit or public
   source context, extractor version or schema family, public-safe file path
   and line span, snippet hash or summary, artifact family, limitation,
   non-claim, validation evidence, unresolved gaps, and next owner?
9. Are coverage labels treated as boundaries rather than quality judgments or
   management decisions?
10. Are next-owner categories public role categories rather than private
    people, private team names, or organizational authority assignments?
11. Are forbidden runtime, production, endpoint-performance, outage-cause,
    release-safety, operational-safety, complete-coverage, AI/LLM,
    embedding, vector database, prompt-classification, autonomous-approval,
    automated-management-decision, release-approval, replacement, and
    absence-of-impact claims blocked clearly enough?
12. Does it prohibit raw source snippets, raw SQL, config values, secrets,
    local absolute paths, raw repository remotes, generated scan directories,
    raw facts streams, raw SQLite content, combined SQLite files, analyzer
    logs, private sample names, private owner names, raw command output,
    hidden validation details, and credential-like values?
13. Are metadata, discovery, sitemap, anchor, and adjacent-link expectations
    specific enough for either a standalone route or host-page section?
14. Are future validation expectations specific enough for visible copy,
    matrix rows, proof path anatomy, owner routing, forbidden wording,
    private/raw material, metadata, link resolution, aggregate site
    validation, and desktop/mobile sanity checks?

## Required Review Commands

Run if available:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-paths-for-managers --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-paths-for-managers --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model or tool is unavailable, record the exact command and error in
`implementation-state.md`.

## Review Status

Initial Opus and Sonnet spec reviews ran through the wrapper and saved review
text. Both reported reduced coverage because Kiro's internal shell/write tools
were denied in non-interactive mode. Medium or higher findings were patched.
One bounded re-review pass with both requested models ran with the same
reduced-coverage note. The re-review Medium finding was patched and recorded
in `implementation-state.md`; no unpatched Medium or higher findings remain.
