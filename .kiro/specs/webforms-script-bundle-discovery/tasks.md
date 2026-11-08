# Regular WebForms script-bundle discovery

- [x] Reconcile branch and existing inline behavior versus standalone JS inventory.
- [x] Retain bounded C#/VB explicit ScriptBundle.Include declarations and member order as syntax candidates.
- [x] Retain literal Scripts.Render references in ASPX/master/ASCX server output blocks.
- [x] Preserve file-local identities, provenance, refusal gaps, and deterministic output.
- [x] Add synthetic positive, negative, ambiguous, malformed and member/source limit tests.
- [x] Complete focused Debug and CLI smoke validation; record exact results.
- [x] Complete the full Debug suite on the current bundle implementation. Pinned AngularJS validation head 4344b34fc passed 6,222 tests, skipped 3 and failed 0 across the main and isolated diagnostics runs.

- [x] Admit bounded inline VB ASAX and retain physical file spans.
- [x] Track explicit Include calls on separate unambiguous method locals and refuse uncertain ownership.

Explicit follow-ups, not acceptance claims of this discovery slice:
- [x] Require a unique literal bundle declaration in the same nearest inventoried Global.asax application for AngularJS source selection.
- [ ] Extend registration and startup execution evidence beyond static source selection.
- [x] Admit .js to the WebForms source snapshot and emit candidate AngularJS click-to-external-method relationships for explicit app/controller scopes.
- [ ] Extend supported handlers through calls to HTTP/backend evidence.
- [ ] Wildcards, directory includes, general variable mutation beyond admitted local Include calls, ScriptManager/Bundle.config forms and configuration-dependent selection.

The AngularJS continuation uses its own rule and synthetic fixture. Private verification is recorded separately by the operator.
- [x] Preserve inline WebForms property initializers as unknown, retain their physical spans and require the rest of the script to parse before attaching an external handler.

- [x] Resolve bounded top-level controller chains to the included module and retain a distinct registration site for each controller.

- [x] Accept one explicit IIS mount for a master-page script and bind the exact selected inventory file, mapping provenance, scan ID and authorized scope.

- [x] Admit bounded quoted WebForms AppSettings expressions in classic IIFEs and report a gap-linked partial handler when dynamic script contents remain unknown.

- [x] Retain a partial handler and unobserved-callback gap for a captured scope assignment directly inside a controller service-call fulfillment callback.
