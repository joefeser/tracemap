# Requirements: Legacy Web Forms static composition

## Scope

Implement issue #708 as one deterministic, syntax/structure-first extension to the existing Web Forms extractor. The slice must remain useful for non-compiling .NET Framework repositories and must not weaken already-shipped evidence.

## Requirements

1. The scanner shall identify supported `if (!IsPostBack)` syntax inside `Page_Init`, `Page_Load`, and `Page_PreRender` methods and emit lifecycle branch-context candidates with exact source spans.
2. Unsupported or dynamic `IsPostBack` conditions in those lifecycle methods shall emit a typed rule-backed gap rather than a branch conclusion.
3. The scanner shall identify bounded `RegisterStartupScript` and representative client-script registration invocations when their registered payload or target is a literal. It shall retain only categorical metadata, lengths, and hashes—not script bodies or raw target values.
4. Dynamic or unsupported client-script registrations shall emit a typed gap.
5. The scanner shall identify literal `__doPostBack` target candidates in checked-in markup and supported literal client-script registrations. It shall hash targets and only attach an exact same-surface control identity when one static control matches.
6. Dynamic, malformed, missing, or ambiguous postback targets shall remain candidates or explicit gaps; the scanner shall not infer event dispatch.
7. The scanner shall correlate an explicit static `DataSourceID` with exactly one same-surface server control. Missing or duplicate targets shall emit typed gaps.
8. The scanner shall identify literal `Eval` and `Bind` field-expression candidates and associate them with a bounded enclosing server control when syntax permits. Field arguments shall be hashed and raw expression text shall not be stored.
9. Each emitted fact shall include a rule ID, evidence tier, file span, commit SHA through the scan manifest, extractor version, coverage label, documented limitations, and ordered supporting fact IDs when composition occurs.
10. A synthetic non-compiling .NET Framework fixture shall prove the new candidates and gaps while preserving `.ashx`, handler, redirect/transfer, markup-event, lifecycle, and reduced-compilation evidence.
11. Output shall be deterministic and shall not store source snippets, script bodies, raw postback targets, raw binding field names, credentials, connection strings, or absolute host paths.

## Non-claims

- No runtime reachability or execution.
- No branch-taken or lifecycle-order conclusion.
- No rendered-page, browser-script, or event-dispatch conclusion.
- No successful control or data binding.
- No database row, query, handler, redirect, transfer, or external-service execution.

