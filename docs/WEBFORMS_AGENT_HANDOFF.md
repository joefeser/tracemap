# Web Forms Agent Handoff

This workflow gives an authorized Claude Code session the retained TraceMap
evidence before asking it to assess a legacy Web Forms conversion. It does not
make Claude output scanner evidence and does not permit source changes.

## Choose the allowed inputs

Use the private application workbench only inside the authorized environment.
For an identity-free discussion, provide only the alias-only outlier JSON and
its matching shareable HTML.

For the one-root pipeline, set the review root and use its fixed stage paths:

```powershell
$ReviewRoot = 'C:\work\webforms-review'
$PacketPath = Join-Path $ReviewRoot 'packet\webforms-modernization.json'
$EvidenceDocsRoot = Join-Path $ReviewRoot 'evidence-docs'
$WorkbenchRoot = Join-Path $ReviewRoot 'workbench'
$RunReceipt = Join-Path $ReviewRoot 'run-receipt.json'
$PromptPath = (Resolve-Path '.\prompts\review-webforms-modernization-evidence.md').Path

Get-Item $RunReceipt,
  $PacketPath,
  (Join-Path $EvidenceDocsRoot 'manifest.json'),
  (Join-Path $EvidenceDocsRoot 'query-recipes.json'),
  (Join-Path $EvidenceDocsRoot 'chunks.jsonl'),
  (Join-Path $WorkbenchRoot 'application-handoff.json')
```

Stop if any path is missing, if `run-receipt.json` is not completed, or if
packet/corpus/workbench provenance does not identify the same retained scan.
Do not select folders by timestamp guessing.

## Start a read-only Claude Code review

The supported copy/paste path from the TraceMap checkout is one command:

```powershell
.\scripts\Start-FocusedWebFormsClaudeReview.ps1 -ReviewRoot $ReviewRoot
```

The wrapper validates the completed receipt and required inputs, grants only
the packet, evidence-doc, and workbench directories, loads the checked-in
prompt, and starts Claude Code in plan mode. It does not grant source access.

The equivalent expanded command is:

```powershell
claude --permission-mode plan `
  --add-dir $EvidenceDocsRoot `
  --add-dir $WorkbenchRoot `
  --add-dir (Split-Path $PacketPath -Parent) `
  (Get-Content $PromptPath -Raw)
```

`--add-dir` grants Claude Code access to the three evidence directories, while
plan mode keeps the first pass focused on analysis. See Anthropic's
[Claude Code CLI reference](https://docs.anthropic.com/en/docs/claude-code/cli-usage)
and confirm the installed version with `claude --help` when necessary.

### Corporate BAT/Python launchers

If work policy requires a BAT or Python wrapper for Bedrock, proxy, or SSO,
first run the ready-to-paste
[corporate launcher inspection prompt](../prompts/guided-corporate-claude-launcher-setup.md)
inside that environment. It returns a sanitized forwarding contract without
publishing launcher code or configuration. Do not point the handoff script at a
corporate launcher until repeated arguments, plan mode, multiline prompt
transport, and the policy boundary have been verified.

For a verified transparent launcher, pass its local path:

```powershell
.\scripts\Start-FocusedWebFormsClaudeReview.ps1 `
  -ReviewRoot $ReviewRoot `
  -ClaudeLauncherPath 'C:/path/to/approved-corporate-launcher.bat'
```

The launcher path is never stored in an artifact. In this mode, the script
preserves the repeated evidence-directory arguments and plan mode, adds
`--print`, and pipes the multiline checked-in prompt through stdin to avoid BAT
quoting and Windows command-line-length hazards. The default invocation remains
the normal interactive `claude` command when `-ClaudeLauncherPath` is omitted.

Do not add the application source directory on the first pass. If the evidence
review produces a precise source question, start a separately authorized
follow-up with the minimum required source scope.

## Expected answer

The prompt asks for:

1. an evidence/provenance receipt;
2. a technology and capability inventory;
3. page cohorts and bounded conversion questions;
4. systemic extractor gaps separated from application-specific gaps;
5. a small prioritized review queue; and
6. explicit facts, inferences, unknowns, and owner questions.

The output is planning material. It is not runtime proof, a migration estimate,
an approval, or a TraceMap artifact.

Move conclusions through the separate [private review and approval
workflow](WEBFORMS_PRIVATE_REVIEW_WORKFLOW.md) before creating work items.
