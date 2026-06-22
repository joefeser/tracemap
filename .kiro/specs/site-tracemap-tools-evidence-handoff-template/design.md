# Site TraceMap Tools Evidence Handoff Template Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Overview

This design describes a future public-site concept surface for a reusable
TraceMap evidence handoff template. The surface should help a human carry one
bounded static-evidence question to another reviewer or owner while keeping the
proof path, deterministic evidence fields, limitations, non-claims, validation
evidence, and stop condition visible.

The implementation is site-only. It does not add scanner behavior, reducer
behavior, generated handoff output, ownership automation, runtime monitoring,
release approval, operational safety proof, AI impact analysis, LLM analysis,
or replacement for human review.

## Placement Decision

Candidate placements:

- `/handoff/template/`
- `/team-evidence-handoff/template/`
- Section on `/team-evidence-handoff/`
- Section on `/packets/assembly/`

The implementation should inspect the live neighboring routes before choosing.
The preferred decision should minimize overlap:

- Choose `/handoff/template/` if the site needs a neutral reusable template
  that is not tied to a team-specific or packet-assembly parent.
- Choose `/team-evidence-handoff/template/` if the live
  `/team-evidence-handoff/` route already owns template-like receiver
  language and nested placement would make the route easier to discover.
- Choose a section on `/team-evidence-handoff/` if the template is short and
  receiver-specific context is the clearest user path.
- Choose a section on `/packets/assembly/` only if implementation finds that
  the template is mostly a packet-assembly artifact and direct handoff routes
  would create duplicate page purpose.

The selected placement, rejected alternatives, metadata consequences, and route
gaps must be recorded in `implementation-state.md` before site source changes.
If none of the four candidate placements are acceptable after inspecting the
live site, the implementation must stop, record all conflicts in
`implementation-state.md`, and move the spec back to needs-revision before
changing any site source.
Embedded placement is likely difficult because the full content set is large;
it is retained only for completeness and should be selected only if validation
can pass without weakening required fields, examples, stop conditions,
non-claims, or neighbor distinctions.

## Content Structure

1. Intro: name the evidence handoff template, show
   `Public claim level: concept`, and state
   `No public conclusion without evidence`.
2. When to use it: explain that the template transfers one bounded
   static-evidence question, claim, or proof path to another human reviewer or
   owner.
3. Neighbor distinctions (section heading label: `neighbor distinctions`):
   differentiate from `/team-evidence-handoff/`,
   `/incident-evidence-handoff/`, `/packets/assembly/`,
   `/reviewer-quickstart/`, `/owners/follow-up/`, and
   `/decisions/evidence-record/`.
4. Template: present the required field labels in a compact table or list.
5. Filled synthetic example: show a public-safe, clearly synthetic example
   with concept-level data and no private source material.
6. Unsafe example: show what must stop or be rewritten, using synthetic unsafe
   wording rather than real private material.
7. Handoff checklist: provide a final pre-share checklist for handoff
   question, public claim level, proof path, rule ID or family, evidence tier,
   coverage label, limitation, non-claim, validation evidence, owner to ask,
   stop condition, and audience when the receiver differs from the owner to
   ask.
8. Stop conditions: list the blockers that require pause, downgrade, private
   review, or another owner.
9. Non-claims: state the boundaries for runtime, release, safety,
   completeness, ownership, AI or LLM analysis, and human review.

## Required Template Fields

The rendered template must include these 15 field labels exactly:

- `handoff question`
- `audience`
- `proof path`
- `public claim level`
- `rule ID/family`
- `evidence tier`
- `coverage label`
- `public-safe path/span`
- `commit SHA`
- `extractor version`
- `limitation`
- `non-claim`
- `validation evidence`
- `owner to ask`
- `stop condition`

Each field should have a short description. The descriptions should keep the
field tied to deterministic, public-safe static evidence and should say when
missing data becomes a limitation or stop condition.

## Synthetic Examples

The filled example should be labeled `synthetic example` or equivalent visible
copy. It should populate all 15 required fields. Private or unavailable values
should be shown as explicit limitations rather than omitted. It may use values
such as:

- concept-only handoff question about a route, handler, DTO, dependency edge,
  package reference, configuration surface, or SQL-facing reference;
- synthetic audience naming a receiving review role, not a person;
- synthetic proof path to a public-safe TraceMap route;
- synthetic rule ID or rule family;
- public claim level `concept` unless the example quotes a real public-safe
  evidence surface;
