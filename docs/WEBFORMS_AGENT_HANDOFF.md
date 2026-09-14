# Web Forms Agent Handoff

This workflow gives an authorized Claude Code session the retained TraceMap
evidence before asking it to assess a legacy Web Forms conversion. It does not
make Claude output scanner evidence and does not permit source changes.

## Choose the allowed inputs

Use the private application workbench only inside the authorized environment.
For an identity-free discussion, provide only the alias-only outlier JSON and
its matching shareable HTML.

Set the exact paths printed by the generating commands:

```powershell
$PacketPath = 'C:\work\tracemap-output\webforms-page-list-<packet>\webforms-modernization.json'
$EvidenceDocsRoot = 'C:\work\tracemap-output\evidence-docs-<run>'
$WorkbenchRoot = 'C:\work\tracemap-output\webforms-application-workbench-<run>'
$PromptPath = (Resolve-Path '.\prompts\review-webforms-modernization-evidence.md').Path

Get-Item $PacketPath,
  (Join-Path $EvidenceDocsRoot 'manifest.json'),
  (Join-Path $EvidenceDocsRoot 'query-recipes.json'),
  (Join-Path $EvidenceDocsRoot 'chunks.jsonl'),
  (Join-Path $WorkbenchRoot 'application-handoff.json')
```

Stop if any path is missing or if packet/corpus/workbench provenance does not
identify the same retained scan. Do not select folders by timestamp guessing.

## Start a read-only Claude Code review

From the TraceMap checkout:

```powershell
claude --permission-mode plan `
  --add-dir $EvidenceDocsRoot `
  --add-dir $WorkbenchRoot `
  --add-dir (Split-Path $PacketPath -Parent) `
  (Get-Content $PromptPath -Raw)
```

`--add-dir` grants Claude Code access to the three evidence directories, while
plan mode keeps the first pass focused on analysis. These flags are documented
in Anthropic's Claude Code CLI reference; confirm them with `claude --help` on
the work machine if its installed version differs.

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

