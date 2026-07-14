# TraceMap Positioning Story (Internal)

Status: internal-narrative
Audience: founder / product / go-to-market
Not public copy. This file preserves the positioning narrative from a skeptical
enterprise-buyer review. It is **not** a public claim surface: it must not be
published as-is, and any language reused on the `tracemap.tools` site must first
pass the site claim guardrails (rule IDs, evidence tiers, coverage labels,
limitations, claim-level labels, no runtime/AI/release-safety overclaim). It
describes positioning, not shipped capability; where it names a capability, the
source of truth is [README.md](../README.md), [rules/rule-catalog.yml](../rules/rule-catalog.yml),
and [docs/VALIDATION.md](VALIDATION.md).

---

## The one-sentence story

**TraceMap is the deterministic evidence layer for change review: when someone
has to decide whether a change is safe to ship, migrate, or approve, TraceMap
tells them exactly what the code *shows* it touches — with a rule ID, a
confidence tier, and an honest map of where the analysis stops — and it does it
reproducibly, offline, without an LLM guessing in the core.**

Everything else is a footnote to that sentence.

## Who it is actually for (today vs. tomorrow)

Be honest about the buyer, because the pitch changes with it.

- **Today's real buyer** is a hands-on staff engineer, architect, or
  modernization lead who will run a CLI in a pilot. They adopt tools by running
  them, not by reading a pricing page. The product they touch is the CLI plus
  the artifacts it writes (`facts.ndjson`, `index.sqlite`, `report.md`,
  `release-review.md`).
- **Tomorrow's economic buyer** is the engineering director / architecture
  review board (CAB) member / change-management owner who signs off on risky
  changes and owns the blast radius when a change goes wrong. They don't buy a
  scanner; they buy *a defensible answer they can put in a ticket.*

The bridge between them is the **evidence packet**: the thing the engineer
generates and the director trusts.

## The pain, told as a scene

A change is up for review — a renamed contract property, an endpoint reshape, a
schema or archive-link setup script. Someone in the room asks the only question
that matters: *"What does this touch?"*

What happens next is the pain:

- Someone hand-waves from memory.
- Someone runs find-usages, which dies on the half-broken legacy project.
- Someone pastes the diff into an LLM bot, which answers confidently and
  sometimes wrongly.
- The room ships anyway, and the regression — or the 2 a.m. archive job that ran
  against the wrong database — teaches them what the review missed.

The cost isn't the tool budget. It's the incident, the rollback, the migration
that stalls because nobody can prove what's safe to cut. **The pain is
"defensible answers to impact questions are expensive, slow, and vibes-based."**

## Why TraceMap is believable where others aren't

The differentiator is not "static analysis." It's the *posture*:

- **It refuses to conclude without evidence.** Every row carries a rule ID and
  an evidence tier (semantic / structural / syntax / unknown). No conclusion is
  dressed up beyond what produced it.
- **It labels its own coverage and gaps.** When it can't resolve DI, reflection,
  or dynamic dispatch, it emits an explicit `AnalysisGap` and marks coverage
  reduced — instead of a silent false negative that looks clean.
- **It is deterministic and reproducible.** Same input, same output. No
  hallucination, no model drift, no "it said something different yesterday."
- **It runs offline on private code.** Nothing leaves the machine; the core uses
  no LLM, embeddings, vector DB, or prompt classification. For a security-anxious
  enterprise that alone clears a procurement hurdle.

For a buyer who has been burned by three tools that oversold "impact analysis,"
**the honesty is the moat.** The moment that converts a skeptic is watching
TraceMap *decline to answer* — return `NeedsReview` or `NoEvidenceReducedCoverage`
instead of a false green — and realizing that's exactly why the green rows can be
trusted.

## The wedges, in priority order

1. **Front door — release / change-review evidence packets.** "Before this ships,
   here is what static evidence says it touches, each row tagged with rule ID,
   tier, coverage, and the gaps it can't resolve, as a Markdown packet for the PR
   or CAB ticket." Most legible pain, most demoable, built on shipped
   `reduce` / `contract-diff` / `release-review`.
