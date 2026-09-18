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
The review root may be anywhere on the workstation and does not need to be a
Git repository. By default, Claude runs from a stable private conversation
directory at `<ReviewRoot>\claude-workspace`; pass `-ConversationRoot` to put
that directory anywhere else. The wrapper stores session metadata under the
review root and session-scoped numbered prompt/response files under the conversation root so
later questions can resume the same review without depending on the TraceMap
checkout as the working directory.

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
[Claude Code CLI reference](https://code.claude.com/docs/en/cli-usage)
and confirm the installed version with `claude --help` when necessary.

## Continue the same Claude session

After the first review completes, ask follow-up questions with the continuation
wrapper. The script can be called by its full path from any directory:

```powershell
& 'C:\path\to\tracemap\scripts\Continue-FocusedWebFormsClaudeReview.ps1' `
  -ReviewRoot $ReviewRoot `
  -ClaudeLauncherPath $ClaudeLauncherPath `
  -Question 'Explain the highest-value remaining unknown.'
```

For a longer question, save it anywhere and replace `-Question` with
`-PromptPath C:\path\to\question.md`. The wrapper resumes the exact recorded
session from its original conversation directory, revalidates the receipted
evidence, preserves the same three bounded evidence grants, and saves a new
numbered prompt and response. It does not overwrite the initial assessment.

Backing up the review root and conversation root preserves TraceMap's session
metadata and saved turns. Claude's native JSONL conversation history remains
machine-local in Claude's configuration directory, so copying only the session
UUID to another machine does not make that native session portable.

### Why the resume picker can be empty

The corporate launcher uses Claude's non-interactive `--print` mode. Claude
persists those sessions, but intentionally omits them from the picker shown by
`claude --resume` with no identifier. Resume them by exact UUID instead. The
behavior is documented in Claude's
[session guide](https://code.claude.com/docs/en/sessions). The start wrapper
records that UUID, so it is not necessary to search the user profile:

```powershell
$Session = Get-Content `
  (Join-Path $ReviewRoot 'agent-reviews\claude-session.json') `
  -Raw | ConvertFrom-Json

$Session.sessionId
$Session.conversationRoot
```

`Continue-FocusedWebFormsClaudeReview.ps1` reads those values and invokes
`--resume <sessionId>` from the original conversation directory. The UUID
should also match the native JSONL filename beneath Claude's user-level
`projects` directory. For a session created before TraceMap retained the UUID,
the following diagnostic lists recent native transcripts:

```powershell
Get-ChildItem "$env:USERPROFILE\.claude\projects" `
  -Recurse -File -Filter '*.jsonl' |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 10 LastWriteTime, BaseName, FullName
```

`BaseName` is normally the session UUID, but timestamp sorting can select an
unrelated Claude run. Prefer the retained `claude-session.json` for every new
review. Persistence also requires that neither the launcher nor the environment
sets `--no-session-persistence` or `CLAUDE_CODE_SKIP_PROMPT_HISTORY`.

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
  -ClaudeLauncherPath 'C:\path\to\approved-corporate-launcher.bat'
```

The launcher path is never stored in an artifact. In this mode, the script
preserves the repeated evidence-directory arguments and plan mode, adds
`--print`, and pipes the multiline checked-in prompt through stdin to avoid BAT
quoting and Windows command-line-length hazards. The default invocation remains
the normal interactive `claude` command when `-ClaudeLauncherPath` is omitted.
The complete stdout assessment is retained at
`agent-reviews/claude-evidence-review.md` under the review root; a missing or
empty response fails closed instead of reporting a successful handoff.
Use forward slashes inside `webforms-review.jsonc`; use a native backslash path
for this PowerShell launcher parameter. The script also normalizes a drive path
that was supplied with forward slashes before checking it.

The workbench keeps coverage and truncation as separate claims. Application
analysis exposes `coverageReductionReasons` and `packetTruncationReasons`;
page analysis labels the application-wide flag with
`packetTruncationScope: application-packet` and reports page-local traversal
limits separately through `pageTraversalTruncated` and
`pageTraversalTruncationReasons`. The handoff also aggregates exact
`nextEvidenceSummary` entries from unresolved chains and publishes safe control
registration metadata in `controlRegistrationGaps`. Reviewers should use those
fields instead of inferring that reduced source analysis caused a size limit,
or recommending a generic assembly change without a retained control prefix,
type, namespace, assembly, or resolution state.

Do not add the application source directory on the first pass. If the evidence
review produces a precise source question, start a separately authorized
follow-up with the minimum required source scope:

```powershell
.\scripts\Start-FocusedWebFormsClaudeSourceReview.ps1 `
  -ReviewRoot $ReviewRoot `
  -SourceRoot $SourceRoot `
  -SourceRelativePath @('Pages/Selected.aspx', 'Pages/Selected.aspx.vb') `
  -ClaudeLauncherPath 'C:\path\to\approved-corporate-launcher.bat'
```

This separate command accepts 1–12 explicit relative files under one source
root, permits only Web Forms markup and C#/VB source extensions, caps each file
at 2 MiB and the selection at 8 MiB, stages aliased copies in a temporary
directory, and deletes that directory after Claude exits. It never grants the
whole source root. Its complete response is retained at
`agent-reviews/claude-selected-source-review.md`. Source-assisted conclusions
remain separate from compiler-resolved TraceMap evidence.

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
