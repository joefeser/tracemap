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
- [x] Require a unique literal bundle declaration in the same nearest inventoried Global.asax application for AngularJS source selection.
- [ ] Extend registration and startup execution evidence beyond static source selection.
- [x] Admit .js to the WebForms source snapshot and emit candidate AngularJS click-to-external-method relationships for explicit app/controller scopes.
- [ ] Extend supported handlers through calls to HTTP/backend evidence.
- [ ] Wildcards, directory includes, general variable mutation beyond admitted local Include calls, ScriptManager/Bundle.config forms and configuration-dependent selection.

The AngularJS continuation uses its own rule and synthetic fixture. Private verification is recorded separately by the operator.
