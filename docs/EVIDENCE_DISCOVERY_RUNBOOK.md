# Investigate one conversion question from a completed TraceMap run

For a paper review, print the [human evidence review worksheet](HUMAN_EVIDENCE_REVIEW.md).

Start with the completed run folder and a question about one behavior. For example, “Which handler receives this button's request?” or “Which command receives the SQL assembled here?” A human or an agent can follow the same steps.

## 1. Read the human handoff

Open the workspace's `START-HERE.md`, then `report.md` and the relevant source report. For a standalone scan, start with `report.md` and `scan-manifest.json`. Record the source commit, build status, analysis level, and any bounds that affect the question. A Web Forms packet, when present, can narrow the investigation to one page. Keep the original reports with the run so another reviewer can see what the collector reported before anyone interpreted the evidence.

## 2. Discover what this index actually contains

Open the selected SQLite file with `sqlite3 -readonly`, then use `.tables` and `.schema` to inspect it. For a single scan, inventory the available fact families:

```sql
SELECT fact_type, rule_id, evidence_tier, count(*) AS retained
FROM facts
GROUP BY fact_type, rule_id, evidence_tier
ORDER BY fact_type, rule_id, evidence_tier;
```

For a combined index, choose the source label from `index_sources`, then use this inventory. Bind `@label` to that label in the SQLite shell with `.parameter set @label web` (the public trial uses `web`).

```sql
SELECT f.fact_type, f.rule_id, f.evidence_tier, count(*) AS retained
FROM combined_facts f JOIN index_sources s ON s.source_index_id = f.source_index_id
WHERE s.label = @label
GROUP BY f.fact_type, f.rule_id, f.evidence_tier
ORDER BY f.fact_type, f.rule_id, f.evidence_tier;
```

Pick a candidate from the relevant family using its source location or metadata identity. Open the fact by ID and inspect its properties. Follow its explicit support, declaration, request, or handler IDs as applicable. In combined data, resolve original IDs within the originating source index. Record the query and the IDs you actually followed. Use the [SQLite recipes and report commands](SQLITE_REVIEW_RUNBOOK.md) for two worked examples.

## 3. Check each hop, especially where evidence changes form

| Question at this hop | Check before using it for conversion |
|---|---|
| Is this script available to this page? | Follow the page or master reference to the bundle declaration and member. A click assigned inside a callback can be located even when callback invocation is unobserved. |
| Is this source literal the compiled operand? | Verify the complete digest using the extractor's encoding and the operand order. Preserve the difference between an exact literal match and a same-length candidate. |
| Is this the called overload? | Match the call operand's assembly, declaring type and complete signature, including by-reference parameters, to the admitted definition. Cite the target artifact identity, module MVID and definition token when available. A similar body in a newer source checkout supplies a lead. |
| What receives this value? | Follow argument slots, call results and allocation sites in the matched body. Identify the receiver and collection at each call. Adding a table mapping to an adapter and adding a parameter to a command answer different questions. |

At a break, inspect the recorded gap and its reason. If authorized matching source or compiled evidence fills part of the break, cite it separately. If the artifact is missing, the version differs, or more than one candidate survives, record the first unresolved hop and stop there. An empty query under partial coverage leaves the question open.

## 4. Return a review note a developer can act on

Give each hop a state in plain language, such as **retained**, **independently verified**, or **unresolved**. These are review-note labels. Preserve the collector's own fact type, tier and gap code.

For example: “The caller's SQL literal is independently verified against the compiled operand. Parameter attachment remains unresolved because the retained Add call targets the adapter's table-mapping collection. Inspect the exact wrapper overload for a call on the command's parameter collection.” A developer can use the literal now and put a focused check around the remaining behavior.

Keep the note beside the run in the authorized review area. Include the scan/commit, fact or gap ID, source span or IL location, rule, tier, extractor version, and the reason for each conclusion. End with what can be converted now and one verification that could change the decision. Treat a proposed test and an observed test result as distinct entries.

## Copy this prompt into Claude

> Investigate this conversion question: **[question]**. Use the completed TraceMap run at **[run folder]**. Start with its human report and manifest, then use the index read-only. Discover the available evidence families and choose one concrete caller or surface. Follow exact supporting identities as far as the evidence permits. For each hop give its state, reason, and citation. Label independent inspection of authorized matching source or assemblies separately. At the first unresolved hop, explain what is missing and what observation would settle it. Return a short companion review note with the query, what the conversion can use now, and one acceptance test. Preserve the original reports and facts. This is a retained-run review; do not rescan or execute the application or its database.
