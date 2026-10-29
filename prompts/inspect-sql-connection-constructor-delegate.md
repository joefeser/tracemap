# Work-machine delegate inspection — read only, no new scan

This is the active next diagnostic instruction. Do not rerun the scan protocol in
continue-sql-compiled-wrapper-evidence.md. The existing receipt from clean public
implementation f5fe3b7d1965d843cb0167e96428d95490bcfaa4 (extractor 0.2.21,
explicit-il-body-evidence.v22) already records the actual failing invocation path.

## What we are resolving

Both selected inner SQL roots currently emit zero completed entry candidates and
stop at lifecycle-opcode:ldftn. The recorded path is selected root -> wrapper
connection Open() -> wrapper connection constructor(String), a retained body of
28 instructions, failing at IL_002b (43 decimal).

The earlier attribution to an unrelated JSON/LINQ sibling method was wrong: it
used corpus-level call observations instead of opcodeFailure.invocationPath.
Do not use that sibling method as evidence for this failure. Retry/event-handler
registration is only a hypothesis until the actual constructor is inspected.

## Strict execution scope

Read AGENTS.md. Preserve all local dirty files, binaries, receipts and outputs.
Use the existing latest receipt and local IL/metadata inspection only. No source
or tool edits, fetch/build/test/scan, retries, subagents, application execution,
database access, package installation or framework DLL admission. If an existing
inspection tool cannot expose the requested evidence, report unavailable and stop.

Bind the failing body by binary SHA plus module-local method token from the
receipt, then offset IL_002b. Do not guess the token from a photograph or identify
a method by its name alone. Follow only the constructor, delegate target and
immediate delegate consumer needed for this inspection. Inspect at most four
bodies and 512 total IL instructions; report truncation explicitly.

## Return one sanitized text report, at most 30 lines

1. Existing tool SHA/version; selected root aliases; failing binary alias/token,
   offset/opcode, retained count, and actual invocation-path aliases.
2. ldftn target signature: static/instance, generic/virtual, parameter and return
   types, body retained/available. Alias all private types and method names.
3. Delegate receiver: null, this, another allocation/reference, or unknown. State
   the observed origin and whether root-this or tracked lifecycle objects occur.
4. Exact delegate type and constructor signature, then its immediate consumer:
   event add accessor, field store, method argument, invocation, return or unknown.
   Public framework identities may be named; private identifiers must be aliased.
5. If an event subscription, report publisher origin, subscriber receiver origin,
   event signature and whether subscription is conditional. Do not infer when or
   whether the event fires.
6. Inspect the retained callback body within the limit: reads/writes, calls,
   captures and possible references to connection, command, parameters or DataSet.
   Distinguish observed absence in inspected instructions from unknown effects of
   uninspected callees. State body/metadata limitations and unresolved dispatch.
7. Describe the minimal structural shape needed for an independently authored
   public synthetic fixture in prose only; no private source/IL dump or samples.

Keep private names, paths, SQL, values, source, binaries and full receipts local.
Do not request email, code transfer or more photographs. Return text only.

Do not implement or recommend a proprietary-name allowlist, wholesale constructor
skip, dropped stores, assumed callback purity or general ldftn acceptance. A small
body, void delegate or lack of visible captures alone does not establish safety.
This inspection selects the next bounded public implementation; it does not prove
SQL lineage, native entry completion or runtime behavior. STOP after the report.
