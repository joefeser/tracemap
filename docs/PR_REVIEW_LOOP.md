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

Operational boundaries:

- Codex review requests are policy-controlled and bounded.
- Qodo review requests are explicit owner actions; the normal loop must not
  post `@qodo-code-review review`.
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
