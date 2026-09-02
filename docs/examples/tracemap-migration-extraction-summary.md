# TraceMap migration extraction summary

## Bottom line

- The run remains failed/partial: one of 17 source scopes reported full
  semantic success and 16 reported reduced or partial analysis.
- Endpoint alignment is not proven. Sixty-six interaction pairs were configured,
  but none had exact aligned evidence under the available reduced coverage.
- Razor evidence is currently a reduced-coverage gap inventory, not a complete
  binding inventory.
- The only legacy-event rule shown is WinForms-specific and cannot support a
  WebForms event-binding claim.
- Angular/TypeScript extraction is blocked by toolchain and configuration gaps;
  those gaps are not proof of application defects.

## E1-E4 lane state

| Lane | State | Evidence available | One next action |
| --- | --- | --- | --- |
| E1 Endpoint alignment | not-proven | 414 server endpoint declarations, 312 normalized candidates, zero exact matches, zero client-call candidates, and 102 unavailable route identities | Restore client-call evidence first; then run bounded candidate clustering over existing endpoint and reverse reports. |
| E2 Razor binding inventory | reduced-coverage | 69 reduced binding diagnostics: 67 binding-type and two binding-property gaps | Recover semantic prerequisites for the highest-gap scopes, then rerun only Razor-binding extraction. |
| E3 Legacy event patterns | unknown | 11 WinForms subscription gaps; no rule-backed WebForms binding evidence | Separate event evidence by technology with a bounded rule-backed classifier before making WebForms claims. |
| E4 Angular/TypeScript readiness | blocked-by-toolchain | 27 missing-module, 105 ordinary-type, and 30 TypeScript configuration diagnostics across six blocked scopes | Produce anonymous per-scope readiness categories before attempting Angular parity analysis. |

## Recommended order

Start with E4 readiness because the available evidence contains zero client-call
candidates. Endpoint clustering cannot recover a missing client-side evidence
set. After the affected TypeScript prerequisites are corrected, rerun only the
affected client extraction and then repeat E1 alignment.

## Limitations

This summary was reconstructed from owner-supplied sanitized metadata and
photos of aggregate output. It is not a byte-for-byte copy of the private
result. Private source labels, repository identities, run IDs, routes, values,
and source commit SHAs were deliberately omitted. The exact private source
snapshot cannot be authenticated from this handoff, so the summary is suitable
for planning extraction improvements, not proving application completeness or
migration parity.
