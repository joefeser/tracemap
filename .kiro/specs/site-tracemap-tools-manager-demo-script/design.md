# Site TraceMap Tools Manager Demo Script Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Purpose

This design defines a future public-site script for a bounded live
conversation. It is not site implementation. It does not add scanner behavior,
reducer behavior, generated artifacts, public runtime claims, or AI/LLM
analysis.

The future surface should help Joe show `tracemap.tools` to a friend,
teammate, or manager in a way that is concise, useful, and hard to misrepeat as
a stronger claim than the public evidence supports.

## Recommended Placement

Use `/demo/manager-script/` unless implementation-time route review finds a
better fit.

Candidate placements:

| Placement | Decision | Reason |
| --- | --- | --- |
| `/demo/manager-script/` | Preferred | Names the audience and the format while staying under demo-oriented surfaces. |
| `/demo/briefing/` | Rejected | Sounds like a summary page, not a bounded script with stop conditions. |
| Section on `/demo/runbook/` | Rejected | The runbook is an operator checklist; this script is a presenter aid. |
| Section on `/manager-brief/` | Rejected | The manager brief is framing; this script choreographs a live route sequence. |
| Replacing or merging with `/manager-packet/` | Rejected | The packet explains manager value; this script gives a time-boxed route and answer guide. |

Future implementation must record the final placement and rejected
alternatives in `implementation-state.md`.

## Page Model

Recommended sections:

1. Opening context: state `Public claim level: concept`, `No public conclusion
   without evidence`, audience, purpose, and claim boundary.
2. 2-minute tour: give a short route order and three or four safe talking
   points.
3. 5-minute proof walkthrough: follow one public claim from site framing to
   proof path, proof-source catalog, demo result, limitations, and validation.
4. Manager questions and safe answer shapes: answer likely manager questions
   with bounded verbs and links.
5. Engineer questions and proof routes: route technical questions to public
   evidence surfaces without exposing local artifacts.
6. Stop conditions: list moments where the presenter must stop instead of
   escalating the claim.
7. Follow-up handoff: provide a public-link bundle shape and reminder language.
8. Non-claims: keep runtime, production, release, incident, AI/LLM, and
   completeness boundaries visible.

The page may use existing long-form static site patterns such as a hero,
compact callout, route list, question cards, and boundary section. It should
avoid decorative or marketing-heavy treatment; this is a working script, not a
landing page.

## Required Route Sequence

The script should present the following sequence after implementation verifies
each public route resolves:

1. `/`: orient to TraceMap's deterministic static evidence model.
2. `/capabilities/`: show what TraceMap can inspect and where boundaries begin.
3. `/proof-paths/`: show that public claims should have proof routes.
4. `/proof-source-catalog/`: show where public proof points back to source
   families or evidence surfaces without exposing raw artifacts.
5. `/demo/result/`: show a public demo result shape and visible limitations.
6. `/demo/runbook/`: show the operator checklist and sharing boundary.
7. `/questions/`: show how stakeholder questions route to evidence surfaces.
8. `/limitations/`: show explicit non-claims.
9. `/validation/`: show what public-site validation checks.
10. `/static-vs-runtime/`: close by distinguishing static evidence from
    runtime telemetry.

If any route is missing, the future implementation must block, substitute the
current equivalent, or remove that stop and record the decision.

## Script Content Shape

Opening context should sound like:

- TraceMap is deterministic static evidence, not runtime monitoring.
- This walkthrough shows how a claim stays attached to proof or stops.
- The goal is faster orientation, fewer vague review conversations,
  evidence-backed handoff, and clearer limitations.

The 2-minute tour should:

- Open `/`, `/capabilities/`, `/demo/result/`, `/proof-paths/`, and
  `/limitations/`.
- Use only links that pass the same generated-output verification required for
  the full route sequence.
- Use short statements that can be repeated safely.
- Stop before raw artifact or production questions.

The 5-minute proof walkthrough should:

- Open the full required route sequence.
- Pick one public claim or demo row only after verifying the page presents it.
- Name the rule ID or rule family, evidence tier, coverage label, proof route,
  and limitation if those fields are visible.
- Close with validation and static-versus-runtime boundaries.

Manager question answers should follow this pattern:

- `Question`: what the manager asks.
- `Safe answer shape`: what the presenter may say.
- `Open`: public route to show.
- `Stop when`: condition requiring handoff or limitation link.

Engineer proof routes should follow this pattern:

- `Question`: what the engineer asks.
- `Proof route`: public route sequence to inspect.
- `What to look for`: rule ID or family, evidence tier, coverage label,
  source mapping, limitation, validation, where visibly present on the verified
  public route.
- `Do not expose`: raw facts, SQLite, logs, source, SQL, config, secrets,
  local paths, remotes, generated scan directories, private names, hidden
  validation details.

## Non-Claims And Stop Conditions

The rendered page must not claim:

- Production incident diagnosis.
- Runtime behavior proof.
- Production traffic.
- Endpoint performance insight.
- Outage cause.
- Release approval.
- Operational safety.
- Complete coverage.
- Complete dependency understanding.
- AI/LLM impact analysis.
- Embeddings, vector database analysis, or prompt classification.

The presenter must stop when a question asks for any of those conclusions, or
when a public claim lacks a visible rule ID or rule family, evidence tier,
coverage label, proof path, limitation, or verified public route.

## Metadata And Discovery Design

If implemented as a standalone route, metadata should use:

- Public claim level: `concept`.
- Title: bounded manager demo script language.
- Description: deterministic static evidence, proof routes, limitations, and
  safe handoff.
- Sitemap metadata only if comparable concept pages are included.
- Discovery metadata only if comparable concept pages are included.

Metadata must avoid runtime, production, endpoint-performance, release-safety,
operational-safety, incident-diagnosis, complete-coverage, complete-dependency,
and AI/LLM positioning.

## Validation Design

Future validation should use structured HTML parsing where practical and
focused text checks where exact copy matters. It should verify:

- Required claim label and shared principle.
- Required script blocks.
- Required route links or recorded substitutions.
- Metadata, sitemap metadata, and discovery metadata for standalone placement.
- At least one inbound discovery link from `/demo/`, `/demo/runbook/`,
  `/manager-brief/`, or the selected parent page, unless a deliberate
  direct-navigation-only decision is recorded.
- Safe answer shapes for manager questions.
- Public proof routes for engineer questions.
- Stop conditions and follow-up handoff.
- Forbidden claims absent from rendered HTML, metadata, discovery output,
  sitemap output, tests, fixtures, and generated pages, except where they
  appear only inside explicit non-claim or red-flag sections of rendered body
  copy.
- Private/raw materials absent across visible copy, metadata, tests, fixtures,
  discovery output, sitemap output, and generated pages.
- Word count between 900 and 2,400 visible words unless documented otherwise.
- Desktop and mobile browser sanity with no horizontal overflow or
  unreadable card/table layouts.

Future implementation should wire focused validation into the existing site
validation path so `npm run validate` exercises the page or section.
