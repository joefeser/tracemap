# Claude: prepare an implementation evidence packet

Use the completed current-tool HTTP readiness check as the starting point. Prepare
an evidence-backed packet for a later, explicitly scoped implementation task.
Do not implement application changes or restart extraction.

## Bounds and privacy

30 minutes total, read-only inspection of existing approved local artifacts and
source already in the accepted scope. No build, scan, combine, preflight, requery,
database connection, SQL execution, outbound HTTP, application execution,
dependency installation, Git mutation, or new diagnostic scripts. Use existing
approved inspection tools. Stop at the budget and list incomplete items.

Keep source, paths, symbols, endpoints, SQL text, schema details, identifiers,
hashes, payloads and all generated notes private, outside Git. Write one new local
packet without overwriting acceptance notes or retained artifacts. Return only a
manually sanitized summary using H1–H4 and anonymous SQL contract labels.

## 1. Reconcile accepted evidence

Read the baseline inventory, acceptance/correction notes, and the completed
`check-current-work-http-readiness.md` result. Identify artifacts by recorded
identity, not newest-folder names. Record live tool revision separately from each
artifact's producer revision. This task does not qualify a newly pulled checkout.

Rehash accepted baseline artifacts against the recorded before-run values from
the completed readiness check. Include HTTP and database artifacts for which such
values were recorded. Report MATCH / MISMATCH / NO RECORDED COMPARATOR per artifact.
A current match establishes matching bytes at this check, not continuous immutability
throughout the prior run. Do not call a before-run-only check preservation proof.
If anything differs, preserve it and stop using that artifact as accepted evidence.

## 2. HTTP implementation evidence, H1–H4

For each accepted path, document locally:

- Current observed behavior and separately any explicit desired behavior. If no
  implementation objective was supplied, mark DESIRED CHANGE NOT SPECIFIED.
- Verified entry/root and expected terminal, ordered joins, rule IDs, tiers,
  locations, supporting fact references, source revision and extractor versions.
- Inputs and outputs, transformations, dependencies, configuration requirements,
  and statically observed side effects. Mark unknowns; reachability alone does not
  establish payload values, authorization, validation, or successful mutation.
- Existing test evidence and proposed synthetic acceptance cases for a later task.
- Questions that require operator requirements or additional evidence before code.

Event binding remains an anchor, not a traversed call edge. H3's HTTP method is
unproven in the retained evidence, not inherently impossible to establish statically.
H4's structural lifecycle candidate is separate from its semantic method-to-HTTP
path; dispatch, construction and lifecycle execution remain unproven. Preserve all
truncation and gap reasons. Missing bounded paths do not prove absence.

## 3. SQL evidence status — separate from HTTP acceptance

Find the previously accepted database review receipt, report, inventory and any
recorded schema/procedure contract acceptance notes. Do not execute historical
prompts merely because they are referenced by a note; this task's read-only bounds
take precedence. Reconcile retained evidence only, with no new query or rescan.

For each relevant SQL contract, distinguish:

1. Static caller-to-database path retained and previously accepted: root/terminal,
   rules, tiers, locations, supporting facts, bounds and original producer revision.
2. Command/procedure name and parameter mapping evidenced, partial, or missing.
3. Schema/procedure definition available and contract checked, partial, or missing.
4. Write/transaction behavior and runtime validity: only claim prior validation if
   a specific retained acceptance record proves its exact scope; otherwise UNVERIFIED.
5. Artifact identity now matches its recorded comparator, or cannot be checked.

Procedure definitions do not establish the client's CommandType or parameter
constructor/overload behavior. Those require separate source or retained compiled
evidence from the caller/wrapper. A supplied definition can inform result and write
contracts, but cannot by itself prove deployed identity, runtime success, or that
all write-path tests are unblocked. Missing historical hashes cannot be repaired
retroactively by recording a new hash today.

Classify as PREVIOUSLY ACCEPTED STATIC EVIDENCE RETAINED, PARTIAL CONTRACT EVIDENCE,
MISSING INPUT, or INPUT DRIFT. Do not collapse these into "SQL works." The successful
HTTP check does not validate SQL, and retained old reports do not qualify the new
tool's SQL behavior. If SQL is unrelated to these paths, keep it a separate inventory
entry; do not invent a linkage. Missing schema or DDL is a request for evidence,
not permission to access a database or fabricate a contract.

## 4. Return a decision-ready summary

Return a sanitized table: group/contract label; retained evidence status; baseline
comparison; requirements known; unresolved inputs/contracts; proposed next action.
Then answer:

- What bounded review/design work can proceed now?
- Is a concrete implementation objective defined? If not, which operator decisions
  are needed? Do not invent a migration target or desired application behavior.
- Which SQL claims are supported by retained acceptance, and which remain untested?
- What is the smallest next evidence request or synthetic test for each blocker?

No overall application-build, migration, security, SQL-runtime, or exhaustive-coverage
readiness claim. Do not start implementation. Do not publish private packet content.
