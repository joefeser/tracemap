# Implementation state

Branch: `codex/integrate-main-into-dev`; target: `dev`.

Scope: owner-requested reconciliation of main site work and dev engine/evidence
work, with history and functionality audit. No protected branch has been changed.
The integration merge is `4f1a4b03aabc488197af121777d1addc9bb066a0`.

Evidence, exact parent pins, acceptance matrix, validation commands and limitations
are in [the reconciliation audit](../../../docs/validation/MAIN_DEV_RECONCILIATION.md).
There were no overlapping changed paths or merge conflicts. The merge tree is the
exact 2,717-entry union. The #802 reset tree equals the #801 promotion tree.

Repairs beyond the union: rule-catalog PackageVersion precedence wording,
already covered by the existing produced-package regression, and explicit sample
restore in both public smoke scripts plus real ASP.NET references in their
positive endpoint fixture. Restore alone did not repair the failed connected-path
assertion; the properly referenced fixture now passes the complete public demo.
No engine behavior or evidence guard was changed.

Site tests/build/validation, CLI scan/artifact validation, artifact-validator tests,
privacy guard, desktop/mobile checks and all three non-.NET local suites passed.
The Python endpoint smoke retained its expected reduced coverage. The initial
full .NET run overlapped CLI rebuilds and failed two Web Forms cases; all six
focused cases pass in isolation. The complete isolated suite then passed
3,432/0/1 at `001f1de3` in 19m40s; hosted .NET independently passed. Full demo and clean-assets demo/combined-smoke
runs pass. See the audit for the diagnostic limits.

Review repair adds claim-cell/metadata agreement, explicit unresolved-base gaps
and LF checkout preservation for the exact-hashed site generator. Published
ancestry was independently verified through GitHub; both source pins are parents
of the integration merge. The existing alias-input rejection remains intact.

Delivery requires current-head public CI and live ACK. Their final results belong in the integration PR, which is the delivery
status authority; this note is the implementation checkpoint. Pinned OSS smokes
and private Windows/MSVC work remain explicitly deferred. Preserve sibling
worktrees and the primary checkout's unrelated edits.
