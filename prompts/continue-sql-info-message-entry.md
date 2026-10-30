# SUSPENDED — reported scan resource regression

The instructions below are inactive. A work-machine 0.2.22 run with the new flag
was reported to grow to roughly 46 GB without completing. The responsible phase
and root cause are NOT established. Do not build, rerun, retry or enable this flag
from this prompt. Use `diagnose-info-message-resource-regression.md` for read-only
inspection of existing evidence only. Passing local tests are not resource-safety
acceptance for this workload. A separate gated diagnostic is described in
run-bounded-info-message-diagnostic.md; it does not reactivate this unguarded scan.

---

# Work-machine InfoMessage entry check — one build, one scan, then stop

This supersedes the prior inspection-only prompts for this next run. The public
implementation is extractor 0.2.22, evidence policy explicit-il-body-evidence.v23,
InfoMessage policy explicit-sql-info-message/1.

EXPECTED_IMPLEMENTATION_COMMIT: `850042739468b57bf3b261bcabaaad344874221e`

Use that exact implementation or a verified docs-only descendant. Record the
actual build SHA and verify that any descendant changes only documentation/prompts,
not implementation, fixtures, tests or rules. Do not use a dirty tool build.

Read AGENTS.md. Preserve existing dirty patches, inputs, receipts and outputs.
Use an isolated clean public tool checkout; do not reset, clean, overwrite or apply
local fixes. Verify the implementation SHA and build basis before proceeding.

Perform at most ONE build and ONE targeted native scan, then ONE sanitized report
and STOP. No tests, edits, retries, subagents, application runtime or SQL execution.
If isolation/build/input verification fails, report that failure and stop.

Reuse the same verified INNER root selections and compiled input paths/hashes
from existing private receipts. Exclude outer facades. Do not guess paths or tokens
from photographs, add framework DLLs, widen selected roots or modify private code.
Freeze inputs and use a fresh private output directory. Keep the existing flags:

```
--il-opaque-root-initializers
--il-external-trace-boundary
--il-compiled-scalar-boundary
```

Add this new flag:

```
--il-info-message-boundary
```

The new boundary tracks the exact instance event delegate, its private field,
connection subscription/removal, connection-linked command and filled DataSet.
It inspects the callback and post-Fill naming body without executing either.
Callbacks may append only strings to own private ArrayLists and use the finite
signed string/XML/VB framework catalogue on untracked values. Sender access,
other captured fields, writes, uninspected calls and tracked-reference escapes
still refuse. General delegate/LINQ interpretation and constructor bypasses were
not added. Unsupported private shapes may still produce explicit gaps.

Successful entries using this boundary have the separate schema
il-sql-lifecycle-info-message-entry/1. infoMessages binds callback body/binary,
receiver/delegate/connection allocations, private field hashes and event sites.
resultNaming binds the naming body and actual filled DataSet/execution, with
names and ordinal association unbound. Event firing, runtime names, table count,
XML effects, naming success and normal completion are not proven. Do not claim
default names when no concrete callback values exist. Do not use this entry as a
legacy read/comparison fact or fabricate consumer schema comparison.

Report at most 35 sanitized text lines:
- Actual 40-character clean implementation SHA, extractor/policies, build status,
  flag recognition, private input hash verification and selected-root count.
- Per root: entry candidate count/schema; constructor/opaque/scalar/trace receipts
  if present; infoMessages and resultNaming counts.
- If an entry exists: distinct command/parameter allocations and attachment,
  Fill execution, returned DataSet allocation, and whether naming binds that SAME
  allocation/execution. Report names as unbound, not guessed or runtime defaults.
- If blocked: FIRST actual rule/gap on each selected root, and its own root/binary/
  token/offset. Read infoMessageFailure directly on the AnalysisGap when present:
  phase, opcode, call hash. If a framework call is rejected, include its PUBLIC
  framework signature/assembly/version/key from existing local metadata; alias
  private calls. Callback inspection is not callback invocation.
- For opcode gaps, use opcodeFailure.invocationPath, not corpus-level call facts.
  Distinguish absent receipt from proven passed/unreached phase.
- State baseline preserved; ENTRY_LINEAGE_PROVEN yes only if completed linked
  native evidence actually exists; consumer comparison not performed if absent.

Private identifiers, source/IL, XML/SQL/literal values, binaries and full receipts
stay local. Use aliases and short text only. No code/sample transfer, email or
new photographs requested. STOP after the one report, even if a gap remains.
