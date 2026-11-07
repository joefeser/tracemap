# Regular WebForms script-bundle discovery

- [x] Reconcile branch and existing inline behavior versus standalone JS inventory.
- [x] Retain bounded C#/VB explicit ScriptBundle.Include declarations and member order as syntax candidates.
- [x] Retain literal Scripts.Render references in ASPX/master/ASCX server output blocks.
- [x] Preserve file-local identities, provenance, refusal gaps, and deterministic output.
- [x] Add synthetic positive, negative, ambiguous, malformed and member/source limit tests.
- [x] Complete focused Debug and CLI smoke validation; record exact results.
- [ ] Complete the full Debug suite on the final patch before PR/merge (attempt stopped after over 40 minutes; regex guard repaired and focused recheck passed).

Explicit follow-ups, not acceptance claims of this discovery slice:
- [ ] Application-scoped registration/reference resolution, including duplicate declarations and startup evidence.
- [ ] Admitted external JavaScript inventory and bounded function/call/request relationships.
- [ ] Wildcards, directory includes, variable mutation, ScriptManager/Bundle.config forms and configuration-dependent selection.

Angular is out of scope and is not established as present in the motivating app.
