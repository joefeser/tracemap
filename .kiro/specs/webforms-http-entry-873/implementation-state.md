# Implementation state

Branch: codex/webforms-entry-873.
Fresh origin/dev baseline: 25c4f6475db9b92b3f08dafafd5117a554e3020a.
Issue: https://github.com/joefeser/tracemap/issues/873.

Implemented the three separately typed entry families and a retained-input report
inventory. No call traversal/root admission. Existing composition reused; missing
VB project/qualified-name registration and scan-root relative Src resolution fixed.
Ordinary VB property declarations are the positive fixture; missing C# declarations
stay unavailable. Framework witnesses cover direct signed metadata members only.

Files: docs/validation/WEBFORMS_HTTP_ENTRY_RELATIONSHIPS.md (matrix),
WEBFORMS_HTTP_ENTRY_PUBLIC_CORPUS.md (two pinned MIT static checks),
samples/http-entry-boundaries (independent fixtures), and extended
HttpEntryBoundaryRegressionTests (SQLite/combine/both readers, deterministic
reports, graph exclusion, ambiguous/scope/unsupported negatives).

Development findings retained: initial missing VB registration and root Src bug;
competing unresolved registrations; target directive support; exact graph exclusion;
framework OnInit/OnLoad may be declared on TemplateControl/UserControl/Page,
not only Control. No fake signed framework types added. Exact supported signatures
are documented. Cache evidence for the new joins without weakening the existing
external-enumeration regression. Historical version literals updated only where
asserting the current extractor.

Validation before final freeze: original HTTP 17/17; intermediate adjacent suite
163/163; relevant Python diagnostics 60/60. Broad Python discovery was an invalid
invocation of SQL command-text tests (32 setup errors, CONFIG unset); it is not a
pass. Preliminary broad .NET filter was cancelled after final review edits and is
not an acceptance receipt. Final focused/full/CLI results pending in validation-receipt.md.

No third-party app build/restore/hosting or endpoint/database execution. Corpora
remain outside the checkout. No push, PR, external reviewer or workflow mutation
authorized. Windows/private and other pinned adapter checks remain separate gates.
