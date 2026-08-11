# Site Reverse Impact and Static Dispatch Implementation State

- Branch: `codex/site-reverse-impact-dispatch`
- Base: fresh `origin/dev` at `6dd737ab9fe7a4005829cf4cb1adf304ef31ee59`
- Scope: issue #617 only; two connected site articles plus metadata, validator, tests, and this spec.
- Claim level: demo where checked-in contracts support the statement; no runtime selection, execution, production, completeness, release, or safety claims.
- Validation: focused validator 14/14; full site tests 908/908; build and validate passed with 107 HTML files, 3,604 internal references, and 106 sitemap URLs; desktop 1,440×1,000 and mobile 390×844 browser checks passed without overflow or console warnings/errors; private-path and diff checks pending commit review.
- Follow-up: #621 and #624 will be stacked as separate story PRs; child branches must never merge into this parent branch.
