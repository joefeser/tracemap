# PR Review Loop

TraceMap uses a repo-local Agent Control Kit lane config for pull-request
review readiness:

```text
.agent-control/lanes/pr-review-loop.yaml
```

The lane treats Codex and Qodo as a trusted review group. Both reviewers are
preferred, but the `dev` lane may proceed after the configured wait when at
least one trusted reviewer has returned and all mechanical gates are clean.
Missing reviewers remain residual risk for `dev`; `main`, `master`, and
`release/**` remain human-mediated promotion targets.

Operational boundaries:

- Codex review requests are policy-controlled and bounded.
- Qodo review requests are explicit owner actions; the normal loop must not
  post `@qodo-code-review review`.
- `main`, `master`, and `release/**` are not overnight auto-merge targets.
- `dev`, `integration/**`, and `feature/**` may be owner-override eligible only
  when the mechanical gates are clean.
- Merge-commit readback is the default; squash merge requires separate owner
  approval.

Run the loop from a TraceMap checkout so the repo-local lane file is loaded by
default. The command expects normal GitHub CLI authentication or a GitHub token
available to Agent Control, such as `GITHUB_TOKEN`:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr <number> --base <branch> --json
```

The JSON readback should include `evidence.configSource.laneConfig` showing
whether the lane file was loaded, missing, disabled, or invalid.
