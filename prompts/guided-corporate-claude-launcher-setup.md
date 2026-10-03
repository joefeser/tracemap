# Inspect a corporate Claude launcher for safe argument forwarding

Use this prompt only inside the authorized work environment. The launcher may
contain private Bedrock, proxy, SSO, certificate, account, or endpoint settings.
Do not reproduce any of those values in the answer.

## Ready-to-paste prompt

```text
Inspect the corporate Claude launcher that I identify locally. It consists of a
Windows BAT or CMD wrapper around a Python script, which eventually starts the
approved Claude executable through our corporate Bedrock/proxy/SSO setup.

Do not modify any files yet. Do not reveal or repeat credentials, tokens,
internal hostnames, URLs, account IDs, role names, certificate paths, user
names, repository paths, environment-variable values, or proprietary source.
Refer to components only as BAT launcher, Python launcher, approved Claude
executable, corporate proxy, or corporate authentication.

Determine and report:

1. Whether the BAT/CMD launcher currently forwards all arguments with `%*`.
2. Whether the Python launcher receives the forwarded arguments without
   consuming, reordering, joining, or reparsing unknown Claude CLI arguments.
3. The exact argument path from BAT/CMD argv, through Python argv/argument
   parsing, to the final process invocation.
4. Whether the final invocation uses an argument array with `shell=False` or an
   equivalent boundary that preserves arguments without command-string
   interpolation.
5. Whether repeated flags and their values remain ordered, specifically:
   `--add-dir <path> --add-dir <path> --add-dir <path>`.
6. Whether `--permission-mode plan` reaches the approved Claude executable
   unchanged.
7. Whether a large multiline prompt can be the final argument without damage
   from BAT quoting, PowerShell quoting, Python parsing, Windows command-line
   length limits, percent expansion, delayed expansion, or newline handling.
8. If a multiline argument is unsafe, which already-supported mechanism is
   safest: stdin, a prompt file read by the PowerShell caller, or another
   documented launcher input. Do not invent an unsupported Claude CLI flag.
9. The exact PowerShell invocation contract another script should use. Express
   private paths as placeholders, for example:
   `& '<corporate-launcher>' @ClaudeArguments`.
10. The smallest BAT and Python changes required to support transparent
    forwarding, if forwarding is not already correct. Show only minimal,
    sanitized pseudocode or a patch with all private values replaced by
    placeholders.
11. Whether the launcher can accept an explicit executable or launcher path
    from a PowerShell parameter without bypassing corporate authentication,
    policy, logging, or proxy configuration.

Validate the proposed contract locally with harmless arguments only. Do not
send application evidence or source during this test. Include results for:

- one flag/value pair;
- three repeated `--add-dir` pairs containing spaces;
- `--permission-mode plan`;
- a short prompt containing quotes and `%`;
- a multiline prompt, or a clear explanation that it cannot be passed safely.

Return only this sanitized structure:

launcherAssessment=<transparent|partial|not-forwarding|unknown>
batForwardsAllArguments=<true|false|unknown>
pythonPreservesUnknownArguments=<true|false|unknown>
repeatedFlagsPreserved=<true|false|unknown>
planModePreserved=<true|false|unknown>
multilinePromptTransport=<argument|stdin|prompt-file|unsupported|unknown>
recommendedPowerShellContract=<sanitized single-line example>
minimalChangeNeeded=<none|bat|python|bat-and-python|unknown>
policyBoundaryPreserved=<true|false|unknown>
limitations=<sanitized comma-separated limitations>

After that block, provide a short sanitized explanation and the minimal
proposed pseudocode or patch if a change is required. Stop and ask me for the
specific launcher files if they have not been identified locally. Do not search
unrelated drives or directories.
```

Bring the sanitized result block and any placeholder-only patch back to the
TraceMap maintainer. Do not copy the corporate launcher, Python source, or its
configuration outside the authorized environment unless it has been separately
reviewed and approved for disclosure.