2. **The moat — legacy .NET modernization risk discovery.** WebForms / WinForms /
   WCF / ASMX / Remoting static evidence with honest gaps. Unglamorous, hard,
   exactly where LLM tools faceplant, and where thirty years of scar tissue is an
   unfair advantage. Slower cycle, defensible revenue, natural consulting on-ramp.
3. **The sleeper — deterministic grounding for AI review.** Every team is bolting
   LLM PR-bots onto the pipeline and getting confident, hallucinated impact
   claims. TraceMap is the deterministic evidence feed that grounds them: the
   agent must cite rule-backed facts instead of inventing them. This turns the
   biggest apparent threat into a distribution channel — and it keeps the LLM
   *outside* the core, so it never violates the determinism principle.
4. **Credibility spike, not a wedge — SQL / archive preflight.** The PostgreSQL
   operator-runbook evidence is a "look how careful we are" proof point. Keep it
   as a trust exhibit; do not put it on the front door.

If forced to pick one to lead with: **release-review packet as the front door,
legacy .NET as the moat, AI-grounding as the expansion bet.**

## The trap to avoid (why the current story under-sells)

TraceMap's discipline is currently its *headline* instead of its *footer*. The
public surface talks at length about its own epistemology — claim ledgers, proof
paths, non-claims, wording guides — and almost never about the buyer's outcome.
Repeated end to end, the non-claims start to read as "this tool can't conclude
anything," which is the opposite of the truth.

The fix is one move, not a rewrite: **lead with the buyer's pain and one real
piece of evidence; move the discipline to the footer.** The refusal-to-overclaim
should be the *reason to trust the confident row*, not the whole pitch. Confident
outcome on top, honest limits underneath — in that order.

## Open source vs. paid (open-core)

- **Stay fully open (Apache-2.0): the entire core** — scanner, reducer, all
  language adapters, rule catalog, CLI, output formats. The thesis is
  auditability and no lock-in; closing any of it kills the differentiator. The
  open core is the marketing.
- **Commercial, in priority order:** (1) fleet-scale orchestration — "combine
  200 repos, keep evidence history, diff packets over time"; (2) CI/PR/CAB
  integration and policy gates with SSO, retention, audit; (3) legacy-.NET
  modernization assessments (tool-backed consulting, highest margin, hardest to
  copy); (4) a grounding feed licensed to AI-review vendors.
- **Never paywall** the rule catalog or determinism — those are the credibility,
  not the product.

## The founder thread

The thing that makes TraceMap hard to sell is the same thing that makes it worth
selling. Thirty years of watching "impact analysis" mean "regex with a
dashboard" is why this tool refuses to lie — and that refusal is the entire
wedge with buyers who have the same scars. The narrative job is not to hide the
caution. It is to put a confident, outcome-shaped claim *on top of* the caution,
so the buyer feels the relief of "finally, a tool that tells me what it doesn't
know" instead of the fatigue of "another tool that won't commit to anything."

## What this story authorizes next (not done here)

- A rewritten homepage hero that leads with pain + one real evidence row
  (guarded copy, claim-leveled).
- A "deterministic grounding for AI review" concept page.
- One real, sanitized, end-to-end reference packet from a public OSS repo to
  replace meta-copy with concrete proof.
- Optionally, a `site-`prefixed Kiro spec that turns this narrative into
  guardrail-passing public copy.

Anchors for anyone extending this: [README.md](../README.md),
[docs/ADAPTER_RUNWAY.md](ADAPTER_RUNWAY.md), [docs/PRD.md](PRD.md),
[rules/rule-catalog.yml](../rules/rule-catalog.yml), and the site claim
guardrails at [site/scripts/site-claim-guardrails.mjs](../site/scripts/site-claim-guardrails.mjs).
