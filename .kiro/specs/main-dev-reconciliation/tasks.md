# Main/dev reconciliation tasks

- [x] Pin fetched main/dev and merge base; inspect active worktrees without touching unrelated work.
- [x] Audit changed paths, reset ancestry and patch-equivalent commits.
- [x] Create an isolated main-based integration branch and merge pinned dev.
- [x] Independently verify the merge tree equals the complete union.
- [x] Correct proven PackageVersion rule-catalog documentation drift.
- [x] Give the positive endpoint fixture real ASP.NET references and restore its assets in both public smoke scripts.
- [x] Verify complete public demo and combined-path smoke with sample assets absent and unchanged assertions.
- [x] Validate site, public CLI artifacts and desktop/mobile pages.

Delivery gates: complete isolated local .NET success, final-head public CI and
live ACK review. Record these results in the integration PR; a draft or a
committed audit alone does not satisfy them.

Protected-branch merge, private Windows validation and further .NET features are
outside this implementation task.
