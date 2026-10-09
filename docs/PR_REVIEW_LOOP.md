# PR Review Loop

TraceMap uses a repo-local Agent Control Kit lane config for pull-request
review readiness:

```text
.agent-control/lanes/pr-review-loop.yaml
```

The lane requires Codex and trusted exact-head Baz review evidence before partial
findings are processed. Qodo is disabled: do not request or await it. ACK observes
Baz reviews automatically; it never requests Baz. Numeric quorum alone cannot
release a batch while the trusted Baz return is missing. Stale Baz does not count
as exact-head evidence. Baz has at most two finding/fix cycles; clean verification
at the ceiling remains permitted. Checks, threads, findings, merge state,
risky-file gates and main/release promotion policy remain unchanged.

After `FRESH_REVIEW_FIX_CYCLE_CEILING_REACHED`, the trusted lane may use the
configured `claude-local` reviewer as a bounded fallback. This is a read-only
Claude Opus 4.8 review whose artifact must prove the exact head, actual model,
complete coverage, and a mutation-free worktree. Only `joefeser` may admit the
result as an `owner_authorized_receipt` in the `trustedCodeReview` quorum. The
receipt does not bypass checks, unresolved threads, findings, merge state,
risky-file gates, or branch policy, and it cannot replace a hosted reviewer
that never returned at least once.

The fallback is bounded to two durable attempts and two fix cycles. Each
provider invocation has a 30-minute timeout and a $4 ceiling, so the aggregate
authorized spend is at most $8. A finding-bearing receipt follows the ordinary
patch/disposition workflow; a changed head requires a new exact-head receipt.
`main` remains human-mediated even when ACK returns `merge_ready`.

This policy is authorized only when the same effective fallback contract is
already present at the same lane path on the trusted target base. A PR cannot
authorize its own fallback from head-only configuration. Therefore the first
lane-authorization PR must be reviewed and merged manually before later PRs
can use the fallback.

Operational boundaries:

- Codex review requests are policy-controlled and bounded.
- Qodo is retired and disabled. Do not request or await it.
- Baz re-reviews automatically after pushes; no manual trigger is sent.
- Automatic local review is Claude-only, read-only, exact-head, and available
  only after the configured Codex freshness ceiling.
- During a typed hosted-review failure or non-return, Joe may explicitly invoke
  the same trusted-base fallback with `--owner-authorized-local-review`; this
  flag does not retag Codex or Qodo.
- `main`, `master`, and `release/**` are not overnight auto-merge targets.
- `dev`, `integration/**`, and `feature/**` may be owner-override eligible only
  when the mechanical gates are clean.
- Merge-commit readback is the default; squash merge requires separate owner
  approval.

The trusted Baz batch, bounded Codex recovery, and local-review fallback require
verified capabilities. This example uses immutable Agent Control Kit `v0.5.5`
at `ab398330c03fbe34b7fdd600efa9698adf003a67`. Before a loop, verify the exact
checkout, stable identity, release receipt, and consumer lane. The lane requires
ACK `>=0.5.2 <0.6.0` plus trustedHostedReviewerQuorum and the other declared capabilities; version range alone is insufficient. This example
uses the verified local release installation; set `ACK_ROOT` to the actual
release checkout and retain its matching receipt when installing elsewhere:

```bash
ACK_ROOT="$HOME/.local/share/agent-control-kit/releases/v0.5.5"
ACK_RELEASE_RECEIPT="$ACK_ROOT/.agent-control/tmp/releases/v0.5.5.json"
ACK_SHA=ab398330c03fbe34b7fdd600efa9698adf003a67

git -C "$ACK_ROOT" fetch origin --tags
test "$(git -C "$ACK_ROOT" rev-parse HEAD)" = "$ACK_SHA"
test "$(git -C "$ACK_ROOT" rev-parse 'v0.5.5^{commit}')" = "$ACK_SHA"
npm --prefix "$ACK_ROOT" run build
node "$ACK_ROOT/dist/cli.js" version --json
node "$ACK_ROOT/dist/cli.js" release verify \
  --repo-root "$ACK_ROOT" \
  --receipt "$ACK_RELEASE_RECEIPT" \
  --json
node "$ACK_ROOT/dist/cli.js" doctor \
  --repo-root "$PWD" \
  --lane-config "$PWD/.agent-control/lanes/pr-review-loop.yaml" \
  --json
```

A missing exact tag or receipt, a non-`release_ready` release verification, or a
nonzero doctor result is a preflight failure. Do not fall back to a mutable dev
checkout, an older installed binary, or a prerelease build.

Run the loop from a TraceMap checkout so the repo-local lane file is loaded by
default. The command expects normal GitHub CLI authentication or a GitHub token
available to Agent Control, such as `GITHUB_TOKEN`:

```bash
node "$ACK_ROOT/dist/cli.js" pr-loop \
  --repo joefeser/tracemap --pr <number> --base <branch> --quiet --json-decision
```

Read the named patch brief or handoff for full evidence. Use full `--json` when
you need `evidence.configSource.laneConfig` to inspect whether the lane was
loaded, missing, disabled, or invalid.

After ACK authorizes patching a settled review batch, read the whole patch brief
and its `invariantAudit` plan. Verify the shared invariant against repository
evidence, include demonstrated sibling defects, and validate a regression
matrix before one consolidated patch and push. Settle each covered finding
individually and rerun ACK on the new head. The plan grants no extra scope,
review budget, reviewer requests, or merge authority.

Run the consumer lane regression with:

```bash
node --test scripts/pr-review-loop-lane.test.mjs
```

### Pinned consumer regression

After verifying the pinned ACK installation, run:

```sh
ACK_ROOT=/path/to/verified/ack-v0.5.5 node --test scripts/pr-review-loop-consumer.test.mjs
```

This uses ACK 0.5.5's actual packet loader with an explicit repository root and
lane path. Git-backed loading must establish repo-local committed provenance;
missing, altered, external, untracked and overlay configurations are rejected.
The resulting packet then enters ACK's offline simulator. Missing and
stale Baz cannot release a Codex-clean batch; current Baz can. Disabled Qodo is
neither awaited nor requested by the loop. The test requires the pinned release
and does not install software, call GitHub or invoke local reviewers. It covers
pr-loop policy, not manual use of the separate request-review CLI: operators
must still never explicitly request retired Qodo. Keep the lightweight lane-text
tests as configuration assertions, not substitutes for consumer behavior.
