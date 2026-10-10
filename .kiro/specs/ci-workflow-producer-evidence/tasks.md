# CI workflow producer evidence

- [x] Add opt-in `--index-ci-producers` (default off) with flag-off output byte-compatibility pinned by tests.
- [x] Read `.github/workflows/*.yml|*.yaml` at exactly that depth with absolute resource caps (64 files / 1 MiB per file / 8 MiB total).
- [x] Bounded structural YAML subset parser: captured `jobs.<id>.steps[*].run` blocks with line spans and literal env maps; unsupported constructs and malformed YAML are typed file-level gaps.
- [x] `dotnet pack` command analysis: `-p:`/`--property:` property flags, quoting, continuations, folded blocks, NuGet PackageVersion-over-Version precedence, project/solution target detection.
- [x] Effective identity resolution: flag id > explicit project-declared id; templated/unsafe/solution/no-target ids are `ci-producer-id-unevidenced` gaps. Versions resolve same-workflow literal env only; unresolvable templates omit the version and name the template.
- [x] Dedupe policy: same package at one effective version collapses to one intact deterministic occurrence (prefer known-version evidence); two versions are a `ci-producer-version-conflict` gap with no fact.
- [x] PackageProduced facts with sourceKind=ci-workflow / manifestKind=github-workflow / workflowPath matching the pinned upgrade-authority contract bytes.
- [x] Fingerprint binding: flag joins the scan-conditions receipt scope fingerprint (both-off keeps the legacy v1 hash) and the scan-id signature carries generator + bounded-input hashes.
- [x] Rule catalog, VALIDATION, public sample fixture and this spec updated.
- [x] Deliver bounded PR to dev and complete the Baz + Codex review loop.

- [x] Independent no-ACK P1/P2 review: preserve env shadowing, reject opaque scopes and multiline shell data, fix operator splitting/empty overrides, bind provenance and intact dedupe spans, and reject linked workflow ancestors.

- [x] Bound discovery before sorting and preserve embedded shell quote/escape semantics; reject overflow without filesystem-order-dependent partial facts.
