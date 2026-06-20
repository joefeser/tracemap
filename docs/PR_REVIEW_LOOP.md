# PR Review Loop

TraceMap uses a repo-local Agent Control Kit lane config for pull-request
review readiness:

```text
.agent-control/lanes/pr-review-loop.yaml
```

The lane requires Codex and Qodo review evidence. Both reviewers use
`waitUntilReturnedBeforeProcessing`, so the loop waits for both required
reviewers to return, or for the bounded wait policy to hand control back to the
owner, before processing partial findings.

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
default:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr <number> --base <branch> --json
```

The JSON readback should include `evidence.configSource.laneConfig` showing
whether the lane file was loaded, missing, disabled, or invalid.
