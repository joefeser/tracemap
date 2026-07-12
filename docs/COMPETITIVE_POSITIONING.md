# TraceMap Competitive Positioning (Internal)

Status: internal-narrative
Audience: founder / product / go-to-market
Not public copy. This file preserves a competitive-positioning memo. It is **not**
a public claim surface: do not publish as-is, and any competitor comparison used
on `tracemap.tools` must be independently fact-checked and kept within the site
claim guardrails. The TraceMap side is verified against the repo; the competitor
side is from general knowledge as of authoring and is **fact-checkable** — see the
flags at the end. Companion to [POSITIONING_STORY.md](POSITIONING_STORY.md).

---

## The frame: TraceMap isn't in any of their categories

Plot the space on two axes:

- **What question it answers:** *"where is this / show me the code"* (search &
  navigation) → *"what does changing this touch, and how sure are we"* (impact
  reduction).
- **How it answers:** *probabilistic* (LLM / embeddings, non-reproducible,
  uncited) → *deterministic* (rule-backed, reproducible, cited).

Competitors cluster in three corners. The **deterministic + impact-reduction**
corner is nearly empty, and that is where TraceMap sits. That empty quadrant is
the category to own: *deterministic change-impact evidence, with provenance, for
review.* Not search. Not refactor. Not SAST. Not a PR bot.

## The field

| Tool | What it's really for | Overlaps TraceMap on | What it does that TraceMap doesn't | What TraceMap does that it doesn't | Wedge line |
|---|---|---|---|---|---|
| **Code RAG / "chat with your repo"** (Cody, Cursor retrieval, Glean-style) | Answer NL questions over code via embeddings + LLM | "what touches X?" questions | Fluent NL, any-language, instant | Reproducible, cited, gap-honest answers with rule IDs | "Their answer is a plausible paragraph. Yours is a fact with a line number." |
| **Qodo (Codium) / PR-Agent** | LLM review of a PR diff | Change-impact commentary at review time | Inline suggestions, test-gen, natural prose | Deterministic evidence the bot can't hallucinate | "It's the grounding under their bot, not a rival to it." |
| **Sourcegraph** (search, Cody, Batch Changes) | Code search & navigation at scale | Cross-repo find-usages | Fast universal search, mass edits, huge-scale indexing | A provenance-tagged impact packet with evidence tiers + coverage labels | "Search finds the string. TraceMap tells you the blast radius, with confidence per row." |
| **OpenRewrite / Moderne** | Recipe-based automated refactoring across repos | Understanding what a change reaches | Actually applies the change, mass-scale | Assesses impact/risk before anyone changes anything; honest gaps | "They swing the hammer. You tell people where the walls are first." |
| **CodeQL** (+ Semgrep) | Semantic/pattern queries, mostly security & dataflow | Deterministic, semantic, provenance | Deep taint/dataflow, large security rule ecosystem, CI-native | Contract-delta review packets with tiers + reduced-coverage labeling, legacy-.NET honesty | "Same rigor, different question: they hunt vulnerabilities, you answer 'what does this change break.'" |
| **Snyk / Dependabot** | Dependency vuln/upgrade alerts | Package-upgrade impact surface | CVE feeds, auto-PRs | Static evidence of what your code actually touches per package, with gaps | "They tell you a CVE exists; you tell you if your code reaches it." |

## Where TraceMap is genuinely differentiated (moat, not just difference)

1. **Determinism as a feature.** Same input → identical output, no model, no
   drift, offline, nothing leaves the machine. No probabilistic tool can say
   this, and it clears a security-procurement bar the LLM tools trip on.
2. **Provenance + honest gaps on every row.** Rule ID, evidence tier, coverage
   label, commit SHA, explicit `AnalysisGap`. The refusal to silently guess is
   what the LLM tools don't do and the search/refactor tools don't bother to.
3. **Legacy .NET depth with honest boundaries** (WebForms/WCF/ASMX/Remoting).
   Where RAG hallucinates, CodeQL isn't aimed, and OpenRewrite doesn't reach.
4. **It grounds the LLM tools instead of fighting them.** The Qodo/RAG wave
   becomes distribution, not competition (see the site `/grounding/` page).

## Where competitors win outright (say it plainly)

- **Breadth & polish:** Sourcegraph/CodeQL index at a scale and language breadth
  TraceMap doesn't touch yet.
- **Ergonomics:** RAG/Qodo answer in fluent English inside the IDE/PR now;
  TraceMap makes you read an artifact.
- **Ecosystem:** CodeQL/Semgrep have thousands of community rules; TraceMap has
  one catalog.
- **Distribution:** Dependabot/Qodo are one-click in the GitHub UI; TraceMap is a
  CLI + artifacts. (CI/PR integration is the product move that closes this.)
- **Automated action:** OpenRewrite/Moderne actually fix things; TraceMap only
  tells you. A deliberate boundary, but a buyer wanting fixes will feel it.

## The one positioning sentence

> TraceMap is the deterministic evidence layer for change review — the
> reproducible, cited "what does this touch" that search tools don't answer,
> refactor tools skip, and AI reviewers guess at. It doesn't compete with your
> PR bot; it's the ground truth underneath it.

## Fact-check flags (verify before external use)

- Qodo/PR-Agent, Sourcegraph Cody, and Cursor feature sets move fast — confirm
  current capabilities, especially any deterministic/grounding features added.
- Moderne's impact-analysis / "data tables" features increasingly overlap the
  "assess before refactor" claim — check how close they've gotten.
- CodeQL/Semgrep positioning is security-first today; verify neither has pushed
  into change-impact-for-review.
- Glean/Backstage-style catalogs are a possible adjacent competitor for "what
  depends on what" if TraceMap expands toward portfolio/ownership.
