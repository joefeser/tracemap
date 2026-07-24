# PR Review Loop

TraceMap uses a repo-local Agent Control Kit lane config for pull-request
review readiness:

```text
.agent-control/lanes/pr-review-loop.yaml
```

The lane treats Codex and Qodo as a trusted review group. Qodo remains required
and can be requested only by an explicit owner action, but it is a one-pass
reviewer: a prior-head Qodo result with explicit zero finding counters may
become residual risk when exact-head required Codex is clean and the configured
fast quorum is met. Stale Codex plus stale Qodo cannot satisfy the lane. Checks,
threads, findings, merge state, risky-file gates, and `main`/release promotion
policy remain unchanged.

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

The one-pass Qodo behavior requires Agent Control Kit PR #281, merged at
`d4eeead`. The installed stable build `eeb217a` predates that fix even though it
reports the same `0.2.0` version and capabilities. Before a loop, verify the
actual build and use a built ACK checkout containing `d4eeead` or a descendant:

```bash
ACK_ROOT=../agent-control-kit
npm --prefix "$ACK_ROOT" run build
ACK_SHA=$(node "$ACK_ROOT/dist/cli.js" version --json | node -e \
  'let data="";process.stdin.on("data",c=>data+=c).on("end",()=>process.stdout.write(JSON.parse(data).gitSha))')
git -C "$ACK_ROOT" merge-base --is-ancestor d4eeead "$ACK_SHA"
node "$ACK_ROOT/dist/cli.js" doctor \
  --repo-root "$PWD" \
  --lane-config "$PWD/.agent-control/lanes/pr-review-loop.yaml" \
  --json
```

A nonzero ancestry or doctor result is a preflight failure. Do not fall back to
the older installed binary.

Run the loop from a TraceMap checkout so the repo-local lane file is loaded by
default. The command expects normal GitHub CLI authentication or a GitHub token
available to Agent Control, such as `GITHUB_TOKEN`:

```bash
node ../agent-control-kit/dist/cli.js pr-loop \
  --repo joefeser/tracemap --pr <number> --base <branch> --json
```

The JSON readback should include `evidence.configSource.laneConfig` showing
whether the lane file was loaded, missing, disabled, or invalid.

Run the consumer lane regression with:

```bash
node --test scripts/pr-review-loop-lane.test.mjs
```