- one of the TraceMap evidence tiers;
- concept-only, demo-only, partial, reduced, gap, unknown, syntax-only, or an
  existing public-site coverage label;
- sanitized public-safe path/span language;
- visibly synthetic commit-like context only when clearly marked synthetic,
  such as `synthetic-sha-0001` rather than a realistic 40-character hex
  string;
- synthetic extractor-version context only when clearly marked synthetic;
- explicit limitation stating what the static evidence cannot prove;
- categorical validation evidence, not command output;
- explicit non-claim wording;
- explicit stop-condition wording;
- role-based owner to ask.

The unsafe example should be labeled `unsafe example` or equivalent visible
copy. It should demonstrate stop-worthy patterns without publishing real raw
material: missing proof path, private-only support, raw or private material,
unsupported runtime proof, unsupported release or safety wording, unsupported
complete-coverage wording, AI or LLM analysis wording, no validation evidence,
no owner to ask, or blame language.

## Neighbor Route Relationship

- `/team-evidence-handoff/`: receiver-specific handoff language. The template
  supplies reusable fields that can travel with that language.
- `/incident-evidence-handoff/`: incident-adjacent static evidence transfer.
  The template is general and must not imply outage cause or incident command.
- `/packets/assembly/`: broader human workflow for selecting packet
  ingredients. The template is the field structure for one bounded handoff
  after ingredients are selected.
- `/reviewer-quickstart/`: reviewer orientation. The template is a portable
  handoff form, not onboarding.
- `/owners/follow-up/`: owner follow-up framing if present. The template
  names a role to ask but does not claim real org ownership or assign work.
- `/decisions/evidence-record/`: decision-record framing if present. The
  template preserves handoff context and stop conditions, not final decisions.

Cross-links should use role-specific anchor text and must not imply runtime
proof, release approval, operational safety, complete coverage, AI or LLM
analysis, real org ownership, or replacement of human review.

## Public Safety

The page and metadata must not publish raw facts, SQLite content, analyzer
logs, source snippets, SQL, config values, secrets, local paths, remotes,
generated scan directories, private sample names, command output, hidden
validation details, credential-like values, connection strings, tokens, keys,
private repository identifiers, named individuals, or personal owner names.

The page may publish authored concept copy, public-safe summaries, and
synthetic examples. Any private repository evidence must remain private until a
public-safe summary has been reviewed and approved for public site use.

## Metadata and Discovery

If implemented as a standalone route, the future implementation should add:

- page title and description consistent with neighboring concept pages;
- canonical URL for the selected route;
- Open Graph title, description, URL, and `og:type`;
- sitemap metadata using existing `pages.json` patterns;
- discovery metadata with `publicClaimLevel: concept`, source type consistent
  with neighboring site pages, bounded hint category, preferred proof path
  where the live site pattern supports it, limitations, non-claims, and
  neighboring route hints.

If implemented as an embedded section, the future implementation should add:

- a stable section anchor;
- minimal parent-page metadata or discovery changes needed for readers and
  validators to find the section;
- an `implementation-state.md` note explaining why separate sitemap metadata
  is not required.

## Validation Design

Future implementation should add focused validation following neighboring
concept-page validator patterns. The validator should check:

- selected route or section renders;
- required rendered copy appears: `Public claim level: concept` and
  `No public conclusion without evidence`;
- all required field labels appear exactly;
- required sections appear: when to use it, neighbor distinctions, template,
  filled synthetic example, unsafe example, handoff checklist, stop
  conditions, and non-claims;
- neighboring route distinctions appear for `/team-evidence-handoff/`,
  `/incident-evidence-handoff/`, `/packets/assembly/`,
  `/reviewer-quickstart/`, `/owners/follow-up/`, and
  `/decisions/evidence-record/`; these distinctions are required even when a
  live link is unavailable and the route is documented as a gap;
- required neighbor links and support links to `/proof-paths/`,
  `/limitations/`, and `/validation/` resolve to `routes-index.json`,
  sitemap metadata, generated pages, or documented route gaps;
- standalone metadata includes title, description, canonical URL, Open Graph
  fields, `og:type`, sitemap metadata, and discovery metadata;
- discovery metadata includes `publicClaimLevel: concept`, limitations, and
  non-claims when standalone;
- examples are visibly labeled synthetic or derived from approved public-safe
  demo summaries;
- the filled synthetic example populates all 15 required fields, with private
  or unavailable values shown as explicit limitations rather than omitted;
