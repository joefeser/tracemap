# Human evidence review worksheet

Print one copy per conversion question. Keep the completed sheet with the original run in the authorized workspace.

## Start with the human report

1. Open START-HERE.md for a workspace collection, or report.md and scan-manifest.json for a single scan. Record the source revision, extractor version, coverage and any build or truncation gaps.
2. Write one question with a named source and destination. Pick an entry from the report and locate its fact ID in the retained SQLite index. In a combined index, keep the source index ID with the original fact ID.
3. Follow each supporting ID to its source span or compiled location. For a compiled overload compare the assembly, declaring type and complete signature. Record the module identity, method token and IL offset where available.
4. Give each relationship a state and a reason. Cite the rule, tier and facts. Record the first missing link and what would establish it. Keep a reviewer note with the run so the original scanner output stays reproducible.

## Two public controls

**SQL.** In the VB SQL argument sample, the UPDATE passes into a wrapper. The read-only query returns a source hit with binding=unverified. Record SQL composition, CommandText flow and parameter attachment separately. To establish attachment, follow the parameter object into the parameter collection of the same command. An adapter table-mapping addition establishes a different relationship.

**Web Forms.** In the service POST sample, two markup clicks point to the same script method. The fixture query returns four rows because each click carries two gaps. The dynamic script source is unavailable and callback invocation is unobserved. Keep both reasons when citing the static POST and handler declaration.

## Record the decision

| Review field | Record |
|---|---|
| Question and reviewer | |
| Run, source index and commit | |
| Extractor, coverage and limits | |
| Claim, state and reason | |
| Rule, tier and supporting fact IDs | |
| Source span or module, method and IL offset | |
| First missing link and verification needed | |

## Continue from the retained index

Use the [discovery runbook](EVIDENCE_DISCOVERY_RUNBOOK.md) for the agent handoff. The [SQLite review runbook](SQLITE_REVIEW_RUNBOOK.md) includes the public control queries and commands to regenerate human reports and JSON. Retain the original Markdown report for human review alongside its manifest and index.
