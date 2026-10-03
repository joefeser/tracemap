# Private Web Forms Review Workflow

This workflow keeps deterministic scanner evidence, downstream interpretation,
human conclusions, and approval decisions separate. The document is public;
application identities, source, generated private artifacts, and reviewer notes
remain on the authorized machine.

## Evidence-to-decision stages

1. **Collect** — run the receipted pipeline from a clean committed checkout.
   Preserve the config, scan, packet, evidence docs, workbench, and receipt.
2. **Validate** — confirm the receipt is completed, hashes match, coverage is
   explicit, and truncation or ceilings are visible. A partial result remains
   useful but cannot be relabeled complete.
3. **Interpret** — a human or authorized agent may group aliases, propose
   hypotheses, and identify missing evidence. Interpretations are not facts.
4. **Inspect** — answer bounded questions with the smallest necessary source,
   runtime, database-contract, or external-owner evidence. Do not paste those
   private answers into a shareable artifact.
5. **Record review metadata** — write verdicts, dispositions, comments, and
   corrections in the validated review overlay. Never modify scanner facts to
   match a conclusion.
6. **Approve separately** — an authorized owner accepts, rejects, or requests
   more evidence. Approval is not inferred from a scanner or agent result.
7. **Create work items** — only approved conclusions become tickets. Each ticket
   cites the retained evidence IDs, the human-review record, open unknowns, and
   the source/commit identity held inside the authorized system.

## Minimum gates

| Gate | Required evidence | Stop condition |
| --- | --- | --- |
| Collection | Completed or explicitly partial receipt with repo and commit SHA | Missing/mixed provenance |
| Interpretation | Fact/inference/unknown ledger | Uncited conclusion or a gap treated as absence |
| Human review | Named reviewer, timestamp, disposition, evidence references | Review changes scanner evidence |
| Approval | Explicit authorized-owner decision | Agent output presented as approval |
| Ticket creation | Approved review plus retained references | Private source copied into a public tracker |

External services are boundaries. When a retained call reaches another team's
endpoint, record the supported local call and the external ownership boundary;
request that team's contract or runtime evidence separately. Do not invent the
unavailable implementation behind the boundary.

The experimental WITS overlays provide deterministic validation for review
metadata. See the editing guides under `scripts/webforms-review/`. Automation
may prepare drafts or proposed tickets, but publishing them must remain a
separate, explicit authorized action.
