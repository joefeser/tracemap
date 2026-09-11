[CmdletBinding()]
param(
    [string]$PacketPath = '',
    [string]$OutputRoot = '',
    [string]$OutputDirectory = '',
    [string]$EvidenceDocsRoot = '',
    [string]$ReviewPath = '',
    [string]$SourceRoot = '',
    [switch]$IncludeRawSource,
    [ValidateRange(0, 50)]
    [int]$SourceContextLines = 4,
    [string]$ConfigPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-HtmlText([object]$Value) { [Net.WebUtility]::HtmlEncode([string]$Value) }
function Values([object]$Value) { if ($null -eq $Value) { @() } else { @($Value) } }
function New-StableAlias([string]$Prefix, [int]$Number) { '{0}-{1:d3}' -f $Prefix, $Number }
function Same([object]$Left, [object]$Right) { [string]::Equals([string]$Left, [string]$Right, [StringComparison]::Ordinal) }
function Get-PathStringComparison([string]$ExistingRoot) {
    if ($IsWindows) { return [StringComparison]::OrdinalIgnoreCase }
    $probeName = '.tracemap-case-probe-' + [Guid]::NewGuid().ToString('N') + '-a'
    $probePath = Join-Path $ExistingRoot $probeName
    try {
        [IO.File]::WriteAllText($probePath, '', [Text.UTF8Encoding]::new($false))
        $alternatePath = Join-Path $ExistingRoot $probeName.ToUpperInvariant()
        if (Test-Path -LiteralPath $alternatePath -PathType Leaf) { return [StringComparison]::OrdinalIgnoreCase }
        return [StringComparison]::Ordinal
    }
    finally {
        if (Test-Path -LiteralPath $probePath) { Remove-Item -LiteralPath $probePath -Force }
    }
}
function Assert-NoLinkedOutputAncestor([string]$Root, [string]$Parent) {
    $relativeParent = [IO.Path]::GetRelativePath($Root, $Parent)
    if ($relativeParent -eq '.') { return }
    $cursor = $Root
    foreach ($segment in @($relativeParent.Split([IO.Path]::DirectorySeparatorChar, [StringSplitOptions]::RemoveEmptyEntries))) {
        $cursor = Join-Path $cursor $segment
        if (!(Test-Path -LiteralPath $cursor)) { break }
        if ((Get-Item -LiteralPath $cursor -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
            throw 'ApplicationWorkbenchOutputOutsideRoot'
        }
    }
}
function Project-Evidence([object]$Evidence) {
    if ($null -eq $Evidence) { return $null }
    return [ordered]@{
        factId = [string]$Evidence.factId
        ruleId = [string]$Evidence.ruleId
        evidenceTier = [string]$Evidence.evidenceTier
        coverageLabel = [string]$Evidence.coverageLabel
        commitSha = [string]$Evidence.commitSha
        filePath = $Evidence.filePath
        startLine = $Evidence.startLine
        endLine = $Evidence.endLine
        extractorId = $Evidence.extractorId
        extractorVersion = $Evidence.extractorVersion
    }
}
function Project-PathEvidence([object]$Evidence) {
    if ($null -eq $Evidence) { return $null }
    return [ordered]@{
        evidenceId = [string]$Evidence.evidenceId
        ruleId = [string]$Evidence.ruleId
        evidenceKind = [string]$Evidence.evidenceKind
        evidenceTier = [string]$Evidence.evidenceTier
        coverageLabel = [string]$Evidence.coverageLabel
        commitSha = [string]$Evidence.commitSha
        filePath = $Evidence.filePath
        startLine = $Evidence.startLine
        endLine = $Evidence.endLine
        extractorId = $Evidence.extractorId
        extractorVersion = $Evidence.extractorVersion
        supportingFactIds = @(Values $Evidence.supportingFactIds)
    }
}
function Property-Value([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Evidence-List([object[]]$Evidence) {
    if ($Evidence.Count -eq 0) { return '<p class="muted">No retained evidence rows in this packet.</p>' }
    $rows = foreach ($item in $Evidence) {
        $path = ConvertTo-HtmlText $item.filePath
        $span = if ($null -ne $item.startLine) { "L$($item.startLine)-$($item.endLine)" } else { 'span unavailable' }
        '<li><code>{0}</code> <code>{1}</code> <code>{2}</code> — {3}:{4}</li>' -f (ConvertTo-HtmlText $item.factId), (ConvertTo-HtmlText $item.ruleId), (ConvertTo-HtmlText $item.evidenceTier), $path, (ConvertTo-HtmlText $span)
    }
    '<ul>{0}</ul>' -f ($rows -join '')
}

function Path-Evidence-List([object[]]$Evidence) {
    if ($Evidence.Count -eq 0) { return '<p class="muted">No retained path-location rows in this packet.</p>' }
    $rows = foreach ($item in $Evidence) {
        $span = if ($null -ne $item.startLine) { "L$($item.startLine)-$($item.endLine)" } else { 'span unavailable' }
        '<li><code>{0}</code> <code>{1}</code> <code>{2}</code> — {3}:{4}</li>' -f (ConvertTo-HtmlText $item.evidenceId), (ConvertTo-HtmlText $item.ruleId), (ConvertTo-HtmlText $item.evidenceKind), (ConvertTo-HtmlText $item.filePath), (ConvertTo-HtmlText $span)
    }
    '<ul>{0}</ul>' -f ($rows -join '')
}

function Source-Excerpt([object]$Evidence, [string]$Root, [int]$Context) {
    if (!$Root -or $null -eq $Evidence -or !$Evidence.filePath -or $null -eq $Evidence.startLine) { return '' }
    $relative = ([string]$Evidence.filePath).Replace('/', [IO.Path]::DirectorySeparatorChar).Replace('\', [IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)') { return '<p class="warning">Source path was not safe to resolve.</p>' }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if ((Get-Item -LiteralPath $rootPath -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { return '<p class="warning">Source root is a symlink or junction and was not read.</p>' }
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootPath $relative))
    if (!$candidate.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        !(Test-Path -LiteralPath $candidate -PathType Leaf)) { return '<p class="warning">Working-tree source was unavailable.</p>' }
    $cursor = $rootPath
    foreach ($segment in @($relative.Split([IO.Path]::DirectorySeparatorChar, [StringSplitOptions]::RemoveEmptyEntries))) {
        $cursor = Join-Path $cursor $segment
        if ((Get-Item -LiteralPath $cursor -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { return '<p class="warning">Working-tree source crossed a symlink or junction and was not read.</p>' }
    }
    $file = Get-Item -LiteralPath $candidate
    if ($file.Length -gt 4MB) { return '<p class="warning">Working-tree source exceeded the 4 MiB excerpt bound.</p>' }
    $lines = [IO.File]::ReadAllLines($candidate)
    $first = [Math]::Max(1, [int]$Evidence.startLine - $Context)
    $last = [Math]::Min($lines.Count, [int]$Evidence.endLine + $Context)
    if ($last -lt $first) { return '' }
    $selected = for ($line = $first; $line -le $last; $line++) { '{0,6}  {1}' -f $line, $lines[$line - 1] }
    '<pre><code>{0}</code></pre>' -f (ConvertTo-HtmlText ($selected -join "`n"))
}

if (!$ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'Run-FocusedWebFormsPageList.json' }
if ((!$OutputRoot -or !$PacketPath) -and (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    . (Join-Path $PSScriptRoot 'webforms-review/FocusedWebFormsConfig.ps1')
    $config = Read-FocusedWebFormsConfig -ConfigPath $ConfigPath
    if (!$OutputRoot) { $OutputRoot = $config.OutputRoot }
}
if (!$OutputRoot) { throw 'ApplicationWorkbenchOutputRootRequired' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (!(Test-Path -LiteralPath $OutputRoot -PathType Container)) { [IO.Directory]::CreateDirectory($OutputRoot) | Out-Null }

if (!$PacketPath) {
    $latest = Get-ChildItem -LiteralPath $OutputRoot -File -Recurse -Filter 'webforms-modernization.json' |
        Where-Object { $_.FullName -match '[\\/]webforms-page-list-[^\\/]+[\\/]webforms-modernization\.json$' } |
        Sort-Object LastWriteTimeUtc, FullName -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'ApplicationWorkbenchPacketUnavailable' }
    $PacketPath = $latest.FullName
}
if (!(Test-Path -LiteralPath $PacketPath -PathType Leaf)) { throw 'ApplicationWorkbenchPacketUnavailable' }
$packetFile = Get-Item -LiteralPath $PacketPath
if ($packetFile.Length -le 0 -or $packetFile.Length -gt 128MB) { throw 'ApplicationWorkbenchPacketLimit' }
$packet = [IO.File]::ReadAllText($packetFile.FullName) | ConvertFrom-Json -Depth 100
if ($packet.schemaVersion -ne 'webforms-modernization-packet.v1') { throw 'ApplicationWorkbenchPacketSchemaMismatch' }
$sources = @(Values $packet.sources)
if ($sources.Count -ne 1 -or !$sources[0].scanId -or !$sources[0].commitSha) { throw 'ApplicationWorkbenchPacketProvenanceMismatch' }
$surfaces = @(Values $packet.surfaces)
if ($surfaces.Count -lt 1 -or $surfaces.Count -gt 1000) { throw 'ApplicationWorkbenchSurfaceLimit' }
if ($IncludeRawSource -and (!$SourceRoot -or !(Test-Path -LiteralPath $SourceRoot -PathType Container))) { throw 'ApplicationWorkbenchSourceRootUnavailable' }
$reviewBySurface = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
if ($ReviewPath) {
    if (!(Test-Path -LiteralPath $ReviewPath -PathType Leaf)) { throw 'ApplicationWorkbenchReviewUnavailable' }
    $reviewValidator = Join-Path $PSScriptRoot 'webforms-review/Invoke-WitsApplicationReview.ps1'
    & $reviewValidator -Mode Validate -PacketPath $packetFile.FullName -ReviewPath $ReviewPath | Out-Null
    $review = [IO.File]::ReadAllText([IO.Path]::GetFullPath($ReviewPath)) | ConvertFrom-Json -Depth 40
    foreach ($decision in @($review.decisions)) { $reviewBySurface.Add([string]$decision.surfaceId, $decision) }
}
if ($EvidenceDocsRoot) {
    if (!(Test-Path -LiteralPath $EvidenceDocsRoot -PathType Container)) { throw 'ApplicationWorkbenchCorpusUnavailable' }
    foreach ($name in @('manifest.json', 'query-recipes.json', 'chunks.jsonl')) {
        $required = Join-Path $EvidenceDocsRoot $name
        if (!(Test-Path -LiteralPath $required -PathType Leaf) -or (Get-Item -LiteralPath $required).Length -le 0) { throw 'ApplicationWorkbenchCorpusUnavailable' }
    }
}
$validatorProject = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/RawWebFormsEvidence.csproj'
& dotnet build $validatorProject -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'ApplicationWorkbenchValidatorBuildFailed' }
$validatorDll = Join-Path $PSScriptRoot 'diagnostics/RawWebFormsEvidence/bin/Release/net10.0/RawWebFormsEvidence.dll'
& dotnet $validatorDll '--validate-application-workbench-inputs' $packetFile.FullName $(if ($EvidenceDocsRoot) { [IO.Path]::GetFullPath($EvidenceDocsRoot) } else { '-' })
if ($LASTEXITCODE -ne 0) { throw 'ApplicationWorkbenchInputValidationFailed' }

$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
if (!$OutputDirectory) { $OutputDirectory = Join-Path $OutputRoot "webforms-application-workbench-$stamp" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$outputPrefix = $OutputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$outputComparison = Get-PathStringComparison $OutputRoot
if (!$OutputDirectory.StartsWith($outputPrefix, $outputComparison)) { throw 'ApplicationWorkbenchOutputOutsideRoot' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'ApplicationWorkbenchOutputExists' }
$parent = Split-Path -Parent $OutputDirectory
Assert-NoLinkedOutputAncestor $OutputRoot $parent
if (!(Test-Path -LiteralPath $parent -PathType Container)) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
$staging = Join-Path $parent ('.' + (Split-Path -Leaf $OutputDirectory) + '.staging-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($staging) | Out-Null

try {
    [IO.File]::Copy($packetFile.FullName, (Join-Path $staging 'webforms-modernization.snapshot.json'), $false)
    $ordered = @($surfaces | Sort-Object @{ Expression = { [string]$_.evidence.filePath } }, @{ Expression = { [string]$_.surfaceId } })
    $pageRows = [Collections.Generic.List[object]]::new()
    $applicationPages = [Collections.Generic.List[object]]::new()
    $associatedGapIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $ordinal = 0
    foreach ($surface in $ordered) {
        $ordinal++
        $pageId = New-StableAlias 'page' $ordinal
        $chains = @(Values $packet.eventChains | Where-Object { Same $_.surfaceId $surface.surfaceId } | Sort-Object chainId)
        $chainIds = @($chains | ForEach-Object { [string]$_.chainId })
        $boundaries = @(Values $packet.downstreamBoundaries | Where-Object { Same $_.surfaceId $surface.surfaceId } | Sort-Object boundaryId)
        $identity = @(Values $packet.identityStateInventory | Where-Object { $null -ne $_.surfaceId -and (Same $_.surfaceId $surface.surfaceId) } | Sort-Object identityStateId)
        $projectBatchCount = @(Values $packet.batchDataMovementInventory | Where-Object { $null -ne $_.projectId -and (Same $_.projectId $surface.projectId) }).Count
        $candidates = @(Values $packet.structuralSliceCandidates | Where-Object { @($_.surfaceIds | Where-Object { Same $_ $surface.surfaceId }).Count -gt 0 } | Sort-Object candidateId)
        $pageIdentity = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($value in @($surface.surfaceId, $surface.evidence.factId) + @(Values $surface.supportingFactIds) + @(Values $surface.controlIds)) { if ($value) { [void]$pageIdentity.Add([string]$value) } }
        foreach ($chain in $chains) { foreach ($value in @($chain.chainId, $chain.bindingFactId, $chain.handlerId, $chain.handlerFactId, $chain.legacyPathId) + @(Values $chain.supportingFactIds) + @(Values $chain.supportingEdgeIds)) { if ($value) { [void]$pageIdentity.Add([string]$value) } } }
        foreach ($item in @($boundaries + $identity + $candidates)) {
            foreach ($property in @('boundaryId','identityStateId','batchDataMovementId','candidateId','terminalEvidenceId')) {
                $propertyValue = Property-Value $item $property
                if ($propertyValue) { [void]$pageIdentity.Add([string]$propertyValue) }
            }
            foreach ($value in @(Values (Property-Value $item 'supportingFactIds')) + @(Values (Property-Value $item 'supportingEdgeIds'))) {
                if ($value) { [void]$pageIdentity.Add([string]$value) }
            }
        }
        $evidence = @($surface.evidence) + @(Values $surface.supportingEvidence) + @($chains | ForEach-Object { Values $_.evidence }) + @($boundaries | ForEach-Object { Values $_.evidence }) + @($identity | ForEach-Object { $_.evidence }) + @($candidates | ForEach-Object { Values $_.evidence })
        $evidence = @($evidence | Where-Object { $null -ne $_ } | Sort-Object factId, filePath, startLine -Unique)
        $pathEvidence = @($chains | ForEach-Object { Values $_.pathEvidence }) + @($boundaries | ForEach-Object { Values $_.pathEvidence })
        $pathEvidence = @($pathEvidence | Where-Object { $null -ne $_ } | Sort-Object evidenceId, filePath, startLine -Unique)
        foreach ($item in @($evidence + $pathEvidence)) {
            foreach ($property in @('factId','evidenceId')) {
                $propertyValue = Property-Value $item $property
                if ($propertyValue) { [void]$pageIdentity.Add([string]$propertyValue) }
            }
            foreach ($value in @(Values (Property-Value $item 'supportingFactIds')) + @(Values (Property-Value $item 'supportingEdgeIds'))) {
                if ($value) { [void]$pageIdentity.Add([string]$value) }
            }
        }
        $gaps = @(Values $packet.gaps | Where-Object { $null -ne $_.scopeId -and $pageIdentity.Contains([string]$_.scopeId) } | Sort-Object gapId)
        foreach ($gap in $gaps) { [void]$associatedGapIds.Add([string]$gap.gapId) }
        $handlers = @($chains | ForEach-Object { if ($_.handlerSymbol) { [string]$_.handlerSymbol } elseif ($_.handlerId) { [string]$_.handlerId } } | Where-Object { $_ } | Sort-Object -Unique)
        $coverage = @($chains | ForEach-Object { Values $_.coverageLabels } | Sort-Object -Unique)
        if ($coverage.Count -eq 0) { $coverage = @([string]$surface.evidence.coverageLabel) }
        $decision = if ($reviewBySurface.ContainsKey([string]$surface.surfaceId)) { $reviewBySurface[[string]$surface.surfaceId] } else { [pscustomobject]@{ verdict = 'unreviewed'; migrationDisposition = 'unassigned'; capabilityLabel = $null; comment = $null; correction = $null } }
        $analysisStatus = if ([string]$packet.coverage -like 'reduced-*' -or $packet.summary.truncated -or $gaps.Count -gt 0) { 'partial' } else { 'complete' }
        $retrievalHints = [Collections.Generic.List[object]]::new()
        $retrievalHints.Add([ordered]@{ recipeId = 'webforms-surface-facts'; parameters = [ordered]@{ surface_id = [string]$surface.surfaceId; limit = 500 } })
        foreach ($handler in $handlers) { $retrievalHints.Add([ordered]@{ recipeId = 'calls-from-handler'; parameters = [ordered]@{ handler_symbol = $handler; limit = 500 } }) }
        foreach ($boundary in @($boundaries | Where-Object { (Property-Value $_ 'terminalEvidenceIsFact') -eq $true })) { $retrievalHints.Add([ordered]@{ recipeId = 'boundary-supporting-facts'; parameters = [ordered]@{ terminal_evidence_id = [string]$boundary.terminalEvidenceId; limit = 100 } }) }
        $handoff = [ordered]@{
            schemaVersion = 'webforms-application-page-handoff.v1'
            ruleId = 'diagnostic.webforms.application-page-handoff.v1'
            claimLevel = 'local-only'
            pageId = $pageId
            packet = [ordered]@{ packetId = [string]$packet.packetId; scanId = [string]$sources[0].scanId; commitSha = [string]$sources[0].commitSha }
            subject = [ordered]@{ surfaceId = [string]$surface.surfaceId; surfaceKind = [string]$surface.surfaceKind; projectId = [string]$surface.projectId; filePath = [string]$surface.evidence.filePath }
            analysis = [ordered]@{ status = $analysisStatus; coverage = [string]$packet.coverage; packetTruncated = [bool]$packet.summary.truncated }
            counts = [ordered]@{ controls = @(Values $surface.controlIds).Count; eventChains = $chains.Count; boundaries = $boundaries.Count; identityState = $identity.Count; projectDataMovement = $projectBatchCount; structuralCandidates = $candidates.Count; gaps = $gaps.Count }
            eventChains = @($chains | ForEach-Object { [ordered]@{
                chainId = [string]$_.chainId; eventSourceId = [string]$_.eventSourceId; bindingFactId = [string]$_.bindingFactId
                handlerId = [string]$_.handlerId; handlerFactId = [string]$_.handlerFactId; handlerSymbol = $_.handlerSymbol
                classification = [string]$_.classification; legacyPathId = $_.legacyPathId; terminalKind = $_.terminalKind
                traversalStopState = $_.traversalObservation.stopState
                evidence = @((Values $_.evidence) | ForEach-Object { Project-Evidence $_ })
                pathEvidence = @((Values $_.pathEvidence) | ForEach-Object { Project-PathEvidence $_ })
                supportingFactIds = @(Values $_.supportingFactIds); supportingEdgeIds = @(Values $_.supportingEdgeIds)
            } })
            downstreamBoundaries = @($boundaries | ForEach-Object { [ordered]@{
                boundaryId = [string]$_.boundaryId; chainId = [string]$_.chainId; handlerId = [string]$_.handlerId
                boundaryCategory = [string]$_.boundaryCategory; boundaryKind = [string]$_.boundaryKind
                boundaryTargetId = $_.boundaryTargetId; terminalEvidenceId = $_.terminalEvidenceId
                classification = [string]$_.classification; legacyPathId = $_.legacyPathId
                evidence = @((Values $_.evidence) | ForEach-Object { Project-Evidence $_ })
                pathEvidence = @((Values $_.pathEvidence) | ForEach-Object { Project-PathEvidence $_ })
                supportingFactIds = @(Values $_.supportingFactIds); supportingEdgeIds = @(Values $_.supportingEdgeIds)
            } })
            inventories = [ordered]@{
                identityState = @($identity | ForEach-Object { [ordered]@{ id = [string]$_.identityStateId; kind = [string]$_.identityKind; classification = [string]$_.classification; safeMetadata = $_.safeMetadata; evidenceFactId = [string]$_.evidence.factId; supportingFactIds = @(Values $_.supportingFactIds) } })
                structuralCandidates = @($candidates | ForEach-Object { [ordered]@{ id = [string]$_.candidateId; classification = [string]$_.classification; ruleId = [string]$_.ruleId; evidenceTier = [string]$_.evidenceTier; surfaceIds = @(Values $_.surfaceIds); supportingFactIds = @(Values $_.supportingFactIds) } })
            }
            gaps = @($gaps | ForEach-Object { [ordered]@{ gapId = [string]$_.gapId; classification = [string]$_.classification; scopeKind = [string]$_.scopeKind; scopeId = $_.scopeId; ruleId = [string]$_.ruleId; evidenceTier = [string]$_.evidenceTier; coverageLabel = [string]$_.coverageLabel; commitSha = [string]$_.commitSha; filePath = $_.filePath; startLine = $_.startLine; endLine = $_.endLine; extractorId = $_.extractorId; extractorVersion = $_.extractorVersion; supportingFactIds = @(Values $_.supportingFactIds); limitations = @(Values $_.limitations) } })
            supportingIds = @(
                @($evidence | ForEach-Object { [string]$_.factId; Values $_.supportingFactIds; Values $_.supportingEdgeIds })
                @($pathEvidence | ForEach-Object { [string]$_.evidenceId; Values $_.supportingFactIds })
            ) | Where-Object { $_ } | Sort-Object -Unique
            retrievalHints = @($retrievalHints)
            evidenceDocs = if ($EvidenceDocsRoot) { [ordered]@{ status = 'supplied-read-only'; locator = [IO.Path]::GetRelativePath($staging, [IO.Path]::GetFullPath($EvidenceDocsRoot)).Replace('\', '/'); chunks = 'chunks.jsonl'; manifest = 'manifest.json'; queryRecipes = 'query-recipes.json' } } else { [ordered]@{ status = 'not-supplied' } }
            humanReview = [ordered]@{ status = if ($ReviewPath) { 'validated-overlay' } else { 'not-supplied' }; verdict = [string]$decision.verdict; migrationDisposition = [string]$decision.migrationDisposition; capabilityLabel = $decision.capabilityLabel; comment = $decision.comment; correction = $decision.correction }
            limitations = @('Static evidence does not prove runtime execution, branch feasibility, successful binding, business intent, or migration correctness.', 'Human review must remain an overlay and must not rewrite this handoff or its source packet.')
        }
        $handoffPath = Join-Path $staging "$pageId.handoff.json"
        [IO.File]::WriteAllText($handoffPath, (($handoff | ConvertTo-Json -Depth 30) + "`n"), [Text.UTF8Encoding]::new($false))

        $chainRows = foreach ($chain in $chains) {
            '<tr><td><code>{0}</code></td><td>{1}</td><td><code>{2}</code></td><td><code>{3}</code></td><td>{4}</td></tr>' -f (ConvertTo-HtmlText $chain.chainId), (ConvertTo-HtmlText $chain.eventSourceId), (ConvertTo-HtmlText $(if ($chain.handlerSymbol) { $chain.handlerSymbol } else { $chain.handlerId })), (ConvertTo-HtmlText $chain.classification), (ConvertTo-HtmlText $(if ($chain.terminalKind) { $chain.terminalKind } else { $chain.traversalObservation.stopState }))
        }
        $boundaryRows = foreach ($boundary in $boundaries) { '<tr><td><code>{0}</code></td><td>{1}</td><td>{2}</td><td><code>{3}</code></td></tr>' -f (ConvertTo-HtmlText $boundary.boundaryId), (ConvertTo-HtmlText $boundary.boundaryCategory), (ConvertTo-HtmlText $boundary.boundaryKind), (ConvertTo-HtmlText $boundary.boundaryTargetId) }
        $gapRows = foreach ($gap in $gaps) { '<li><code>{0}</code> <code>{1}</code> <code>{2}</code> — {3}; {4}:L{5}-{6}; commit <code>{7}</code>; extractor <code>{8}/{9}</code>; support <code>{10}</code></li>' -f (ConvertTo-HtmlText $gap.gapId), (ConvertTo-HtmlText $gap.ruleId), (ConvertTo-HtmlText $gap.evidenceTier), (ConvertTo-HtmlText $gap.classification), (ConvertTo-HtmlText $gap.filePath), (ConvertTo-HtmlText $gap.startLine), (ConvertTo-HtmlText $gap.endLine), (ConvertTo-HtmlText $gap.commitSha), (ConvertTo-HtmlText $gap.extractorId), (ConvertTo-HtmlText $gap.extractorVersion), (ConvertTo-HtmlText ((Values $gap.supportingFactIds) -join ', ')) }
        $identityRows = foreach ($item in $identity) { '<tr><td><code>{0}</code></td><td>{1}</td><td>{2}</td><td><code>{3}</code></td><td><code>{4}</code></td></tr>' -f (ConvertTo-HtmlText $item.identityStateId), (ConvertTo-HtmlText $item.identityKind), (ConvertTo-HtmlText $item.classification), (ConvertTo-HtmlText (($item.safeMetadata | ConvertTo-Json -Compress))), (ConvertTo-HtmlText $item.evidence.factId) }
        $candidateRows = foreach ($item in $candidates) { '<tr><td><code>{0}</code></td><td>{1}</td><td><code>{2}</code></td><td><code>{3}</code></td></tr>' -f (ConvertTo-HtmlText $item.candidateId), (ConvertTo-HtmlText $item.classification), (ConvertTo-HtmlText $item.ruleId), (ConvertTo-HtmlText ((Values $item.supportingFactIds) -join ', ')) }
        $correctionHtml = if ($null -ne $decision.correction) { '<p><strong>Correction ({0}):</strong> {1}</p>' -f (ConvertTo-HtmlText $decision.correction.category), (ConvertTo-HtmlText $decision.correction.statement) } else { '' }
        $sourceHtml = if ($IncludeRawSource) { Source-Excerpt $surface.evidence $SourceRoot $SourceContextLines } else { '<p class="muted">Raw source omitted. Regenerate with <code>-IncludeRawSource -SourceRoot &lt;authorized-root&gt;</code> for a bounded private excerpt.</p>' }
        $html = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>$(ConvertTo-HtmlText $pageId) Web Forms review</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1200px;margin:auto;padding:24px}.private,.warning{padding:12px;border-left:5px solid #c62828;background:#fff1f0}.summary{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px}.card,details{background:white;border:1px solid #dbe2ee;border-radius:8px;padding:14px;margin:14px 0}.summary .card{margin:0}table{width:100%;border-collapse:collapse}th,td{padding:9px;border:1px solid #dbe2ee;text-align:left;vertical-align:top}th{background:#eaf1ff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px;overflow-wrap:anywhere}pre{overflow:auto;background:#172033;color:#f8fafc;padding:14px;border-radius:6px}.muted{color:#566070}.button{display:inline-block;padding:7px 10px;background:#eaf1ff;border:1px solid #bed0ee;border-radius:6px;text-decoration:none}</style></head><body><main>
<p><a class="button" href="index.html">Return to application index</a></p><h1>$(ConvertTo-HtmlText $surface.evidence.filePath)</h1><p class="private">PRIVATE local evidence review. Human conclusions are review metadata, not scanner facts.</p>
<section class="summary"><div class="card"><strong>Controls</strong><br>$(@(Values $surface.controlIds).Count)</div><div class="card"><strong>Event chains</strong><br>$($chains.Count)</div><div class="card"><strong>Boundaries</strong><br>$($boundaries.Count)</div><div class="card"><strong>Gaps</strong><br>$($gaps.Count)</div></section>
<section class="card"><h2>Review status</h2><p><strong>Verdict:</strong> $(ConvertTo-HtmlText $decision.verdict) · <strong>Disposition:</strong> $(ConvertTo-HtmlText $decision.migrationDisposition)</p><p><strong>Capability:</strong> $(ConvertTo-HtmlText $decision.capabilityLabel)</p><p>$(ConvertTo-HtmlText $decision.comment)</p>$correctionHtml<p>Human review is a separate validated overlay, never scanner evidence.</p></section>
<section class="card"><h2>Surface</h2><p><code>$(ConvertTo-HtmlText $surface.surfaceId)</code> · $(ConvertTo-HtmlText $surface.surfaceKind) · project <code>$(ConvertTo-HtmlText $surface.projectId)</code></p><p><strong>Coverage:</strong> $(ConvertTo-HtmlText ($coverage -join ', '))</p><p><strong>Controls:</strong> $(ConvertTo-HtmlText ((Values $surface.controlIds) -join ', '))</p></section>
<section class="card"><h2>Trigger and retained call paths</h2><table><thead><tr><th>Chain</th><th>Event source</th><th>Handler</th><th>Classification</th><th>Terminal/stop</th></tr></thead><tbody>$($chainRows -join '')</tbody></table></section>
<details><summary><strong>Downstream boundaries ($($boundaries.Count))</strong></summary><table><thead><tr><th>ID</th><th>Category</th><th>Kind</th><th>Target</th></tr></thead><tbody>$($boundaryRows -join '')</tbody></table></details>
<details><summary><strong>Identity/state ($($identity.Count))</strong></summary><table><thead><tr><th>ID</th><th>Kind</th><th>Classification</th><th>Safe metadata</th><th>Evidence</th></tr></thead><tbody>$($identityRows -join '')</tbody></table></details>
<details><summary><strong>Project-scoped data movement ($projectBatchCount)</strong></summary><p>Stored once in the application handoff and application index because project association does not prove page association.</p></details>
<details><summary><strong>Structural candidates ($($candidates.Count))</strong></summary><table><thead><tr><th>ID</th><th>Classification</th><th>Rule</th><th>Support</th></tr></thead><tbody>$($candidateRows -join '')</tbody></table></details>
<details><summary><strong>Explicit gaps ($($gaps.Count))</strong></summary><ul>$($gapRows -join '')</ul></details>
<details><summary><strong>Evidence citations ($($evidence.Count))</strong></summary>$(Evidence-List $evidence)</details>
<details><summary><strong>Retained call-path locations ($($pathEvidence.Count))</strong></summary>$(Path-Evidence-List $pathEvidence)</details>
<details><summary><strong>Working-tree source excerpt</strong></summary>$sourceHtml</details>
<details><summary><strong>Agent evidence handoff</strong></summary><p><a href="$pageId.handoff.json">Open $pageId.handoff.json</a>. It provides stable evidence IDs and closed TraceMap recipe hints; it is not a BRD.</p></details>
</main></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $staging "$pageId.html"), $html, [Text.UTF8Encoding]::new($false))
        $pageRows.Add([pscustomobject]@{ PageId = $pageId; Path = [string]$surface.evidence.filePath; SurfaceKind = [string]$surface.surfaceKind; Chains = $chains.Count; Boundaries = $boundaries.Count; Gaps = $gaps.Count; Coverage = ($coverage -join ', '); Verdict = [string]$decision.verdict; Disposition = [string]$decision.migrationDisposition })
        $applicationPages.Add([ordered]@{ pageId = $pageId; surfaceId = [string]$surface.surfaceId; filePath = [string]$surface.evidence.filePath; report = "$pageId.html"; handoff = "$pageId.handoff.json"; counts = $handoff.counts })
    }

    $applicationGaps = @(Values $packet.gaps | Where-Object { !$associatedGapIds.Contains([string]$_.gapId) } | Sort-Object gapId)
    $unassociatedIdentity = @(Values $packet.identityStateInventory | Where-Object { $null -eq $_.surfaceId } | Sort-Object identityStateId)
    $surfaceProjectIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($surface in $ordered) { if ($surface.projectId) { [void]$surfaceProjectIds.Add([string]$surface.projectId) } }
    $applicationBatch = @(Values $packet.batchDataMovementInventory | Where-Object { $null -ne $_.projectId -and $surfaceProjectIds.Contains([string]$_.projectId) } | Sort-Object projectId, batchDataMovementId)
    $unassociatedBatch = @(Values $packet.batchDataMovementInventory | Where-Object { $null -eq $_.projectId -or !$surfaceProjectIds.Contains([string]$_.projectId) } | Sort-Object batchDataMovementId)
    $surfaceIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($surface in $ordered) { [void]$surfaceIds.Add([string]$surface.surfaceId) }
    $unassociatedCandidates = @(Values $packet.structuralSliceCandidates | Where-Object { @((Values $_.surfaceIds) | Where-Object { $surfaceIds.Contains([string]$_) }).Count -eq 0 } | Sort-Object candidateId)
    $applicationStatus = if ([string]$packet.coverage -like 'reduced-*' -or $packet.summary.truncated -or @(Values $packet.gaps).Count -gt 0) { 'partial' } else { 'complete' }
    $appHandoff = [ordered]@{
        schemaVersion = 'webforms-application-handoff.v1'; ruleId = 'diagnostic.webforms.application-handoff.v1'; claimLevel = 'local-only'
        packet = [ordered]@{ packetId = [string]$packet.packetId; scanId = [string]$sources[0].scanId; commitSha = [string]$sources[0].commitSha; snapshot = 'webforms-modernization.snapshot.json' }
        analysis = [ordered]@{ status = $applicationStatus; coverage = [string]$packet.coverage; packetTruncated = [bool]$packet.summary.truncated; totalGapCount = @(Values $packet.gaps).Count }
        pageCount = $applicationPages.Count; pages = @($applicationPages)
        applicationGaps = @($applicationGaps | ForEach-Object { [ordered]@{ gapId = [string]$_.gapId; classification = [string]$_.classification; scopeKind = [string]$_.scopeKind; scopeId = $_.scopeId; ruleId = [string]$_.ruleId; evidenceTier = [string]$_.evidenceTier; coverageLabel = [string]$_.coverageLabel; commitSha = [string]$_.commitSha; filePath = $_.filePath; startLine = $_.startLine; endLine = $_.endLine; extractorId = $_.extractorId; extractorVersion = $_.extractorVersion; supportingFactIds = @(Values $_.supportingFactIds); limitations = @(Values $_.limitations) } })
        unassociatedIdentityState = @($unassociatedIdentity | ForEach-Object { [ordered]@{ id = [string]$_.identityStateId; kind = [string]$_.identityKind; classification = [string]$_.classification; safeMetadata = $_.safeMetadata; evidenceFactId = [string]$_.evidence.factId; evidence = Project-Evidence $_.evidence; supportingFactIds = @(Values $_.supportingFactIds) } })
        projectDataMovement = @($applicationBatch | ForEach-Object { [ordered]@{ id = [string]$_.batchDataMovementId; projectId = [string]$_.projectId; surfaceKind = [string]$_.surfaceKind; mechanism = [string]$_.mechanism; operationKind = [string]$_.operationKind; ownerStatus = [string]$_.ownerStatus; projectResolution = [string]$_.projectResolution; safeMetadata = $_.safeMetadata; evidenceFactId = [string]$_.evidence.factId; evidence = Project-Evidence $_.evidence; supportingFactIds = @(Values $_.supportingFactIds) } })
        unassociatedBatchDataMovement = @($unassociatedBatch | ForEach-Object { [ordered]@{ id = [string]$_.batchDataMovementId; surfaceKind = [string]$_.surfaceKind; mechanism = [string]$_.mechanism; operationKind = [string]$_.operationKind; projectId = $_.projectId; safeMetadata = $_.safeMetadata; evidenceFactId = [string]$_.evidence.factId; evidence = Project-Evidence $_.evidence; supportingFactIds = @(Values $_.supportingFactIds) } })
        unassociatedStructuralCandidates = @($unassociatedCandidates | ForEach-Object { [ordered]@{ id = [string]$_.candidateId; classification = [string]$_.classification; ruleId = [string]$_.ruleId; evidenceTier = [string]$_.evidenceTier; surfaceIds = @(Values $_.surfaceIds); supportingFactIds = @(Values $_.supportingFactIds) } })
        evidenceDocs = if ($EvidenceDocsRoot) { [ordered]@{ status = 'supplied-read-only'; locator = [IO.Path]::GetRelativePath($staging, [IO.Path]::GetFullPath($EvidenceDocsRoot)).Replace('\', '/'); chunks = 'chunks.jsonl'; manifest = 'manifest.json'; queryRecipes = 'query-recipes.json' } } else { [ordered]@{ status = 'not-supplied' } }
        humanReview = if ($ReviewPath) { [ordered]@{ status = 'validated-overlay'; schemaVersion = [string]$review.schemaVersion; overlayId = [string]$review.overlayId; reviewState = [string]$review.reviewState } } else { [ordered]@{ status = 'not-supplied' } }
        limitations = @('This is deterministic navigation metadata over one packet, not business intent, a BRD, or a modernization decision.', 'The evidence-docs corpus is an external read-only input and is never rewritten by this generator.')
    }
    [IO.File]::WriteAllText((Join-Path $staging 'application-handoff.json'), (($appHandoff | ConvertTo-Json -Depth 30) + "`n"), [Text.UTF8Encoding]::new($false))
    $applicationGapRows = foreach ($gap in $applicationGaps) { '<li><code>{0}</code> <code>{1}</code> <code>{2}</code> — {3}; scope <code>{4}/{5}</code>; {6}:L{7}-{8}; commit <code>{9}</code>; extractor <code>{10}/{11}</code>; support <code>{12}</code></li>' -f (ConvertTo-HtmlText $gap.gapId), (ConvertTo-HtmlText $gap.ruleId), (ConvertTo-HtmlText $gap.evidenceTier), (ConvertTo-HtmlText $gap.classification), (ConvertTo-HtmlText $gap.scopeKind), (ConvertTo-HtmlText $gap.scopeId), (ConvertTo-HtmlText $gap.filePath), (ConvertTo-HtmlText $gap.startLine), (ConvertTo-HtmlText $gap.endLine), (ConvertTo-HtmlText $gap.commitSha), (ConvertTo-HtmlText $gap.extractorId), (ConvertTo-HtmlText $gap.extractorVersion), (ConvertTo-HtmlText ((Values $gap.supportingFactIds) -join ', ')) }
    $unassociatedIdentityRows = foreach ($item in $unassociatedIdentity) { '<li><code>{0}</code> {1} — {2}; evidence <code>{3}</code> <code>{4}</code> <code>{5}</code> — {6}:L{7}-{8}; commit <code>{9}</code>; extractor <code>{10}/{11}</code></li>' -f (ConvertTo-HtmlText $item.identityStateId), (ConvertTo-HtmlText $item.identityKind), (ConvertTo-HtmlText $item.classification), (ConvertTo-HtmlText $item.evidence.factId), (ConvertTo-HtmlText $item.evidence.ruleId), (ConvertTo-HtmlText $item.evidence.evidenceTier), (ConvertTo-HtmlText $item.evidence.filePath), (ConvertTo-HtmlText $item.evidence.startLine), (ConvertTo-HtmlText $item.evidence.endLine), (ConvertTo-HtmlText $item.evidence.commitSha), (ConvertTo-HtmlText $item.evidence.extractorId), (ConvertTo-HtmlText $item.evidence.extractorVersion) }
    $applicationBatchRows = foreach ($item in $applicationBatch) { '<li><code>{0}</code> {1}/{2}; project <code>{3}</code>; evidence <code>{4}</code> <code>{5}</code> <code>{6}</code> — {7}:L{8}-{9}; commit <code>{10}</code>; extractor <code>{11}/{12}</code></li>' -f (ConvertTo-HtmlText $item.batchDataMovementId), (ConvertTo-HtmlText $item.mechanism), (ConvertTo-HtmlText $item.operationKind), (ConvertTo-HtmlText $item.projectId), (ConvertTo-HtmlText $item.evidence.factId), (ConvertTo-HtmlText $item.evidence.ruleId), (ConvertTo-HtmlText $item.evidence.evidenceTier), (ConvertTo-HtmlText $item.evidence.filePath), (ConvertTo-HtmlText $item.evidence.startLine), (ConvertTo-HtmlText $item.evidence.endLine), (ConvertTo-HtmlText $item.evidence.commitSha), (ConvertTo-HtmlText $item.evidence.extractorId), (ConvertTo-HtmlText $item.evidence.extractorVersion) }
    $unassociatedBatchRows = foreach ($item in $unassociatedBatch) { '<li><code>{0}</code> {1}/{2}; project <code>{3}</code>; evidence <code>{4}</code> <code>{5}</code> <code>{6}</code> — {7}:L{8}-{9}; commit <code>{10}</code>; extractor <code>{11}/{12}</code></li>' -f (ConvertTo-HtmlText $item.batchDataMovementId), (ConvertTo-HtmlText $item.mechanism), (ConvertTo-HtmlText $item.operationKind), (ConvertTo-HtmlText $item.projectId), (ConvertTo-HtmlText $item.evidence.factId), (ConvertTo-HtmlText $item.evidence.ruleId), (ConvertTo-HtmlText $item.evidence.evidenceTier), (ConvertTo-HtmlText $item.evidence.filePath), (ConvertTo-HtmlText $item.evidence.startLine), (ConvertTo-HtmlText $item.evidence.endLine), (ConvertTo-HtmlText $item.evidence.commitSha), (ConvertTo-HtmlText $item.evidence.extractorId), (ConvertTo-HtmlText $item.evidence.extractorVersion) }
    $unassociatedCandidateRows = foreach ($item in $unassociatedCandidates) { '<li><code>{0}</code> {1}; surfaces <code>{2}</code>; support <code>{3}</code></li>' -f (ConvertTo-HtmlText $item.candidateId), (ConvertTo-HtmlText $item.classification), (ConvertTo-HtmlText ((Values $item.surfaceIds) -join ', ')), (ConvertTo-HtmlText ((Values $item.supportingFactIds) -join ', ')) }
    $tableRows = foreach ($row in $pageRows) { '<tr><td><a target="_blank" rel="noopener" href="{0}.html">{0}</a></td><td><code>{1}</code></td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td><a href="{0}.handoff.json">JSON</a></td></tr>' -f $row.PageId, (ConvertTo-HtmlText $row.Path), (ConvertTo-HtmlText $row.SurfaceKind), $row.Chains, $row.Boundaries, $row.Gaps, (ConvertTo-HtmlText $row.Verdict), (ConvertTo-HtmlText $row.Disposition) }
    $index = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Web Forms application workbench</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1400px;margin:auto;padding:24px}.private{padding:12px;border-left:5px solid #c62828;background:#fff1f0}table{width:100%;border-collapse:collapse;background:white}th,td{padding:10px;border:1px solid #dbe2ee;text-align:left;vertical-align:top}th{background:#eaf1ff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px;overflow-wrap:anywhere}a{color:#1558b0}details{background:white;border:1px solid #dbe2ee;border-radius:8px;padding:14px;margin:14px 0}</style></head><body><main><h1>Private Web Forms application workbench</h1><p class="private">PRIVATE: $(ConvertTo-HtmlText $applicationPages.Count) selected surfaces from one retained packet. No source scan was run.</p><p>Packet <code>$(ConvertTo-HtmlText $packet.packetId)</code>. Analysis <code>$(ConvertTo-HtmlText $applicationStatus)</code>; coverage <code>$(ConvertTo-HtmlText $packet.coverage)</code>; truncated <code>$(ConvertTo-HtmlText $packet.summary.truncated)</code>. <a href="application-handoff.json">Application handoff JSON</a> · <a href="webforms-modernization.snapshot.json">Packet snapshot</a>.</p><table><thead><tr><th>Page</th><th>Retained file</th><th>Kind</th><th>Chains</th><th>Boundaries</th><th>Gaps</th><th>Verdict</th><th>Disposition</th><th>Handoff</th></tr></thead><tbody>$($tableRows -join '')</tbody></table><details><summary><strong>Application or unassociated gaps ($($applicationGaps.Count))</strong></summary><ul>$($applicationGapRows -join '')</ul></details><details><summary><strong>Unassociated identity/state ($($unassociatedIdentity.Count))</strong></summary><ul>$($unassociatedIdentityRows -join '')</ul></details><details><summary><strong>Project-scoped batch/data movement ($($applicationBatch.Count))</strong></summary><ul>$($applicationBatchRows -join '')</ul></details><details><summary><strong>Unassociated batch/data movement ($($unassociatedBatch.Count))</strong></summary><ul>$($unassociatedBatchRows -join '')</ul></details><details><summary><strong>Unassociated structural candidates ($($unassociatedCandidates.Count))</strong></summary><ul>$($unassociatedCandidateRows -join '')</ul></details><p>Static evidence does not prove runtime execution, business intent, or migration correctness. Human review remains a separate overlay.</p></main></body></html>
"@
    [IO.File]::WriteAllText((Join-Path $staging 'index.html'), $index, [Text.UTF8Encoding]::new($false))
    [IO.Directory]::Move($staging, $OutputDirectory)
}
catch {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    throw
}

Write-Host "applicationWorkbench=completed;pages=$($surfaces.Count);rawSource=$($IncludeRawSource.ToString().ToLowerInvariant())"
Write-Host "applicationWorkbenchIndex=$(Join-Path $OutputDirectory 'index.html')"
Write-Host 'chunksCorpus=preserved-read-only'
if ($IsWindows) { Start-Process -FilePath (Join-Path $OutputDirectory 'index.html') }