- handoff checklist validation confirms that handoff question, public claim
  level, proof path, rule ID or family, evidence tier, coverage label,
  limitation, non-claim, validation evidence, owner to ask, stop condition, and
  audience-when-different are present, or that any intentionally reduced
  checklist states which fields are omitted and why;
- handoff checklist validation confirms `audience` is present as a visible
  field label, or a visible note states that audience is omitted when receiver
  and owner to ask are the same role;
- stop conditions include missing proof path, private-only support, raw or
  private material, unknown or reduced coverage without label, unsupported
  runtime proof wording, unsupported release or safety wording, unsupported
  complete-coverage wording, AI or LLM analysis wording, no validation
  evidence, no owner to ask, and blame language;
- rendered text, decoded HTML, raw HTML attributes, and metadata do not contain
  forbidden positive claims for generated handoff features, real org
  ownership, runtime proof, production traffic, endpoint performance, outage
  cause, release approval, release safety, operational safety, complete
  coverage, AI impact analysis, LLM analysis, autonomous review, or human
  review replacement;
- rendered text, decoded HTML, raw HTML attributes, and metadata do not expose
  forbidden raw or private material;
- rendered text, decoded HTML, raw HTML attributes, and metadata are checked
  for named individuals, personal owner names, and real organization names
  using a denylist or named-entity check where available; when automated
  detection is unreliable, implementation records a required manual
  public-safety review gate and uses only role-based or synthetic names;
- forbidden-material scanning includes shape-based patterns for realistic
  commit SHAs, including full 40-character hex strings and abbreviated 7-12
  character lowercase hex strings, API tokens, keys, and connection strings;
  clearly synthetic forms such as `synthetic-sha-0001` are required for
  synthetic scan context;
- positive-claim scans are scoped so required non-claims, unsafe examples,
  stop conditions, handoff checklist, template field descriptions, neighbor
  distinctions, and when-to-use section can mention forbidden topics only in
  clearly negated, clearly labeled, or clearly cautionary contexts;
- forbidden positive-claim checks use phrase-scoped patterns, not bare tokens,
  and allow bounded boundary vocabulary and negated forms such as
  `does not prove runtime`, `unsupported runtime proof wording`,
  `No public conclusion without evidence`, `public-safe`, and
  `static evidence`;
- labeled rendered contexts are machine-checkable by nearest section heading
  or visible example label containing `non-claim`, `not claimed`,
  `unsafe example`, `not recommended`, `stop condition`,
  `stop conditions`, `handoff checklist`, `template field`,
  `neighbor distinction`, `distinguish`, `when to use`, or `caution`,
  or by an explicit context marker such as `data-context="non-claim"`,
  `data-context="unsafe-example"`, `data-context="stop-condition"`,
  `data-context="handoff-checklist"`, `data-context="template-field"`,
  `data-context="neighbor-distinction"`, `data-context="when-to-use"`, or
  `data-context="caution"`; a bare `template` section heading does not label
  every descendant as an allowed forbidden-term context;
- presence checks for similarly named labels, such as `non-claim` versus
  `non-claims`, `stop condition` versus `stop conditions`, and
  `public claim level` intro versus field labels, use exact context-scoped
  matching so substring collisions do not satisfy the wrong rule;
- metadata has no rendered section context, so forbidden terms in metadata
  appear only in explicit limitations or non-claims fields;
- rendered visible-body word count has a hard minimum of 500 words for a
  standalone route or 300 words for an embedded section, and a target maximum
  of 1600 words standalone or 900 words embedded, excluding global navigation,
  footer, metadata, hidden validation data, alt attributes, scripts, and
  styles;
- exceeding the standalone target emits a tightening warning, not a hard
  failure, provided all required labels, sections, stop conditions,
  non-claims, and all six neighbor distinctions are present and pass
  independently;
- embedded placement remains a hard failure if required content cannot fit the
  900-word bound without weakening required content;
- embedded placement is rejected if required content cannot fit the embedded
  word-count bound without weakening the template fields, examples, stop
  conditions, non-claims, or neighbor distinctions;
- standalone copy that exceeds the 1600-word target should be
  tightened by shortening descriptions and examples, not by dropping required
  labels, sections, stop conditions, non-claims, or neighbor distinctions;
- required labels, required sections, and neighbor distinctions are checked
  independently from word count;
- standard site validation, private-path guard, build validation, and desktop
  and mobile browser sanity checks pass before PR.
