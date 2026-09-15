# Review Web Forms modernization evidence

You are reviewing deterministic static evidence produced by TraceMap for an
authorized ASP.NET Web Forms application. Begin with the evidence artifacts,
not the application source. Do not edit files, run the application, connect to
external systems, or infer private identities.

## Read in this order

1. Locate the supplied `application-handoff.json` and read its provenance,
   coverage, limitations, call accounting, page inventory,
   `analysis.coverageReductionReasons`, `analysis.packetTruncationReasons`,
   `nextEvidenceSummary`, and `controlRegistrationGaps`.
2. Read the supplied evidence-docs `manifest.json` and `query-recipes.json`.
   Confirm that their scan and input provenance is compatible with the
   application handoff. Report any mismatch and stop.
3. Use page handoffs for selected pages. Use `chunks.jsonl` only through the
   retained evidence identifiers or closed retrieval hints already present in
   the handoffs and query recipes. Do not load the entire corpus into your
   response.
4. Read the private packet only when a required field is absent from the
   handoffs. Do not quote or reproduce private paths, symbols, source text,
   repository names, commit identifiers, business identifiers, SQL, config
   values, URLs, or credentials in the answer.

## Evidence rules

- Treat TraceMap records as static evidence with explicit coverage and
  limitations, not runtime truth.
- Preserve page aliases in the answer. Do not attempt to recover their source
  paths or identities.
- `P / F / S` means chain-associated projections / unique retained facts /
  normalized source sites. These are not runtime call counts.
- Syntax and semantic records at one site may explain `F > S`. Reuse across
  chains may explain `P > F`.
- Technology-family labels require retained semantic declaring-type or
  assembly evidence. Do not classify a dependency from a method or control
  name alone.
- Separate systemic extractor/coverage gaps from page-specific application
  questions.
- Coverage reduction and packet truncation are different states. Never claim
  that reduced source analysis caused truncation unless an explicit retained
  truncation reason says so. A page's `packetTruncated` value describes the
  application packet; use `pageTraversalTruncated` for that page's chains.
- Never convert an evidence gap into a negative conclusion.
- Describe `.ashx` artifacts as HTTP handlers unless retained contract evidence
  establishes a stronger API classification.
- When requesting more evidence, name the unresolved call targets, declaring
  types, assemblies, source-availability states, or traversal bounds retained
  in the packet. Do not replace a precise frontier with generic advice to add
  assemblies or source.
- Use each retained `nextEvidenceKind`, target, required input, and truncation
  reason before proposing a rerun. Do not recommend increasing a generic
  traversal limit when the retained reason or bound is unavailable.
- For unresolved control registrations, report the retained safe prefix, type,
  assembly, namespace, and registration state. If a field is unavailable, say
  that explicitly; do not invent a library identity or generic assembly fix.

## Deliverable

Produce a bounded Markdown assessment with these sections:

1. **Evidence receipt** — artifact schemas, generator/input hashes, coverage,
   truncation/ceiling state, and any provenance mismatch.
2. **What the evidence establishes** — facts only, with page alias and evidence
   category citations.
3. **Technology and capability map** — application, framework, Telerik, other
   third-party, unresolved, client behavior, state, navigation, data, and
   boundary evidence. Mark unavailable categories explicitly.
4. **Review cohorts** — group pages by evidenced characteristics. Explain each
   grouping without predicting effort.
5. **Conversion questions** — for each cohort, list the source, runtime,
   contract, ownership, or UX questions that must be answered before choosing a
   target implementation.
6. **Extractor backlog** — repeated evidence gaps that should be investigated
   in TraceMap rather than filed as hundreds of application defects.
7. **First review queue** — at most 12 aliased pages or cohorts, selected for
   information gain. State why each was selected.
8. **Fact / inference / unknown ledger** — keep these three categories visibly
   separate.

Do not provide a total migration estimate, declare conversion feasible or
safe, create tickets, recommend production changes, or claim that static paths
executed at runtime. End with the smallest additional evidence request that
would materially change the assessment. The final stdout response must contain
the complete eight-section Markdown assessment; do not return only a summary,
plan-file path, or pointer to another artifact.
