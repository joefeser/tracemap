# Regular WebForms script-bundle discovery

- [x] Reconcile branch and existing inline behavior versus standalone JS inventory.
- [x] Retain bounded C#/VB explicit ScriptBundle.Include declarations and member order as syntax candidates.
- [x] Retain literal Scripts.Render references in ASPX/master/ASCX server output blocks.
- [x] Preserve file-local identities, provenance, refusal gaps, and deterministic output.
- [x] Add synthetic positive, negative, ambiguous, malformed and member/source limit tests.
- [x] Complete focused Debug and CLI smoke validation; record exact results.
- [x] Complete the full Debug suite on the current bundle implementation. Pinned validation head d39d533a0 passed 6,167 tests, skipped 3 and failed 0 across the main and isolated diagnostics runs.

- [x] Admit bounded inline VB ASAX and retain physical file spans.
- [x] Track explicit Include calls on separate unambiguous method locals and refuse uncertain ownership.

Explicit follow-ups, not acceptance claims of this discovery slice:
- [ ] Application-scoped registration/reference resolution, including duplicate declarations and startup evidence.
- [ ] Admitted external JavaScript inventory and bounded function/call/request relationships.
- [ ] Wildcards, directory includes, general variable mutation beyond admitted local Include calls, ScriptManager/Bundle.config forms and configuration-dependent selection.

Angular is out of scope and is not established as present in the motivating app.
