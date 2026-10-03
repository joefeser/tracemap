# PR Review Loop

TraceMap uses a repo-local Agent Control Kit lane config for pull-request
review readiness:

```text
.agent-control/lanes/pr-review-loop.yaml
```

The lane treats Codex and Qodo as a trusted review group. Qodo remains required
and can be requested only by an explicit owner action. The initial batch waits
for Qodo's single return. After Qodo has returned once, a later-head stale result
is terminal residual risk: ACK must not request, retry, or wait for a second
Qodo return. Exact-head Codex may then satisfy the configured fast quorum after
all findings are patched or dispositioned. Stale Codex plus stale Qodo cannot
satisfy the lane. Checks, threads, findings, merge state, risky-file gates, and
`main`/release promotion policy remain unchanged.

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
- Qodo review requests are explicit owner actions; the normal loop must not
  post `@qodo-code-review review`.
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

The one-pass Qodo lifecycle, bounded current-head Codex recovery, and trusted
local-review fallback use the immutable Agent Control Kit `v0.5.2` release
at `949f31b733de89c1019939ca07f8399c1e170d59`. Before a loop, verify the exact
checkout, stable identity, release receipt, and consumer lane. The lane requires
ACK `>=0.5.2 <0.6.0` for consolidated invariant-audit repair briefs. This example
uses the verified local release installation; set `ACK_ROOT` to the actual
release checkout and retain its matching receipt when installing elsewhere:

```bash
ACK_ROOT="$HOME/.local/share/agent-control-kit/releases/v0.5.2"
ACK_RELEASE_RECEIPT="$ACK_ROOT/.agent-control/tmp/releases/v0.5.2.json"
ACK_SHA=949f31b733de89c1019939ca07f8399c1e170d59

git -C "$ACK_ROOT" fetch origin --tags
test "$(git -C "$ACK_ROOT" rev-parse HEAD)" = "$ACK_SHA"
test "$(git -C "$ACK_ROOT" rev-parse 'v0.5.2^{commit}')" = "$ACK_SHA"
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
