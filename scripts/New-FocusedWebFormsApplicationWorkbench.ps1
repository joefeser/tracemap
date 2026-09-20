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
function Get-TextSha256([string]$Value) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
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
function Get-CallSiteKey([object]$Call) {
    $callSiteId = Property-Value $Call 'callSiteId'
    if ($callSiteId) { return [string]$callSiteId }
    $evidence = Property-Value $Call 'evidence'
    return '{0}|{1}|{2}|{3}' -f ([string](Property-Value $evidence 'filePath')), ([int](Property-Value $evidence 'startLine')), ([int](Property-Value $evidence 'endLine')), ([string](Property-Value $Call 'calleeName'))
}
function Get-NormalizedCallSites([object[]]$Calls) {
    $groups = @($Calls | Where-Object { $null -ne $_ } | Group-Object { Get-CallSiteKey $_ } | Sort-Object Name)
    return @($groups | ForEach-Object {
        $rows = @($_.Group | Sort-Object `
            @{ Expression = { if (([string](Property-Value $_ 'resolution')) -eq 'compiler-resolved' -or ([string]$_.callKind) -like 'Semantic*') { 0 } else { 1 } } }, `
            @{ Expression = { [string]$_.callEvidenceId } })
        $preferred = $rows[0]
        [pscustomobject]@{
            CallSiteId = [string]$_.Name
            Preferred = $preferred
            EvidenceFactCount = $rows.Count
            HasSemanticEvidence = @($rows | Where-Object { ([string](Property-Value $_ 'resolution')) -eq 'compiler-resolved' -or ([string]$_.callKind) -like 'Semantic*' }).Count -gt 0
            HasSyntaxEvidence = @($rows | Where-Object { ([string](Property-Value $_ 'resolution')) -eq 'syntax-only' -or ([string]$_.callKind) -like 'Syntax*' }).Count -gt 0
        }
    })
}
function Complete-OptionalProperties([object[]]$Items, [string[]]$Names) {
    foreach ($item in @(Values $Items)) {
        if ($null -eq $item) { continue }
        foreach ($name in $Names) {
            if ($null -eq $item.PSObject.Properties[$name]) {
                Add-Member -InputObject $item -MemberType NoteProperty -Name $name -Value $null
            }
        }
    }
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

function Outlier-Table([string]$Title, [object[]]$Rows) {
    if ($Rows.Count -eq 0) {
        return '<details><summary><strong>{0}</strong></summary><p>No pages retained this signal.</p></details>' -f (ConvertTo-HtmlText $Title)
    }
    $body = foreach ($row in @($Rows | Select-Object -First 25)) {
        '<tr><td><code>{0}</code></td><td>{1}</td><td>{2}</td><td>{3} / {4} / {5}</td><td>{6}</td><td>{7}</td><td>{8}</td><td>{9}</td><td>{10}</td><td>{11}</td><td>{12}</td></tr>' -f `
            (ConvertTo-HtmlText $row.pageId), $row.counts.controls, $row.counts.eventChains,
            $row.counts.callProjections, $row.counts.uniqueCallFacts, $row.counts.normalizedCallSites, $row.counts.projectionReuse,
            $row.counts.callEvidenceCeilingChains,
            $row.chainOutcomes.unresolvedHandlers, $row.chainOutcomes.otherIncomplete,
            $row.chainOutcomes.truncated, $row.counts.boundaries, $row.counts.gaps
    }
    '<details open><summary><strong>{0}</strong></summary><table><thead><tr><th>Page</th><th>Controls</th><th>Chains</th><th>Retained projections / unique facts / normalized sites</th><th>Reuse</th><th>Call evidence ceiling</th><th>Handler unavailable</th><th>Other incomplete</th><th>Traversal truncated</th><th>Boundaries</th><th>Gaps</th></tr></thead><tbody>{1}</tbody></table></details>' -f (ConvertTo-HtmlText $Title), ($body -join '')
}

function Private-Outlier-Table([string]$Title, [object[]]$Rows, [hashtable]$SourcePathByPageId) {
    if ($Rows.Count -eq 0) {
        return '<details><summary><strong>{0}</strong></summary><p>No pages retained this signal.</p></details>' -f (ConvertTo-HtmlText $Title)
    }
    $body = foreach ($row in @($Rows | Select-Object -First 25)) {
        $pageId = [string]$row.pageId
        $sourcePath = if ($SourcePathByPageId.ContainsKey($pageId)) { [string]$SourcePathByPageId[$pageId] } else { 'source path unavailable' }
        $pageHref = [Uri]::EscapeDataString($pageId) + '.html'
        $pageLink = '<a href="{0}" title="{1}" aria-label="{2}: {1}"><code>{2}</code></a><small class="source-path">{1}</small>' -f `
            (ConvertTo-HtmlText $pageHref), (ConvertTo-HtmlText $sourcePath), (ConvertTo-HtmlText $pageId)
        '<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3} / {4} / {5}</td><td>{6}</td><td>{7}</td><td>{8}</td><td>{9}</td><td>{10}</td><td>{11}</td><td>{12}</td></tr>' -f `
            $pageLink, $row.counts.controls, $row.counts.eventChains,
            $row.counts.callProjections, $row.counts.uniqueCallFacts, $row.counts.normalizedCallSites, $row.counts.projectionReuse,
            $row.counts.callEvidenceCeilingChains,
            $row.chainOutcomes.unresolvedHandlers, $row.chainOutcomes.otherIncomplete,
            $row.chainOutcomes.truncated, $row.counts.boundaries, $row.counts.gaps
    }
    '<details open><summary><strong>{0}</strong></summary><table><thead><tr><th>Page / source</th><th>Controls</th><th>Chains</th><th>Retained projections / unique facts / normalized sites</th><th>Reuse</th><th>Call evidence ceiling</th><th>Handler unavailable</th><th>Other incomplete</th><th>Traversal truncated</th><th>Boundaries</th><th>Gaps</th></tr></thead><tbody>{1}</tbody></table></details>' -f (ConvertTo-HtmlText $Title), ($body -join '')
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
$generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$packetSha256 = (Get-FileHash -LiteralPath $packetFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$packet = [IO.File]::ReadAllText($packetFile.FullName) | ConvertFrom-Json -Depth 100
if ($packet.schemaVersion -ne 'webforms-modernization-packet.v1') { throw 'ApplicationWorkbenchPacketSchemaMismatch' }
$sources = @(Values $packet.sources)
if ($sources.Count -lt 1 -or $sources.Count -gt 64) { throw 'ApplicationWorkbenchPacketProvenanceMismatch' }
$sourceKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($source in $sources) {
    $scanId = [string](Property-Value $source 'scanId')
    $commitSha = [string](Property-Value $source 'commitSha')
    if ([string]::IsNullOrWhiteSpace($scanId) -or $commitSha -notmatch '^[0-9a-fA-F]{40}$' -or !$sourceKeys.Add("$scanId|$($commitSha.ToLowerInvariant())")) {
        throw 'ApplicationWorkbenchPacketProvenanceMismatch'
    }
}
$surfaces = @(Values $packet.surfaces)
if ($surfaces.Count -lt 1 -or $surfaces.Count -gt 1000) { throw 'ApplicationWorkbenchSurfaceLimit' }
if ($IncludeRawSource -and (!$SourceRoot -or !(Test-Path -LiteralPath $SourceRoot -PathType Container))) { throw 'ApplicationWorkbenchSourceRootUnavailable' }
$reviewBySurface = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
$reviewSha256 = $null
if ($ReviewPath) {
    if (!(Test-Path -LiteralPath $ReviewPath -PathType Leaf)) { throw 'ApplicationWorkbenchReviewUnavailable' }
    $reviewValidator = Join-Path $PSScriptRoot 'webforms-review/Invoke-WitsApplicationReview.ps1'
    & $reviewValidator -Mode Validate -PacketPath $packetFile.FullName -ReviewPath $ReviewPath | Out-Null
    $resolvedReviewPath = [IO.Path]::GetFullPath($ReviewPath)
    $reviewSha256 = (Get-FileHash -LiteralPath $resolvedReviewPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $review = [IO.File]::ReadAllText($resolvedReviewPath) | ConvertFrom-Json -Depth 40
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

# Packet contracts deliberately omit properties that are not established for a
# particular evidence row. PowerShell strict mode must distinguish a missing
# optional property from a malformed packet, which the validator already
# rejected above. Complete only the optional projection fields used below;
# the authoritative packet snapshot remains byte-for-byte unchanged.
Complete-OptionalProperties (Values $packet.surfaces) @('projectId','controlIds','controls','supportingEvidence','supportingFactIds')
Complete-OptionalProperties @($packet) @('clientBehaviorInventory','serverBehaviorInventory')
Complete-OptionalProperties (Values $packet.eventChains) @(
    'chainId','surfaceId','eventSourceId','bindingFactId','handlerId','handlerFactId','handlerSymbol','handlerResolution',
    'classification','legacyPathId','terminalKind','traversalObservation','evidence','pathEvidence',
    'supportingFactIds','supportingEdgeIds','coverageLabels','callEvidence','callEvidenceTotalCount','callEvidenceTruncated')
Complete-OptionalProperties (Values $packet.downstreamBoundaries) @(
    'boundaryId','chainId','surfaceId','handlerId','boundaryCategory','boundaryKind','boundaryTargetId',
    'terminalEvidenceId','terminalEvidenceIsFact','classification','legacyPathId','evidence','pathEvidence',
    'supportingFactIds','supportingEdgeIds')
Complete-OptionalProperties (Values $packet.identityStateInventory) @(
    'identityStateId','identityKind','classification','surfaceId','safeMetadata','evidence','supportingFactIds')
Complete-OptionalProperties (Values $packet.batchDataMovementInventory) @(
    'batchDataMovementId','surfaceKind','mechanism','operationKind','ownerStatus','projectResolution','projectId',
    'safeMetadata','evidence','supportingFactIds')
Complete-OptionalProperties (Values $packet.structuralSliceCandidates) @(
    'candidateId','classification','ruleId','evidenceTier','surfaceIds','evidence','supportingFactIds')
Complete-OptionalProperties (Values $packet.clientBehaviorInventory) @(
    'clientBehaviorId','behaviorKind','surfaceId','selectorKind','selectorTarget','targetResolution',
    'safeMetadata','evidence','supportingFactIds','limitations')
Complete-OptionalProperties (Values $packet.serverBehaviorInventory) @(
    'serverBehaviorId','behaviorKind','surfaceId','targetResolution','safeMetadata','evidence','supportingFactIds','limitations')
Complete-OptionalProperties (Values $packet.gaps) @(
    'gapId','classification','scopeKind','scopeId','ruleId','evidenceTier','coverageLabel','commitSha','filePath',
    'startLine','endLine','extractorId','extractorVersion','supportingFactIds','limitations','safeMetadata','truncationReason')
foreach ($chain in @(Values $packet.eventChains)) {
    Complete-OptionalProperties @($chain) @('nextEvidenceKind','unresolvedCallTargets','nextEvidenceInputs')
    Complete-OptionalProperties (Values $chain.callEvidence) @('callSiteId','resolution','technologyFamily','declaringType','assemblyName')
    if ($null -eq $chain.traversalObservation) {
        $chain.traversalObservation = [pscustomobject]@{ stopState = $null; truncationReasons = @() }
    } else {
        Complete-OptionalProperties @($chain.traversalObservation) @('stopState','truncationReasons')
    }
}

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
        $chains = @(Values $packet.eventChains | Where-Object { Same $_.surfaceId $surface.surfaceId } | Sort-Object `
            @{ Expression = { [string](@(Values $_.evidence | Sort-Object filePath, startLine | Select-Object -First 1).filePath) } }, `
            @{ Expression = { [int](@(Values $_.evidence | Sort-Object filePath, startLine | Select-Object -First 1).startLine) } }, `
            @{ Expression = { [string]$_.handlerSymbol } }, chainId)
        $chainIds = @($chains | ForEach-Object { [string]$_.chainId })
        $boundaries = @(Values $packet.downstreamBoundaries | Where-Object { Same $_.surfaceId $surface.surfaceId } | Sort-Object boundaryId)
        $identity = @(Values $packet.identityStateInventory | Where-Object {
            $surfaceId = Property-Value $_ 'surfaceId'
            $null -ne $surfaceId -and (Same $surfaceId $surface.surfaceId)
        } | Sort-Object identityStateId)
        $projectBatchCount = @(Values $packet.batchDataMovementInventory | Where-Object {
            $projectId = Property-Value $_ 'projectId'
            $null -ne $projectId -and (Same $projectId $surface.projectId)
        }).Count
        $candidates = @(Values $packet.structuralSliceCandidates | Where-Object {
            @((Values (Property-Value $_ 'surfaceIds')) | Where-Object { Same $_ $surface.surfaceId }).Count -gt 0
        } | Sort-Object candidateId)
        $clientBehavior = @(Values $packet.clientBehaviorInventory | Where-Object {
            Same $_.surfaceId $surface.surfaceId
        } | Sort-Object @{ Expression = { [string]$_.evidence.filePath } }, @{ Expression = { [int]$_.evidence.startLine } }, behaviorKind, clientBehaviorId)
        $serverBehavior = @(Values $packet.serverBehaviorInventory | Where-Object {
            Same $_.surfaceId $surface.surfaceId
        } | Sort-Object @{ Expression = { [string]$_.evidence.filePath } }, @{ Expression = { [int]$_.evidence.startLine } }, behaviorKind, serverBehaviorId)
        $pageIdentity = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($value in @($surface.surfaceId, $surface.evidence.factId) + @(Values $surface.supportingFactIds) + @(Values $surface.controlIds)) { if ($value) { [void]$pageIdentity.Add([string]$value) } }
        foreach ($chain in $chains) { foreach ($value in @($chain.chainId, $chain.bindingFactId, $chain.handlerId, $chain.handlerFactId, $chain.legacyPathId) + @(Values $chain.supportingFactIds) + @(Values $chain.supportingEdgeIds)) { if ($value) { [void]$pageIdentity.Add([string]$value) } } }
        foreach ($item in @($boundaries + $identity + $candidates + $clientBehavior + $serverBehavior)) {
            foreach ($property in @('boundaryId','identityStateId','batchDataMovementId','candidateId','clientBehaviorId','serverBehaviorId','terminalEvidenceId')) {
                $propertyValue = Property-Value $item $property
                if ($propertyValue) { [void]$pageIdentity.Add([string]$propertyValue) }
            }
            foreach ($value in @(Values (Property-Value $item 'supportingFactIds')) + @(Values (Property-Value $item 'supportingEdgeIds'))) {
                if ($value) { [void]$pageIdentity.Add([string]$value) }
            }
        }
        $evidence = @($surface.evidence) + @(Values $surface.supportingEvidence) + @($chains | ForEach-Object { Values $_.evidence }) + @($boundaries | ForEach-Object { Values $_.evidence }) + @($identity | ForEach-Object { $_.evidence }) + @($candidates | ForEach-Object { Values $_.evidence }) + @($clientBehavior | ForEach-Object { $_.evidence }) + @($serverBehavior | ForEach-Object { $_.evidence })
        $evidence = @($evidence | Where-Object { $null -ne $_ } | Sort-Object filePath, startLine, endLine, ruleId, factId -Unique)
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
        $gaps = @(Values $packet.gaps | Where-Object {
            $scopeId = Property-Value $_ 'scopeId'
            ($null -ne $scopeId -and $pageIdentity.Contains([string]$scopeId)) -or
            (@(Values (Property-Value $_ 'supportingFactIds') | Where-Object { $pageIdentity.Contains([string]$_) }).Count -gt 0)
        } | Sort-Object filePath, startLine, classification, gapId)
        $gapCategories = @($gaps | Group-Object {
            $classification = [string](Property-Value $_ 'classification')
            if ($classification) { $classification } else { 'unclassified' }
        } | Sort-Object Name | ForEach-Object { [ordered]@{ classification = [string]$_.Name; count = [int]$_.Count } })
        foreach ($gap in $gaps) { [void]$associatedGapIds.Add([string]$gap.gapId) }
        $handlers = @($chains | ForEach-Object { if ($_.handlerSymbol) { [string]$_.handlerSymbol } elseif ($_.handlerId) { [string]$_.handlerId } } | Where-Object { $_ } | Sort-Object -Unique)
        $controlDisplay = @(
            if (@(Values $surface.controls).Count -gt 0) {
                Values $surface.controls | ForEach-Object {
                    $declaredId = [string](Property-Value $_ 'declaredId')
                    $controlType = [string](Property-Value $_ 'controlType')
                    if (!$declaredId) { $declaredId = 'unavailable' }
                    if (!$controlType) { $controlType = 'unknown' }
                    [pscustomobject]@{ DeclaredId = $declaredId; ControlType = $controlType }
                } | Sort-Object DeclaredId, ControlType -Unique
            }
            else {
                Values $surface.controlIds | ForEach-Object {
                    [pscustomobject]@{ DeclaredId = [string]$_; ControlType = 'type unavailable in older packet' }
                }
            }
        )
        $controlDisplayText = if ($controlDisplay.Count -gt 0) {
            @($controlDisplay | ForEach-Object { '{0} ({1})' -f $_.DeclaredId, $_.ControlType }) -join ', '
        }
        else { 'none retained' }
        $coverage = @($chains | ForEach-Object { Values $_.coverageLabels } | Sort-Object -Unique)
        if ($coverage.Count -eq 0) { $coverage = @([string]$surface.evidence.coverageLabel) }
        $decision = if ($reviewBySurface.ContainsKey([string]$surface.surfaceId)) { $reviewBySurface[[string]$surface.surfaceId] } else { [pscustomobject]@{ verdict = 'unreviewed'; migrationDisposition = 'unassigned'; capabilityLabel = $null; comment = $null; correction = $null } }
        $analysisStatus = if ([string]$packet.coverage -like 'reduced-*' -or $packet.summary.truncated -or $gaps.Count -gt 0) { 'partial' } else { 'complete' }
        $boundaryStatus = if ($boundaries.Count -gt 0) {
            "$($boundaries.Count) detected"
        } elseif ($chains.Count -eq 0) {
            'not applicable; no retained event chains'
        } elseif ($analysisStatus -eq 'partial') {
            'not established under partial retained coverage'
        } else {
            'none detected within retained bounded call coverage'
        }
        $clientEventCount = @($clientBehavior | Where-Object { $_.behaviorKind -eq 'client-event-binding' }).Count
        $linkedClientEventCount = @($clientBehavior | Where-Object {
            $_.behaviorKind -eq 'client-event-binding' -and
            (Property-Value $_.safeMetadata 'serverHandlerName') -and
            (Property-Value $_.safeMetadata 'serverHandlerName') -notin @('not-applicable','unavailable')
        }).Count
        $httpRequestCount = @($clientBehavior | Where-Object { $_.behaviorKind -eq 'client-http-request' }).Count
        $linkedHttpRequestCount = @($clientBehavior | Where-Object {
            if ($_.behaviorKind -ne 'client-http-request') { return $false }
            $requestFactId = [string]$_.evidence.factId
            return @($chains | Where-Object {
                [string]$_.bindingFactId -eq $requestFactId -and
                (Property-Value $_ 'handlerFactId')
            }).Count -gt 0
        }).Count
        $navigationCount = @($serverBehavior | Where-Object { $_.behaviorKind -eq 'navigation' }).Count
        $lifecycleCount = @($serverBehavior | Where-Object { $_.behaviorKind -eq 'request-lifecycle' }).Count
        $serverMutationCount = @($serverBehavior | Where-Object { $_.behaviorKind -eq 'control-state-mutation' }).Count
        $inlineReferenceCount = @($serverBehavior | Where-Object { $_.behaviorKind -eq 'inline-server-reference' }).Count
        $unresolvedHandlerCount = @($chains | Where-Object { !(Property-Value $_ 'handlerFactId') }).Count
        $downstreamWithoutTerminalCount = @($chains | Where-Object { $_.traversalObservation.stopState -eq 'observed-downstream-without-supported-terminal' }).Count
        $noDownstreamCount = @($chains | Where-Object { $_.traversalObservation.stopState -eq 'no-observed-downstream-edge' }).Count
        $pathDetailTruncatedChainCount = @($chains | Where-Object {
            $value = Property-Value $_.traversalObservation 'pathEnumerationTruncated'
            if ($null -ne $value) { return $value -eq $true }
            return (Property-Value $_.traversalObservation 'truncated') -eq $true
        }).Count
        $terminalInventoryIncompleteChainCount = @($chains | Where-Object {
            $available = Property-Value $_.traversalObservation 'terminalReachabilityAvailable'
            if ($null -eq $available) { $available = $false }
            $value = Property-Value $_.traversalObservation 'terminalReachabilityComplete'
            $available -eq $true -and $null -ne $value -and $value -eq $false
        }).Count
        $terminalInventoryUnavailableChainCount = @($chains | Where-Object {
            $available = Property-Value $_.traversalObservation 'terminalReachabilityAvailable'
            if ($null -eq $available) { $available = $false }
            $available -ne $true
        }).Count
        $otherIncompleteCount = @($chains | Where-Object {
            !$_.terminalKind -and (Property-Value $_ 'handlerFactId') -and
            $_.traversalObservation.stopState -notin @('observed-downstream-without-supported-terminal','no-observed-downstream-edge') -and
            $_.traversalObservation.stopState -ne 'terminal-reachability-incomplete' -and
            (Property-Value $_.traversalObservation 'pathEnumerationTruncated') -ne $true
        }).Count
        $pagePathDetailTruncationReasons = @($chains | Where-Object {
            (Property-Value $_.traversalObservation 'pathEnumerationTruncated') -eq $true
        } | ForEach-Object { Values (Property-Value $_.traversalObservation 'pathEnumerationTruncationReasons') } | Where-Object { $_ } | Sort-Object -Unique)
        if ($pathDetailTruncatedChainCount -gt 0 -and $pagePathDetailTruncationReasons.Count -eq 0) { $pagePathDetailTruncationReasons = @('reason-unavailable') }
        $pageTerminalReachabilityLimitReasons = @($chains | Where-Object {
            (Property-Value $_.traversalObservation 'terminalReachabilityAvailable') -eq $true -and
            (Property-Value $_.traversalObservation 'terminalReachabilityComplete') -eq $false
        } | ForEach-Object { Values (Property-Value $_.traversalObservation 'terminalReachabilityLimitReasons') } | Where-Object { $_ } | Sort-Object -Unique)
        if ($terminalInventoryIncompleteChainCount -gt 0 -and $pageTerminalReachabilityLimitReasons.Count -eq 0) { $pageTerminalReachabilityLimitReasons = @('reason-unavailable') }
        $nextEvidenceSummary = @($chains | Where-Object {
            $kind = [string](Property-Value $_ 'nextEvidenceKind')
            $kind -and $kind -ne 'none'
        } | Group-Object { [string](Property-Value $_ 'nextEvidenceKind') } | Sort-Object Name | ForEach-Object {
            [ordered]@{
                kind = [string]$_.Name
                chainCount = [int]$_.Count
                targets = @($_.Group | ForEach-Object { Values (Property-Value $_ 'unresolvedCallTargets') } | Where-Object { $_ } | Sort-Object -Unique)
                requiredInputs = @($_.Group | ForEach-Object { Values (Property-Value $_ 'nextEvidenceInputs') } | Where-Object { $_ } | Sort-Object -Unique)
            }
        })
        $chainAssociatedCalls = @($chains | ForEach-Object { Values $_.callEvidence })
        $chainAssociatedCallCount = $chainAssociatedCalls.Count
        $reportedCallProjectionCount = @($chains | ForEach-Object {
            if ($null -ne $_.callEvidenceTotalCount) { [int]$_.callEvidenceTotalCount } else { @(Values $_.callEvidence).Count }
        } | Measure-Object -Sum).Sum
        if ($null -eq $reportedCallProjectionCount) { $reportedCallProjectionCount = 0 }
        $uniqueRetainedCallCount = @($chainAssociatedCalls | ForEach-Object {
            $callEvidenceId = Property-Value $_ 'callEvidenceId'
            if ($callEvidenceId) { [string]$callEvidenceId } elseif ($_.evidence.factId) { [string]$_.evidence.factId }
        } | Where-Object { $_ } | Sort-Object -Unique).Count
        $uniqueCallSiteCount = @(Get-NormalizedCallSites $chainAssociatedCalls).Count
        $callEvidenceOmittedCount = [Math]::Max(0, [int]$reportedCallProjectionCount - $chainAssociatedCallCount)
        $callEvidenceCeilingChainCount = @($chains | Where-Object {
            $_.callEvidenceTruncated -or [int]$(if ($null -ne $_.callEvidenceTotalCount) { $_.callEvidenceTotalCount } else { @(Values $_.callEvidence).Count }) -ge 256
        }).Count
        $technologyFamilies = @($chainAssociatedCalls | ForEach-Object {
            $family = [string](Property-Value $_ 'technologyFamily')
            if ($family) { $family } elseif (([string]$_.callKind) -like 'Semantic*') { 'resolved-unspecified' } else { 'unresolved' }
        } | Group-Object | Sort-Object Name | ForEach-Object { [ordered]@{ family = [string]$_.Name; retainedFacts = [int]$_.Count } })
        $behaviorSummary = "Retained evidence records $($chains.Count) event chain(s), $($clientBehavior.Count) inline client behavior(s), and $($serverBehavior.Count) server behavior(s). $linkedClientEventCount of $clientEventCount client event binding(s) correlate to one retained server handler. $linkedHttpRequestCount of $httpRequestCount inline HTTP request(s) join through a handler declaration to one retained entry method. Chain outcomes include $unresolvedHandlerCount unresolved handler(s), $downstreamWithoutTerminalCount with downstream calls but no supported terminal, $noDownstreamCount with no observed downstream edge, $terminalInventoryIncompleteChainCount incomplete terminal inventory(s), $terminalInventoryUnavailableChainCount unavailable terminal inventory observation(s), $pathDetailTruncatedChainCount path-detail truncation(s), and $otherIncompleteCount other incomplete chain(s). $chainAssociatedCallCount retained chain-associated call fact projection(s) represent $uniqueRetainedCallCount unique retained call fact(s) and $uniqueCallSiteCount normalized source call site(s). $callEvidenceCeilingChainCount chain(s) reached or exceeded the 256-fact call-evidence ceiling; $callEvidenceOmittedCount additional fact projection(s) are explicitly reported as omitted. Server evidence includes $navigationCount navigation candidate(s), $lifecycleCount request-lifecycle candidate(s), $serverMutationCount control-state mutation(s), and $inlineReferenceCount inline server-expression reference(s)."
        $retrievalHints = [Collections.Generic.List[object]]::new()
        $retrievalHints.Add([ordered]@{ recipeId = 'webforms-surface-facts'; parameters = [ordered]@{ surface_id = [string]$surface.surfaceId; limit = 500 } })
        foreach ($handler in $handlers) { $retrievalHints.Add([ordered]@{ recipeId = 'calls-from-handler'; parameters = [ordered]@{ handler_symbol = $handler; limit = 500 } }) }
        foreach ($boundary in @($boundaries | Where-Object { (Property-Value $_ 'terminalEvidenceIsFact') -eq $true })) { $retrievalHints.Add([ordered]@{ recipeId = 'boundary-supporting-facts'; parameters = [ordered]@{ terminal_evidence_id = [string]$boundary.terminalEvidenceId; limit = 100 } }) }
        $handoff = [ordered]@{
            schemaVersion = 'webforms-application-page-handoff.v1'
            ruleId = 'diagnostic.webforms.application-page-handoff.v1'
            claimLevel = 'local-only'
            provenance = [ordered]@{ generator = 'scripts/New-FocusedWebFormsApplicationWorkbench.ps1'; generatorSha256 = $generatorSha256; generatorCanonicalization = 'raw-file-bytes'; inputKind = 'webforms-modernization-packet.v1'; inputSha256 = $packetSha256; inputCanonicalization = 'raw-file-bytes'; reviewOverlayKind = if ($ReviewPath) { 'wits-application-review.v1' } else { 'not-supplied' }; reviewOverlaySha256 = $reviewSha256; reviewOverlayCanonicalization = if ($ReviewPath) { 'raw-file-bytes' } else { 'not-applicable' } }
            pageId = $pageId
            packet = [ordered]@{ packetId = [string]$packet.packetId; scanId = [string]$sources[0].scanId; commitSha = [string]$sources[0].commitSha; sources = @($sources | ForEach-Object { [ordered]@{ sourceId = [string]$_.sourceId; scanId = [string]$_.scanId; commitSha = [string]$_.commitSha } }) }
            subject = [ordered]@{ surfaceId = [string]$surface.surfaceId; surfaceKind = [string]$surface.surfaceKind; projectId = [string]$surface.projectId; filePath = [string]$surface.evidence.filePath }
            analysis = [ordered]@{
                status = $analysisStatus
                coverage = [string]$packet.coverage
                coverageReductionReasons = @(Values (Property-Value $packet.summary 'coverageReductionReasons'))
                packetTruncated = [bool]$packet.summary.truncated
                packetTruncationScope = 'application-packet'
                packetTruncationReasons = @(Values (Property-Value $packet.summary 'truncationReasons'))
                pageTraversalTruncated = ($pathDetailTruncatedChainCount -gt 0)
                pageTraversalTruncationReasons = @($pagePathDetailTruncationReasons)
                pagePathEnumerationTruncated = ($pathDetailTruncatedChainCount -gt 0)
                pagePathEnumerationTruncationReasons = @($pagePathDetailTruncationReasons)
                pageTerminalReachabilityAvailable = ($terminalInventoryUnavailableChainCount -eq 0)
                pageTerminalReachabilityComplete = if ($terminalInventoryUnavailableChainCount -gt 0) { $null } else { $terminalInventoryIncompleteChainCount -eq 0 }
                pageTerminalReachabilityLimitReasons = @($pageTerminalReachabilityLimitReasons)
                boundaryStatus = $boundaryStatus
            }
            counts = [ordered]@{ controls = @(Values $surface.controlIds).Count; eventChains = $chains.Count; clientBehaviors = $clientBehavior.Count; serverBehaviors = $serverBehavior.Count; boundaries = $boundaries.Count; identityState = $identity.Count; projectDataMovement = $projectBatchCount; structuralCandidates = $candidates.Count; gaps = $gaps.Count; retainedCalls = $chainAssociatedCallCount; chainAssociatedRetainedCalls = $chainAssociatedCallCount; reportedCallProjections = [int]$reportedCallProjectionCount; omittedCallProjections = $callEvidenceOmittedCount; uniqueRetainedCallFacts = $uniqueRetainedCallCount; normalizedCallSites = $uniqueCallSiteCount; callEvidenceCeilingChains = $callEvidenceCeilingChainCount }
            callTechnologyFamilies = @($technologyFamilies)
            chainOutcomes = [ordered]@{ unresolvedHandlers = $unresolvedHandlerCount; downstreamWithoutSupportedTerminal = $downstreamWithoutTerminalCount; noObservedDownstream = $noDownstreamCount; terminalInventoryIncomplete = $terminalInventoryIncompleteChainCount; terminalInventoryUnavailable = $terminalInventoryUnavailableChainCount; pathDetailTruncated = $pathDetailTruncatedChainCount; truncated = $pathDetailTruncatedChainCount; otherIncomplete = $otherIncompleteCount }
            eventChains = @($chains | ForEach-Object { [ordered]@{
                chainId = [string]$_.chainId; eventSourceId = [string]$_.eventSourceId; bindingFactId = [string]$_.bindingFactId
                handlerId = [string]$_.handlerId; handlerFactId = [string]$_.handlerFactId; handlerSymbol = $_.handlerSymbol
                handlerResolution = [string](Property-Value $_ 'handlerResolution')
                classification = [string]$_.classification; legacyPathId = $_.legacyPathId; terminalKind = $_.terminalKind
                traversalStopState = $_.traversalObservation.stopState
                traversalTruncationReasons = @(Values (Property-Value $_.traversalObservation 'truncationReasons'))
                terminalReachabilityAvailable = Property-Value $_.traversalObservation 'terminalReachabilityAvailable'
                terminalReachabilityComplete = Property-Value $_.traversalObservation 'terminalReachabilityComplete'
                distinctReachableTerminalCount = Property-Value $_.traversalObservation 'distinctReachableTerminalCount'
                reachableTerminalIds = @(Values (Property-Value $_.traversalObservation 'reachableTerminalIds'))
                minimumTerminalDistance = Property-Value $_.traversalObservation 'minimumTerminalDistance'
                terminalReachabilityLimitReasons = @(Values (Property-Value $_.traversalObservation 'terminalReachabilityLimitReasons'))
                pathEnumerationTruncated = Property-Value $_.traversalObservation 'pathEnumerationTruncated'
                pathEnumerationTruncationReasons = @(Values (Property-Value $_.traversalObservation 'pathEnumerationTruncationReasons'))
                nextEvidenceKind = [string](Property-Value $_ 'nextEvidenceKind')
                unresolvedCallTargets = @(Values (Property-Value $_ 'unresolvedCallTargets'))
                nextEvidenceInputs = @(Values (Property-Value $_ 'nextEvidenceInputs'))
                evidence = @((Values $_.evidence) | ForEach-Object { Project-Evidence $_ })
                pathEvidence = @((Values $_.pathEvidence) | ForEach-Object { Project-PathEvidence $_ })
                supportingFactIds = @(Values $_.supportingFactIds); supportingEdgeIds = @(Values $_.supportingEdgeIds)
                callEvidence = @((Values $_.callEvidence) | ForEach-Object { [ordered]@{ callEvidenceId = [string]$_.callEvidenceId; callSiteId = Property-Value $_ 'callSiteId'; calleeName = [string]$_.calleeName; callKind = [string]$_.callKind; resolution = Property-Value $_ 'resolution'; technologyFamily = Property-Value $_ 'technologyFamily'; declaringType = Property-Value $_ 'declaringType'; assemblyName = Property-Value $_ 'assemblyName'; evidence = Project-Evidence $_.evidence; limitations = @(Values $_.limitations) } })
                callEvidenceTotalCount = [int]$(if ($null -ne $_.callEvidenceTotalCount) { $_.callEvidenceTotalCount } else { @(Values $_.callEvidence).Count })
                callEvidenceTruncated = [bool]$_.callEvidenceTruncated
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
                clientBehavior = @($clientBehavior | ForEach-Object { [ordered]@{ id = [string]$_.clientBehaviorId; kind = [string]$_.behaviorKind; selectorKind = [string]$_.selectorKind; selectorTarget = $_.selectorTarget; targetResolution = [string]$_.targetResolution; safeMetadata = $_.safeMetadata; evidenceFactId = [string]$_.evidence.factId; supportingFactIds = @(Values $_.supportingFactIds) } })
                serverBehavior = @($serverBehavior | ForEach-Object { [ordered]@{ id = [string]$_.serverBehaviorId; kind = [string]$_.behaviorKind; targetResolution = [string]$_.targetResolution; safeMetadata = $_.safeMetadata; evidenceFactId = [string]$_.evidence.factId; supportingFactIds = @(Values $_.supportingFactIds) } })
                identityState = @($identity | ForEach-Object { [ordered]@{ id = [string]$_.identityStateId; kind = [string]$_.identityKind; classification = [string]$_.classification; safeMetadata = $_.safeMetadata; evidenceFactId = [string]$_.evidence.factId; supportingFactIds = @(Values $_.supportingFactIds) } })
                structuralCandidates = @($candidates | ForEach-Object { [ordered]@{ id = [string]$_.candidateId; classification = [string]$_.classification; ruleId = [string]$_.ruleId; evidenceTier = [string]$_.evidenceTier; surfaceIds = @(Values $_.surfaceIds); supportingFactIds = @(Values $_.supportingFactIds) } })
            }
            gaps = @($gaps | ForEach-Object { [ordered]@{ gapId = [string]$_.gapId; classification = [string]$_.classification; scopeKind = [string]$_.scopeKind; scopeId = $_.scopeId; ruleId = [string]$_.ruleId; evidenceTier = [string]$_.evidenceTier; coverageLabel = [string]$_.coverageLabel; commitSha = [string]$_.commitSha; filePath = $_.filePath; startLine = $_.startLine; endLine = $_.endLine; extractorId = $_.extractorId; extractorVersion = $_.extractorVersion; safeMetadata = if ($null -eq $_.safeMetadata) { [ordered]@{} } else { $_.safeMetadata }; truncationReason = $_.truncationReason; supportingFactIds = @(Values $_.supportingFactIds); limitations = @(Values $_.limitations) } })
            nextEvidenceSummary = @($nextEvidenceSummary)
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
            $bindingLocation = @(Values $chain.evidence | Where-Object { $_.ruleId -in @('legacy.webforms.event-binding.v1','legacy.webforms.inline-client-http-request.v1') } | Sort-Object filePath, startLine | Select-Object -First 1)
            $handlerLocation = @(Values $chain.evidence | Where-Object { $_.ruleId -in @('legacy.webforms.handler-resolution.v1','legacy.webforms.client-http-handler-resolution.v1') } | Sort-Object filePath, startLine | Select-Object -First 1)
            $bindingSpan = if ($bindingLocation.Count) { "$($bindingLocation[0].filePath):L$($bindingLocation[0].startLine)-$($bindingLocation[0].endLine)" } else { 'span unavailable' }
            $handlerSpan = if ($handlerLocation.Count) { "$($handlerLocation[0].filePath):L$($handlerLocation[0].startLine)-$($handlerLocation[0].endLine)" } else { 'span unavailable' }
            $calls = @(Values $chain.callEvidence | Sort-Object @{ Expression = { [string]$_.evidence.filePath } }, @{ Expression = { [int]$_.evidence.startLine } }, calleeName, callEvidenceId)
            $callSites = @(Get-NormalizedCallSites $calls | Sort-Object @{ Expression = { [string]$_.Preferred.evidence.filePath } }, @{ Expression = { [int]$_.Preferred.evidence.startLine } }, @{ Expression = { [string]$_.Preferred.calleeName } }, CallSiteId)
            $displayedCalls = @($callSites | Select-Object -First 24)
            $callItems = @($displayedCalls | ForEach-Object {
                $call = $_.Preferred
                $resolution = [string](Property-Value $call 'resolution')
                if (!$resolution) { $resolution = if (([string]$call.callKind) -like 'Semantic*') { 'compiler-resolved' } else { 'syntax-only' } }
                $family = [string](Property-Value $call 'technologyFamily')
                if (!$family) { $family = if ($resolution -eq 'compiler-resolved') { 'resolved-unspecified' } else { 'unresolved' } }
                $declaringType = [string](Property-Value $call 'declaringType')
                $typeDetail = if ($declaringType) { "; declaring type <code>$(ConvertTo-HtmlText $declaringType)</code>" } else { '' }
                $paired = if ($_.HasSemanticEvidence -and $_.HasSyntaxEvidence) { '; semantic + syntax evidence retained' } elseif ($_.EvidenceFactCount -gt 1) { "; $($_.EvidenceFactCount) evidence facts retained" } else { '' }
                '<li><code>{0}</code> <small>{1}; {2}{3}{4}; {5}:L{6}-{7}</small></li>' -f (ConvertTo-HtmlText $call.calleeName), (ConvertTo-HtmlText $call.callKind), (ConvertTo-HtmlText "$resolution / $family"), $typeDetail, $paired, (ConvertTo-HtmlText $call.evidence.filePath), (ConvertTo-HtmlText $call.evidence.startLine), (ConvertTo-HtmlText $call.evidence.endLine)
            })
            $callTotal = if ($null -ne $chain.callEvidenceTotalCount) { [int]$chain.callEvidenceTotalCount } else { $calls.Count }
            $ceilingNote = if ($chain.callEvidenceTruncated) { ' Explicit packet truncation is reported.' } elseif ($callTotal -ge 256) { ' The 256-fact retention ceiling was reached; additional evidence may be unavailable.' } else { '' }
            $callNote = if ($chain.callEvidenceTruncated -or $callTotal -ge 256 -or $callSites.Count -gt $displayedCalls.Count) { "<small>$($displayedCalls.Count) shown of $($callSites.Count) normalized call sites from $callTotal retained fact projections.$ceilingNote</small>" } else { '' }
            $callHtml = if ($calls.Count -gt 0) { '<details class="calls"><summary>{0} sites / {1} retained facts</summary><ul>{2}</ul>{3}</details>' -f $callSites.Count, $callTotal, ($callItems -join ''), $callNote } else { '<span class="muted">none retained</span>' }
            $handlerResolution = [string](Property-Value $chain 'handlerResolution')
            if (!$handlerResolution) { $handlerResolution = if ($chain.handlerFactId) { 'resolved-static-handler' } else { 'unavailable-unclassified' } }
            '<tr><td><code>{0}</code></td><td>{1}<br><small>{2}</small></td><td><code>{3}</code><br><small>{4}; {8}</small></td><td>{5}</td><td><code>{6}</code></td><td>{7}</td></tr>' -f (ConvertTo-HtmlText $chain.chainId), (ConvertTo-HtmlText $chain.eventSourceId), (ConvertTo-HtmlText $bindingSpan), (ConvertTo-HtmlText $(if ($chain.handlerSymbol) { $chain.handlerSymbol } else { $chain.handlerId })), (ConvertTo-HtmlText $handlerSpan), $callHtml, (ConvertTo-HtmlText $chain.classification), (ConvertTo-HtmlText $(if ($chain.terminalKind) { $chain.terminalKind } else { $chain.traversalObservation.stopState })), (ConvertTo-HtmlText $handlerResolution)
        }
        $boundaryRows = foreach ($boundary in $boundaries) { '<tr><td><code>{0}</code></td><td>{1}</td><td>{2}</td><td><code>{3}</code></td></tr>' -f (ConvertTo-HtmlText $boundary.boundaryId), (ConvertTo-HtmlText $boundary.boundaryCategory), (ConvertTo-HtmlText $boundary.boundaryKind), (ConvertTo-HtmlText $boundary.boundaryTargetId) }
        $gapRows = foreach ($gap in $gaps) { '<li><code>{0}</code> <code>{1}</code> <code>{2}</code> — {3}; {4}:L{5}-{6}; commit <code>{7}</code>; extractor <code>{8}/{9}</code>; support <code>{10}</code></li>' -f (ConvertTo-HtmlText $gap.gapId), (ConvertTo-HtmlText $gap.ruleId), (ConvertTo-HtmlText $gap.evidenceTier), (ConvertTo-HtmlText $gap.classification), (ConvertTo-HtmlText $gap.filePath), (ConvertTo-HtmlText $gap.startLine), (ConvertTo-HtmlText $gap.endLine), (ConvertTo-HtmlText $gap.commitSha), (ConvertTo-HtmlText $gap.extractorId), (ConvertTo-HtmlText $gap.extractorVersion), (ConvertTo-HtmlText ((Values $gap.supportingFactIds) -join ', ')) }
        $gapCategoryRows = foreach ($category in $gapCategories) { '<tr><td><code>{0}</code></td><td>{1}</td></tr>' -f (ConvertTo-HtmlText $category.classification), $category.count }
        $identityRows = foreach ($item in $identity) { '<tr><td><code>{0}</code></td><td>{1}</td><td>{2}</td><td><code>{3}</code></td><td><code>{4}</code></td></tr>' -f (ConvertTo-HtmlText $item.identityStateId), (ConvertTo-HtmlText $item.identityKind), (ConvertTo-HtmlText $item.classification), (ConvertTo-HtmlText (($item.safeMetadata | ConvertTo-Json -Compress))), (ConvertTo-HtmlText $item.evidence.factId) }
        $candidateRows = foreach ($item in $candidates) { '<tr><td><code>{0}</code></td><td>{1}</td><td><code>{2}</code></td><td><code>{3}</code></td></tr>' -f (ConvertTo-HtmlText $item.candidateId), (ConvertTo-HtmlText $item.classification), (ConvertTo-HtmlText $item.ruleId), (ConvertTo-HtmlText ((Values $item.supportingFactIds) -join ', ')) }
        $clientBehaviorRows = foreach ($item in $clientBehavior) {
            $httpMethod = Property-Value $item.safeMetadata 'httpMethod'
            $endpointName = Property-Value $item.safeMetadata 'endpointName'
            $endpointFile = Property-Value $item.safeMetadata 'endpointDeclarationFile'
            $eventOrMutation = if ($httpMethod -and $httpMethod -ne 'not-applicable') { "$httpMethod $($item.safeMetadata.endpointKind)" } elseif ($item.safeMetadata.clientEventName -and $item.safeMetadata.clientEventName -ne 'not-applicable') { $item.safeMetadata.clientEventName } elseif ($item.safeMetadata.mutationKinds -and $item.safeMetadata.mutationKinds -ne 'not-applicable') { $item.safeMetadata.mutationKinds } elseif ($item.safeMetadata.constraintKind -and $item.safeMetadata.constraintKind -ne 'not-applicable') { "$($item.safeMetadata.constraintKind)=$($item.safeMetadata.constraintValue)" } else { 'not-applicable' }
            $control = if ($endpointName -and $endpointName -ne 'not-applicable') { $endpointName } elseif ($item.safeMetadata.controlId -and $item.safeMetadata.controlId -ne 'unresolved') { $item.safeMetadata.controlId } else { 'unresolved' }
            $serverHandler = if ($endpointFile -and $endpointFile -ne 'not-applicable') { $endpointFile } elseif ($item.safeMetadata.serverHandlerName -and $item.safeMetadata.serverHandlerName -ne 'not-applicable') { $item.safeMetadata.serverHandlerName } else { 'not-applicable' }
            '<tr><td>{0}</td><td>{1}</td><td><code>{2}</code><br><small>{3}</small></td><td><code>{4}</code><br><small>{5}</small></td><td><code>{6}</code></td><td>{7}:L{8}-{9}</td></tr>' -f (ConvertTo-HtmlText $item.behaviorKind), (ConvertTo-HtmlText $eventOrMutation), (ConvertTo-HtmlText $item.selectorTarget), (ConvertTo-HtmlText $item.selectorKind), (ConvertTo-HtmlText $control), (ConvertTo-HtmlText $item.targetResolution), (ConvertTo-HtmlText $serverHandler), (ConvertTo-HtmlText $item.evidence.filePath), (ConvertTo-HtmlText $item.evidence.startLine), (ConvertTo-HtmlText $item.evidence.endLine)
        }
        $serverBehaviorRows = foreach ($item in $serverBehavior) {
            $referencedType = Property-Value $item.safeMetadata 'referencedTypeName'
            $declarationFile = Property-Value $item.safeMetadata 'declarationFile'
            $expressionKind = Property-Value $item.safeMetadata 'expressionKind'
            $subject = if ($referencedType -and $referencedType -ne 'not-applicable') { $referencedType } elseif ($item.safeMetadata.controlId -and $item.safeMetadata.controlId -ne 'not-applicable') { "$($item.safeMetadata.controlId).$($item.safeMetadata.stateMember)" } elseif ($item.safeMetadata.navigationKind -and $item.safeMetadata.navigationKind -ne 'not-applicable') { $item.safeMetadata.navigationKind } else { $item.safeMetadata.lifecycleOperation }
            $context = if ($item.safeMetadata.branchContext -and $item.safeMetadata.branchContext -ne 'unconditional') { $item.safeMetadata.branchContext } else { 'unconditional' }
            $detail = if ($referencedType -and $referencedType -ne 'not-applicable') { "$($item.targetResolution); $expressionKind; $declarationFile" } elseif ($item.safeMetadata.endResponse -and $item.safeMetadata.endResponse -ne 'not-applicable') { "$($item.targetResolution); endResponse=$($item.safeMetadata.endResponse)" } else { $item.targetResolution }
            $handler = if ($item.safeMetadata.handlerName -and $item.safeMetadata.handlerName -ne 'unavailable') { $item.safeMetadata.handlerName } elseif ($referencedType -and $referencedType -ne 'not-applicable') { 'markup expression' } else { 'unavailable' }
            '<tr><td>{0}</td><td><code>{1}</code></td><td><code>{2}</code></td><td>{3}</td><td>{4}</td><td>{5}:L{6}-{7}</td></tr>' -f (ConvertTo-HtmlText $item.behaviorKind), (ConvertTo-HtmlText $handler), (ConvertTo-HtmlText $subject), (ConvertTo-HtmlText $context), (ConvertTo-HtmlText $detail), (ConvertTo-HtmlText $item.evidence.filePath), (ConvertTo-HtmlText $item.evidence.startLine), (ConvertTo-HtmlText $item.evidence.endLine)
        }
        $correctionHtml = if ($null -ne $decision.correction) { '<p><strong>Correction ({0}):</strong> {1}</p>' -f (ConvertTo-HtmlText $decision.correction.category), (ConvertTo-HtmlText $decision.correction.statement) } else { '' }
        $sourceHtml = if ($IncludeRawSource) { Source-Excerpt $surface.evidence $SourceRoot $SourceContextLines } else { '<p class="muted">Raw source omitted. Regenerate with <code>-IncludeRawSource -SourceRoot &lt;authorized-root&gt;</code> for a bounded private excerpt.</p>' }
        $html = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>$(ConvertTo-HtmlText $pageId) Web Forms review</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1400px;margin:auto;padding:24px}.private,.warning{padding:12px;border-left:5px solid #c62828;background:#fff1f0}.summary{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px}.card,details{background:white;border:1px solid #dbe2ee;border-radius:8px;padding:14px;margin:14px 0}.summary .card{margin:0}.calls{padding:6px;margin:0}.calls ul{margin:.5rem 0;padding-left:1.25rem}.calls li{margin:.25rem 0}table{width:100%;border-collapse:collapse}th,td{padding:9px;border:1px solid #dbe2ee;text-align:left;vertical-align:top}th{background:#eaf1ff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px;overflow-wrap:anywhere}pre{overflow:auto;background:#172033;color:#f8fafc;padding:14px;border-radius:6px}pre code{background:transparent;padding:0;color:inherit;white-space:pre}.muted{color:#566070}.button{display:inline-block;padding:7px 10px;background:#eaf1ff;border:1px solid #bed0ee;border-radius:6px;text-decoration:none}</style></head><body><main>
<p><a class="button" href="index.html">Return to application index</a></p><h1>$(ConvertTo-HtmlText $surface.evidence.filePath)</h1><p class="private">PRIVATE local evidence review. Human conclusions are review metadata, not scanner facts.</p>
<section class="summary"><div class="card"><strong>Controls</strong><br>$(@(Values $surface.controlIds).Count)</div><div class="card"><strong>Event chains</strong><br>$($chains.Count)</div><div class="card"><strong>Client behaviors</strong><br>$($clientBehavior.Count)</div><div class="card"><strong>Server behaviors</strong><br>$($serverBehavior.Count)</div><div class="card"><strong>Boundaries</strong><br>$($boundaries.Count)<br><small>$(ConvertTo-HtmlText $boundaryStatus)</small></div><div class="card"><strong>Retained call projections</strong><br>$chainAssociatedCallCount</div><div class="card"><strong>Unique call facts</strong><br>$uniqueRetainedCallCount</div><div class="card"><strong>Normalized call sites</strong><br>$uniqueCallSiteCount</div><div class="card"><strong>Call evidence ceiling</strong><br>$callEvidenceCeilingChainCount chain(s)<br><small>$callEvidenceOmittedCount explicitly omitted</small></div><div class="card"><strong>Handler unavailable</strong><br>$unresolvedHandlerCount<br><small>event source retained; usable handler fact unavailable</small></div><div class="card"><strong>Downstream / no terminal</strong><br>$downstreamWithoutTerminalCount</div><div class="card"><strong>No downstream</strong><br>$noDownstreamCount</div><div class="card"><strong>Terminal inventory incomplete</strong><br>$terminalInventoryIncompleteChainCount<br><small>retained-graph safety limit reached</small></div><div class="card"><strong>Terminal inventory unavailable</strong><br>$terminalInventoryUnavailableChainCount<br><small>no reachability conclusion available</small></div><div class="card"><strong>Path detail truncated</strong><br>$pathDetailTruncatedChainCount<br><small>terminal inventory reported separately</small></div><div class="card"><strong>Other incomplete chains</strong><br>$otherIncompleteCount</div><div class="card"><strong>Recorded gap facts</strong><br>$($gaps.Count)</div></section>
<section class="card"><h2>Evidence-backed behavior summary</h2><p>$(ConvertTo-HtmlText $behaviorSummary)</p><p><strong>Boundary status:</strong> $(ConvertTo-HtmlText $boundaryStatus).</p><p class="muted">Counts and correlations summarize retained static facts; they do not assert runtime behavior or business intent.</p></section>
<section class="card"><h2>Review status</h2><p><strong>Verdict:</strong> $(ConvertTo-HtmlText $decision.verdict) · <strong>Disposition:</strong> $(ConvertTo-HtmlText $decision.migrationDisposition)</p><p><strong>Capability:</strong> $(ConvertTo-HtmlText $decision.capabilityLabel)</p><p>$(ConvertTo-HtmlText $decision.comment)</p>$correctionHtml<p>Human review is a separate validated overlay, never scanner evidence.</p></section>
<section class="card"><h2>Surface</h2><p><code>$(ConvertTo-HtmlText $surface.surfaceId)</code> · $(ConvertTo-HtmlText $surface.surfaceKind) · project <code>$(ConvertTo-HtmlText $surface.projectId)</code></p><p><strong>Coverage:</strong> $(ConvertTo-HtmlText ($coverage -join ', '))</p><p><strong>Controls:</strong> $(ConvertTo-HtmlText $controlDisplayText)</p></section>
<section class="card"><h2>Trigger and retained call paths</h2><table><thead><tr><th>Chain</th><th>Event source</th><th>Handler</th><th>Call accounting</th><th>Chain conclusion</th><th>Terminal/stop</th></tr></thead><tbody>$($chainRows -join '')</tbody></table><p class="muted">Normalized call sites collapse syntax and semantic facts only when their retained site identity matches. Compiler-resolved declaring types and technology families are static evidence; they do not prove runtime receiver type, branch execution, or dynamic dispatch.</p></section>
<details open><summary><strong>Inline client behavior ($($clientBehavior.Count))</strong></summary><table><thead><tr><th>Kind</th><th>Event / effect</th><th>Selector</th><th>Control / resolution</th><th>Server handler</th><th>Evidence span</th></tr></thead><tbody>$($clientBehaviorRows -join '')</tbody></table><p class="muted">Static candidates only; browser execution, DOM selection, postback, and server-handler execution are not proven.</p></details>
<details open><summary><strong>Server behavior ($($serverBehavior.Count))</strong></summary><table><thead><tr><th>Kind</th><th>Handler</th><th>Control / operation</th><th>Branch</th><th>Resolution / detail</th><th>Evidence span</th></tr></thead><tbody>$($serverBehaviorRows -join '')</tbody></table><p class="muted">Retained static candidates only; branch execution, navigation, request termination, and rendered control state are not proven.</p></details>
<details><summary><strong>Downstream boundaries ($($boundaries.Count))</strong></summary><table><thead><tr><th>ID</th><th>Category</th><th>Kind</th><th>Target</th></tr></thead><tbody>$($boundaryRows -join '')</tbody></table></details>
<details><summary><strong>Identity/state ($($identity.Count))</strong></summary><table><thead><tr><th>ID</th><th>Kind</th><th>Classification</th><th>Safe metadata</th><th>Evidence</th></tr></thead><tbody>$($identityRows -join '')</tbody></table></details>
<details><summary><strong>Project-scoped data movement ($projectBatchCount)</strong></summary><p>Stored once in the application handoff and application index because project association does not prove page association.</p></details>
<details><summary><strong>Structural candidates ($($candidates.Count))</strong></summary><table><thead><tr><th>ID</th><th>Classification</th><th>Rule</th><th>Support</th></tr></thead><tbody>$($candidateRows -join '')</tbody></table></details>
<details><summary><strong>Gap categories ($($gapCategories.Count))</strong></summary><table><thead><tr><th>Classification</th><th>Count</th></tr></thead><tbody>$($gapCategoryRows -join '')</tbody></table></details>
<details><summary><strong>Explicit gaps ($($gaps.Count))</strong></summary><ul>$($gapRows -join '')</ul></details>
<details><summary><strong>Evidence citations ($($evidence.Count))</strong></summary>$(Evidence-List $evidence)</details>
<details><summary><strong>Retained call-path locations ($($pathEvidence.Count))</strong></summary>$(Path-Evidence-List $pathEvidence)</details>
<details><summary><strong>Working-tree source excerpt</strong></summary>$sourceHtml</details>
<details><summary><strong>Agent evidence handoff</strong></summary><p><a href="$pageId.handoff.json">Open $pageId.handoff.json</a>. It provides stable evidence IDs and closed TraceMap recipe hints; it is not a BRD.</p></details>
</main></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $staging "$pageId.html"), $html, [Text.UTF8Encoding]::new($false))
        $pageRows.Add([pscustomobject]@{ PageId = $pageId; Path = [string]$surface.evidence.filePath; ControlDisplay = $controlDisplayText; SurfaceKind = [string]$surface.surfaceKind; Controls = @(Values $surface.controlIds).Count; Chains = $chains.Count; ClientBehaviors = $clientBehavior.Count; ServerBehaviors = $serverBehavior.Count; Boundaries = $boundaries.Count; CallProjections = $chainAssociatedCallCount; ReportedCallProjections = [int]$reportedCallProjectionCount; CallEvidenceOmitted = $callEvidenceOmittedCount; CallEvidenceCeilingChains = $callEvidenceCeilingChainCount; UniqueCallFacts = $uniqueRetainedCallCount; UniqueCallSites = $uniqueCallSiteCount; HandlerUnavailable = $unresolvedHandlerCount; NoTerminal = $downstreamWithoutTerminalCount; NoDownstream = $noDownstreamCount; TerminalInventoryIncomplete = $terminalInventoryIncompleteChainCount; TerminalInventoryUnavailable = $terminalInventoryUnavailableChainCount; PathDetailTruncated = $pathDetailTruncatedChainCount; Truncated = $pathDetailTruncatedChainCount; OtherIncomplete = $otherIncompleteCount; Gaps = $gaps.Count; GapCategories = @($gapCategories); Coverage = ($coverage -join ', '); Verdict = [string]$decision.verdict; Disposition = [string]$decision.migrationDisposition })
        $applicationPages.Add([ordered]@{ pageId = $pageId; surfaceId = [string]$surface.surfaceId; filePath = [string]$surface.evidence.filePath; report = "$pageId.html"; handoff = "$pageId.handoff.json"; counts = $handoff.counts; chainOutcomes = $handoff.chainOutcomes; pageTraversalTruncated = $handoff.analysis.pageTraversalTruncated; pageTraversalTruncationReasons = @($handoff.analysis.pageTraversalTruncationReasons); pagePathEnumerationTruncated = $handoff.analysis.pagePathEnumerationTruncated; pagePathEnumerationTruncationReasons = @($handoff.analysis.pagePathEnumerationTruncationReasons); pageTerminalReachabilityComplete = $handoff.analysis.pageTerminalReachabilityComplete; pageTerminalReachabilityLimitReasons = @($handoff.analysis.pageTerminalReachabilityLimitReasons); nextEvidenceSummary = @($nextEvidenceSummary); gapCategories = @($gapCategories) })
    }

    $applicationGaps = @(Values $packet.gaps | Where-Object { !$associatedGapIds.Contains([string](Property-Value $_ 'gapId')) } | Sort-Object gapId)
    $unassociatedIdentity = @(Values $packet.identityStateInventory | Where-Object { $null -eq (Property-Value $_ 'surfaceId') } | Sort-Object identityStateId)
    $surfaceProjectIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($surface in $ordered) { if ($surface.projectId) { [void]$surfaceProjectIds.Add([string]$surface.projectId) } }
    $applicationBatch = @(Values $packet.batchDataMovementInventory | Where-Object {
        $projectId = Property-Value $_ 'projectId'
        $null -ne $projectId -and $surfaceProjectIds.Contains([string]$projectId)
    } | Sort-Object projectId, batchDataMovementId)
    $unassociatedBatch = @(Values $packet.batchDataMovementInventory | Where-Object {
        $projectId = Property-Value $_ 'projectId'
        $null -eq $projectId -or !$surfaceProjectIds.Contains([string]$projectId)
    } | Sort-Object batchDataMovementId)
    $surfaceIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($surface in $ordered) { [void]$surfaceIds.Add([string]$surface.surfaceId) }
    $unassociatedCandidates = @(Values $packet.structuralSliceCandidates | Where-Object { @((Values (Property-Value $_ 'surfaceIds')) | Where-Object { $surfaceIds.Contains([string]$_) }).Count -eq 0 } | Sort-Object candidateId)
    $applicationStatus = if ([string]$packet.coverage -like 'reduced-*' -or $packet.summary.truncated -or @(Values $packet.gaps).Count -gt 0) { 'partial' } else { 'complete' }
    $applicationNextEvidenceSummary = @($packet.eventChains | Where-Object {
        $kind = [string](Property-Value $_ 'nextEvidenceKind')
        $kind -and $kind -ne 'none'
    } | Group-Object { [string](Property-Value $_ 'nextEvidenceKind') } | Sort-Object Name | ForEach-Object {
        [ordered]@{
            kind = [string]$_.Name
            chainCount = [int]$_.Count
            targets = @($_.Group | ForEach-Object { Values (Property-Value $_ 'unresolvedCallTargets') } | Where-Object { $_ } | Sort-Object -Unique)
            requiredInputs = @($_.Group | ForEach-Object { Values (Property-Value $_ 'nextEvidenceInputs') } | Where-Object { $_ } | Sort-Object -Unique)
        }
    })
    $controlRegistrationGaps = @(Values $packet.gaps | Where-Object {
        ([string]$_.classification) -in @('UnresolvedWebFormsControlRegistration','UnsupportedWebFormsUserControlRegistration')
    } | Sort-Object classification, gapId | ForEach-Object {
        [ordered]@{
            gapId = [string]$_.gapId
            classification = [string]$_.classification
            safeMetadata = if ($null -eq $_.safeMetadata) { [ordered]@{} } else { $_.safeMetadata }
            supportingFactIds = @(Values $_.supportingFactIds)
        }
    })
    $rankedRows = @($pageRows | Sort-Object `
        @{ Expression = { [int]$_.CallEvidenceCeilingChains }; Descending = $true }, `
        @{ Expression = { [int]$_.Truncated }; Descending = $true }, `
        @{ Expression = { [int]$_.TerminalInventoryUnavailable }; Descending = $true }, `
        @{ Expression = { [int]$_.HandlerUnavailable }; Descending = $true }, `
        @{ Expression = { [int]$_.OtherIncomplete }; Descending = $true }, `
        @{ Expression = { [int]$_.Gaps }; Descending = $true }, `
        @{ Expression = { [int]$_.UniqueCallSites }; Descending = $true }, `
        @{ Expression = { [int]$_.CallProjections - [int]$_.UniqueCallFacts }; Descending = $true }, `
        @{ Expression = { [string]$_.PageId } })
    $outlierPages = [Collections.Generic.List[object]]::new()
    $reviewOrder = 0
    foreach ($row in $rankedRows) {
        $reviewOrder++
        $projectionReuse = [Math]::Max(0, [int]$row.CallProjections - [int]$row.UniqueCallFacts)
        $signals = [Collections.Generic.List[string]]::new()
        if ($row.TerminalInventoryIncomplete -gt 0) { $signals.Add('terminal-inventory-incomplete') }
        if ($row.TerminalInventoryUnavailable -gt 0) { $signals.Add('terminal-inventory-unavailable') }
        if ($row.PathDetailTruncated -gt 0) { $signals.Add('path-detail-truncated') }
        if ($row.CallEvidenceCeilingChains -gt 0) { $signals.Add('call-evidence-ceiling-reached') }
        if ($row.CallEvidenceOmitted -gt 0) { $signals.Add('call-evidence-explicitly-omitted') }
        if ($row.HandlerUnavailable -gt 0) { $signals.Add('handler-unavailable') }
        if ($row.OtherIncomplete -gt 0) { $signals.Add('other-incomplete-chain') }
        if ($row.NoTerminal -gt 0) { $signals.Add('downstream-without-supported-terminal') }
        if ($row.NoDownstream -gt 0) { $signals.Add('no-observed-downstream') }
        if ($projectionReuse -gt 0) { $signals.Add('call-projection-reuse') }
        if ($row.UniqueCallFacts -gt 0 -and $row.Boundaries -eq 0) { $signals.Add('calls-without-observed-boundary') }
        if ($row.Chains -gt 0 -and $row.ClientBehaviors -eq 0 -and $row.ServerBehaviors -eq 0) { $signals.Add('chains-without-specialized-behavior-facts') }
        if ($row.Gaps -gt 0) { $signals.Add('explicit-evidence-gaps') }
        $safeGapCategories = @($row.GapCategories | ForEach-Object {
            $classification = [string]$_.classification
            [ordered]@{
                classification = if ($classification -match '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') { $classification } else { 'unclassified' }
                count = [int]$_.count
            }
        })
        $outlierPages.Add([ordered]@{
            reviewOrder = $reviewOrder
            pageId = [string]$row.PageId
            counts = [ordered]@{
                controls = [int]$row.Controls
                eventChains = [int]$row.Chains
                clientBehaviors = [int]$row.ClientBehaviors
                serverBehaviors = [int]$row.ServerBehaviors
                boundaries = [int]$row.Boundaries
                callProjections = [int]$row.CallProjections
                reportedCallProjections = [int]$row.ReportedCallProjections
                omittedCallProjections = [int]$row.CallEvidenceOmitted
                uniqueCallFacts = [int]$row.UniqueCallFacts
                normalizedCallSites = [int]$row.UniqueCallSites
                callEvidenceCeilingChains = [int]$row.CallEvidenceCeilingChains
                projectionReuse = $projectionReuse
                gaps = [int]$row.Gaps
            }
            chainOutcomes = [ordered]@{
                unresolvedHandlers = [int]$row.HandlerUnavailable
                downstreamWithoutSupportedTerminal = [int]$row.NoTerminal
                noObservedDownstream = [int]$row.NoDownstream
                terminalInventoryIncomplete = [int]$row.TerminalInventoryIncomplete
                terminalInventoryUnavailable = [int]$row.TerminalInventoryUnavailable
                pathDetailTruncated = [int]$row.PathDetailTruncated
                truncated = [int]$row.Truncated
                otherIncomplete = [int]$row.OtherIncomplete
            }
            gapCategories = $safeGapCategories
            reviewSignals = @($signals)
        })
    }
    $shareableProjectionSha256 = Get-TextSha256 (ConvertTo-Json -InputObject @($outlierPages) -Depth 12 -Compress)
    $outlierArtifact = [ordered]@{
        schemaVersion = 'webforms-application-outliers.v1'
        ruleId = 'diagnostic.webforms.application-outlier-ranking.v1'
        privacy = 'anonymous-counts-only'
        provenance = [ordered]@{ generator = 'scripts/New-FocusedWebFormsApplicationWorkbench.ps1'; generatorSha256 = $generatorSha256; generatorCanonicalization = 'raw-file-bytes'; inputKind = 'alias-only-page-count-projection'; inputSha256 = $shareableProjectionSha256; inputCanonicalization = 'powershell-json-compact-depth-12-utf8-v1' }
        pageCount = $outlierPages.Count
        ordering = @('call-evidence-ceiling desc','truncated-traversal desc','terminal-inventory-unavailable desc','handler-unavailable desc','other-incomplete desc','gaps desc','normalized-call-sites desc','projection-reuse desc','page-id asc')
        pages = @($outlierPages)
        limitations = @(
            'Aliases resolve only inside the private workbench; retained paths, symbols, source IDs, packet IDs, scan IDs, and commit SHAs are intentionally omitted.',
            'Ordering is a deterministic inspection aid over retained static counts, not business priority, runtime frequency, migration effort, or a modernization conclusion.',
            'Zero counts do not prove absence when retained coverage is partial or reduced.'
        )
    }
    [IO.File]::WriteAllText((Join-Path $staging 'application-outliers.shareable.json'), (($outlierArtifact | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))
    $outlierSections = @(
        Outlier-Table 'Deterministic inspection order' $outlierPages
        Outlier-Table 'Highest normalized source call-site counts' @($outlierPages | Where-Object { $_.counts.normalizedCallSites -gt 0 } | Sort-Object @{ Expression = { $_.counts.normalizedCallSites }; Descending = $true }, pageId)
        Outlier-Table 'Highest call-projection reuse' @($outlierPages | Where-Object { $_.counts.projectionReuse -gt 0 } | Sort-Object @{ Expression = { $_.counts.projectionReuse }; Descending = $true }, pageId)
        Outlier-Table 'Call-evidence retention ceiling reached' @($outlierPages | Where-Object { $_.counts.callEvidenceCeilingChains -gt 0 } | Sort-Object @{ Expression = { $_.counts.callEvidenceCeilingChains }; Descending = $true }, @{ Expression = { $_.counts.omittedCallProjections }; Descending = $true }, pageId)
        Outlier-Table 'Most unavailable handlers' @($outlierPages | Where-Object { $_.chainOutcomes.unresolvedHandlers -gt 0 } | Sort-Object @{ Expression = { $_.chainOutcomes.unresolvedHandlers }; Descending = $true }, pageId)
        Outlier-Table 'Most other incomplete chains' @($outlierPages | Where-Object { $_.chainOutcomes.otherIncomplete -gt 0 } | Sort-Object @{ Expression = { $_.chainOutcomes.otherIncomplete }; Descending = $true }, pageId)
        Outlier-Table 'Most truncated chains' @($outlierPages | Where-Object { $_.chainOutcomes.truncated -gt 0 } | Sort-Object @{ Expression = { $_.chainOutcomes.truncated }; Descending = $true }, pageId)
        Outlier-Table 'Highest explicit evidence-gap counts' @($outlierPages | Where-Object { $_.counts.gaps -gt 0 } | Sort-Object @{ Expression = { $_.counts.gaps }; Descending = $true }, pageId)
        Outlier-Table 'Retained calls without an observed boundary' @($outlierPages | Where-Object { $_.counts.normalizedCallSites -gt 0 -and $_.counts.boundaries -eq 0 } | Sort-Object @{ Expression = { $_.counts.normalizedCallSites }; Descending = $true }, pageId)
    )
    $outlierHtml = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Alias-only Web Forms outlier review</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1650px;margin:auto;padding:24px}.shareable{padding:12px;border-left:5px solid #287a36;background:#effaf1}table{width:100%;border-collapse:collapse;background:white}th,td{padding:9px;border:1px solid #dbe2ee;text-align:left;vertical-align:top}th{background:#eaf1ff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px}details{background:white;border:1px solid #dbe2ee;border-radius:8px;padding:14px;margin:14px 0}</style></head><body><main><h1>Alias-only Web Forms outlier review</h1><p class="shareable">Anonymous count projection for $($outlierPages.Count) pages. It contains page aliases and retained counts only; paths, symbols, repository identifiers, packet identifiers, scan identifiers, and commit SHAs are omitted.</p><p>The inspection order is deterministic: call-evidence ceiling, truncated traversal, unavailable handlers, other incomplete chains, gaps, normalized call sites, projection reuse, then page alias. A chain at the 256-fact ceiling may have additional unavailable evidence even when explicit omission is zero. Normalized sites collapse retained syntax and semantic facts only when their site identities match. This is not a business-priority or migration-effort score.</p><p>Exact generator and privacy-projected input SHA-256 values are retained in the adjacent JSON.</p>$($outlierSections -join '')<p>Static evidence does not prove runtime execution, business intent, migration effort, or absence under partial coverage.</p></main></body></html>
"@
    [IO.File]::WriteAllText((Join-Path $staging 'application-outliers.shareable.html'), $outlierHtml, [Text.UTF8Encoding]::new($false))
    $sourcePathByPageId = @{}
    foreach ($row in $pageRows) { $sourcePathByPageId[[string]$row.PageId] = [string]$row.Path }
    $privateOutlierSections = @(
        Private-Outlier-Table 'Deterministic inspection order' $outlierPages $sourcePathByPageId
        Private-Outlier-Table 'Highest normalized source call-site counts' @($outlierPages | Where-Object { $_.counts.normalizedCallSites -gt 0 } | Sort-Object @{ Expression = { $_.counts.normalizedCallSites }; Descending = $true }, pageId) $sourcePathByPageId
        Private-Outlier-Table 'Highest call-projection reuse' @($outlierPages | Where-Object { $_.counts.projectionReuse -gt 0 } | Sort-Object @{ Expression = { $_.counts.projectionReuse }; Descending = $true }, pageId) $sourcePathByPageId
        Private-Outlier-Table 'Call-evidence retention ceiling reached' @($outlierPages | Where-Object { $_.counts.callEvidenceCeilingChains -gt 0 } | Sort-Object @{ Expression = { $_.counts.callEvidenceCeilingChains }; Descending = $true }, @{ Expression = { $_.counts.omittedCallProjections }; Descending = $true }, pageId) $sourcePathByPageId
        Private-Outlier-Table 'Most unavailable handlers' @($outlierPages | Where-Object { $_.chainOutcomes.unresolvedHandlers -gt 0 } | Sort-Object @{ Expression = { $_.chainOutcomes.unresolvedHandlers }; Descending = $true }, pageId) $sourcePathByPageId
        Private-Outlier-Table 'Most other incomplete chains' @($outlierPages | Where-Object { $_.chainOutcomes.otherIncomplete -gt 0 } | Sort-Object @{ Expression = { $_.chainOutcomes.otherIncomplete }; Descending = $true }, pageId) $sourcePathByPageId
        Private-Outlier-Table 'Most truncated chains' @($outlierPages | Where-Object { $_.chainOutcomes.truncated -gt 0 } | Sort-Object @{ Expression = { $_.chainOutcomes.truncated }; Descending = $true }, pageId) $sourcePathByPageId
        Private-Outlier-Table 'Highest explicit evidence-gap counts' @($outlierPages | Where-Object { $_.counts.gaps -gt 0 } | Sort-Object @{ Expression = { $_.counts.gaps }; Descending = $true }, pageId) $sourcePathByPageId
        Private-Outlier-Table 'Retained calls without an observed boundary' @($outlierPages | Where-Object { $_.counts.normalizedCallSites -gt 0 -and $_.counts.boundaries -eq 0 } | Sort-Object @{ Expression = { $_.counts.normalizedCallSites }; Descending = $true }, pageId) $sourcePathByPageId
    )
    $privateOutlierHtml = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Private Web Forms outlier review</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1650px;margin:auto;padding:24px}.private{padding:12px;border-left:5px solid #c62828;background:#fff1f0}table{width:100%;border-collapse:collapse;background:white}th,td{padding:9px;border:1px solid #dbe2ee;text-align:left;vertical-align:top}th{background:#eaf1ff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px}a{color:#1558b0}.source-path{display:block;margin-top:5px;color:#526078;overflow-wrap:anywhere}details{background:white;border:1px solid #dbe2ee;border-radius:8px;padding:14px;margin:14px 0}</style></head><body><main><h1>Private Web Forms outlier review</h1><p class="private">PRIVATE: source-relative paths are displayed below each page alias. Page aliases link to their detailed private workbench reports and expose the same path as hover text.</p><p><a href="index.html">Return to application workbench</a> · <a href="application-outliers.shareable.html">Open alias-only shareable review</a>.</p><p>The inspection order is deterministic: call-evidence ceiling, truncated traversal, unavailable handlers, other incomplete chains, gaps, normalized call sites, projection reuse, then page alias. This is not a business-priority or migration-effort score.</p>$($privateOutlierSections -join '')<p>Static evidence does not prove runtime execution, business intent, migration effort, or absence under partial coverage.</p></main></body></html>
"@
    [IO.File]::WriteAllText((Join-Path $staging 'application-outliers.html'), $privateOutlierHtml, [Text.UTF8Encoding]::new($false))
    $appHandoff = [ordered]@{
        schemaVersion = 'webforms-application-handoff.v1'; ruleId = 'diagnostic.webforms.application-handoff.v1'; claimLevel = 'local-only'
        provenance = [ordered]@{ generator = 'scripts/New-FocusedWebFormsApplicationWorkbench.ps1'; generatorSha256 = $generatorSha256; generatorCanonicalization = 'raw-file-bytes'; inputKind = 'webforms-modernization-packet.v1'; inputSha256 = $packetSha256; inputCanonicalization = 'raw-file-bytes'; reviewOverlayKind = if ($ReviewPath) { 'wits-application-review.v1' } else { 'not-supplied' }; reviewOverlaySha256 = $reviewSha256; reviewOverlayCanonicalization = if ($ReviewPath) { 'raw-file-bytes' } else { 'not-applicable' } }
        packet = [ordered]@{ packetId = [string]$packet.packetId; scanId = [string]$sources[0].scanId; commitSha = [string]$sources[0].commitSha; sources = @($sources | ForEach-Object { [ordered]@{ sourceId = [string]$_.sourceId; scanId = [string]$_.scanId; commitSha = [string]$_.commitSha } }); snapshot = 'webforms-modernization.snapshot.json' }
        analysis = [ordered]@{
            status = $applicationStatus
            coverage = [string]$packet.coverage
            coverageReductionReasons = @(Values (Property-Value $packet.summary 'coverageReductionReasons'))
            packetTruncated = [bool]$packet.summary.truncated
            packetTruncationScope = 'application-packet'
            packetTruncationReasons = @(Values (Property-Value $packet.summary 'truncationReasons'))
            totalGapCount = @(Values $packet.gaps).Count
        }
        pageCount = $applicationPages.Count; pages = @($applicationPages)
        nextEvidenceSummary = @($applicationNextEvidenceSummary)
        controlRegistrationGaps = @($controlRegistrationGaps)
        outlierReview = [ordered]@{ ruleId = 'diagnostic.webforms.application-outlier-ranking.v1'; privateHtml = 'application-outliers.html'; html = 'application-outliers.shareable.html'; json = 'application-outliers.shareable.json' }
        applicationGaps = @($applicationGaps | ForEach-Object { [ordered]@{ gapId = [string]$_.gapId; classification = [string]$_.classification; scopeKind = [string]$_.scopeKind; scopeId = $_.scopeId; ruleId = [string]$_.ruleId; evidenceTier = [string]$_.evidenceTier; coverageLabel = [string]$_.coverageLabel; commitSha = [string]$_.commitSha; filePath = $_.filePath; startLine = $_.startLine; endLine = $_.endLine; extractorId = $_.extractorId; extractorVersion = $_.extractorVersion; safeMetadata = if ($null -eq $_.safeMetadata) { [ordered]@{} } else { $_.safeMetadata }; truncationReason = $_.truncationReason; supportingFactIds = @(Values $_.supportingFactIds); limitations = @(Values $_.limitations) } })
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
    $tableRows = foreach ($row in $pageRows) {
        $projectionReuse = [Math]::Max(0, [int]$row.CallProjections - [int]$row.UniqueCallFacts)
        $flags = [Collections.Generic.List[string]]::new()
        if ($row.CallEvidenceCeilingChains -gt 0) { $flags.Add('<span class="flag warning">ceiling {0}</span>' -f $row.CallEvidenceCeilingChains) }
        if ($row.CallEvidenceOmitted -gt 0) { $flags.Add('<span class="flag warning">omitted {0}</span>' -f $row.CallEvidenceOmitted) }
        if ($row.HandlerUnavailable -gt 0) { $flags.Add('<span class="flag warning">handler {0}</span>' -f $row.HandlerUnavailable) }
        if ($row.NoTerminal -gt 0) { $flags.Add('<span class="flag warning">no terminal {0}</span>' -f $row.NoTerminal) }
        if ($row.NoDownstream -gt 0) { $flags.Add('<span class="flag warning">no downstream {0}</span>' -f $row.NoDownstream) }
        if ($row.OtherIncomplete -gt 0) { $flags.Add('<span class="flag warning">incomplete {0}</span>' -f $row.OtherIncomplete) }
        if ($row.TerminalInventoryIncomplete -gt 0) { $flags.Add('<span class="flag warning">terminal inventory incomplete {0}</span>' -f $row.TerminalInventoryIncomplete) }
        if ($row.TerminalInventoryUnavailable -gt 0) { $flags.Add('<span class="flag warning">terminal inventory unavailable {0}</span>' -f $row.TerminalInventoryUnavailable) }
        if ($row.PathDetailTruncated -gt 0) { $flags.Add('<span class="flag warning">path detail truncated {0}</span>' -f $row.PathDetailTruncated) }
        if ($row.Gaps -gt 0) { $flags.Add('<span class="flag warning">gaps {0}</span>' -f $row.Gaps) }
        if ($row.Boundaries -gt 0) { $flags.Add('<span class="flag evidence">boundaries {0}</span>' -f $row.Boundaries) }
        if ($flags.Count -eq 0) { $flags.Add('<span class="flag quiet">no retained flags</span>') }
        $activity = '<span class="metric"><strong>{0}</strong> controls</span><span class="metric"><strong>{1}</strong> chains</span><span class="submetric">client {2} · server {3}</span>' -f $row.Controls, $row.Chains, $row.ClientBehaviors, $row.ServerBehaviors
        $calls = '<span class="metric"><strong>{0} / {1} / {2}</strong> P/F/S</span><span class="submetric">reuse {3}</span>' -f $row.CallProjections, $row.UniqueCallFacts, $row.UniqueCallSites, $projectionReuse
        $reviewState = '<span class="metric">{0}</span><span class="submetric">{1}</span>' -f (ConvertTo-HtmlText $row.Verdict), (ConvertTo-HtmlText $row.Disposition)
        $diagnostics = '<details class="row-details"><summary>Show</summary><dl><dt>Retained route</dt><dd><code>{1}</code></dd><dt>Kind</dt><dd>{2}</dd><dt>Calls</dt><dd>{7} projections; {8} unique facts; {9} normalized sites; {19} reuse</dd><dt>Call evidence ceiling</dt><dd>{10} chain(s); {20} explicitly omitted</dd><dt>Handler unavailable</dt><dd>{11} — event source retained; usable handler fact unavailable</dd><dt>Downstream / no terminal</dt><dd>{12}</dd><dt>No downstream</dt><dd>{13}</dd><dt>Terminal inventory incomplete</dt><dd>{21}</dd><dt>Terminal inventory unavailable</dt><dd>{23}</dd><dt>Path detail truncated</dt><dd>{22}</dd><dt>Other incomplete</dt><dd>{15}</dd><dt>Boundaries</dt><dd>{6}</dd><dt>Recorded gap facts</dt><dd>{16}</dd><dt>Review</dt><dd>{17}; {18}</dd><dt>Evidence handoff</dt><dd><a href="{0}.handoff.json">Open JSON</a></dd></dl></details>' -f $row.PageId, (ConvertTo-HtmlText $row.Path), (ConvertTo-HtmlText $row.SurfaceKind), $row.Chains, $row.ClientBehaviors, $row.ServerBehaviors, $row.Boundaries, $row.CallProjections, $row.UniqueCallFacts, $row.UniqueCallSites, $row.CallEvidenceCeilingChains, $row.HandlerUnavailable, $row.NoTerminal, $row.NoDownstream, $row.Truncated, $row.OtherIncomplete, $row.Gaps, (ConvertTo-HtmlText $row.Verdict), (ConvertTo-HtmlText $row.Disposition), $projectionReuse, $row.CallEvidenceOmitted, $row.TerminalInventoryIncomplete, $row.PathDetailTruncated, $row.TerminalInventoryUnavailable
        '<tr class="page-summary"><td><a class="page-link" target="_blank" rel="noopener" href="{0}.html">{0}</a></td><td>{1}</td><td>{2}</td><td><div class="flags">{3}</div></td><td>{4}</td><td>{5}</td></tr><tr class="page-context"><th scope="row">Route and controls</th><td colspan="5"><span class="route"><strong>Route:</strong> <code>{6}</code></span><span class="controls"><strong>Controls:</strong> {7}</span></td></tr>' -f $row.PageId, $activity, $calls, ($flags -join ''), $reviewState, $diagnostics, (ConvertTo-HtmlText $row.Path), (ConvertTo-HtmlText $row.ControlDisplay)
    }
    $index = @"
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Web Forms application workbench</title><style>:root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}main{max-width:1400px;margin:auto;padding:24px}.private{padding:12px;border-left:5px solid #c62828;background:#fff1f0}.table-wrap{overflow-x:auto;border:1px solid #dbe2ee;border-radius:8px;background:white}table{width:100%;border-collapse:collapse;background:white}th,td{padding:10px;border-bottom:1px solid #dbe2ee;text-align:left;vertical-align:top}th{position:sticky;top:0;z-index:1;background:#eaf1ff;white-space:nowrap}tbody tr.page-summary:hover,tbody tr.page-context:hover{background:#f7faff}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px;overflow-wrap:anywhere}a{color:#1558b0}.page-link{font-weight:700;white-space:nowrap}.metric,.submetric,.route,.controls{display:block}.metric{white-space:nowrap}.submetric{margin-top:3px;color:#526078;font-size:.88rem}.page-context th{position:static;background:#f7f9fc;color:#526078;font-size:.82rem}.page-context td{background:#fbfcfe;font-size:.88rem;overflow-wrap:anywhere}.page-context .controls{margin-top:5px}.flags{display:flex;flex-wrap:wrap;gap:5px;min-width:150px}.flag{display:inline-block;border-radius:999px;padding:2px 7px;font-size:.78rem;white-space:nowrap}.flag.warning{background:#fff0d8;color:#7a4100}.flag.evidence{background:#e8f4ff;color:#174e7a}.flag.quiet{background:#edf1f7;color:#526078}.row-details{margin:0;padding:0;border:0;background:transparent;min-width:70px}.row-details summary{cursor:pointer;color:#1558b0}.row-details dl{display:grid;grid-template-columns:max-content minmax(180px,1fr);gap:5px 10px;min-width:420px;margin:10px 0 2px}.row-details dt{font-weight:700}.row-details dd{margin:0}body>main>details{background:white;border:1px solid #dbe2ee;border-radius:8px;padding:14px;margin:14px 0}@media(max-width:800px){main{padding:12px}.table-wrap{border-radius:0}th,td{padding:8px}.row-details dl{grid-template-columns:1fr;min-width:260px}.row-details dd{margin-bottom:6px}}</style></head><body><main><h1>Private Web Forms application workbench</h1><p class="private">PRIVATE: $(ConvertTo-HtmlText $applicationPages.Count) selected surfaces from one retained packet. No source scan was run.</p><p>Packet <code>$(ConvertTo-HtmlText $packet.packetId)</code>. Analysis <code>$(ConvertTo-HtmlText $applicationStatus)</code>; coverage <code>$(ConvertTo-HtmlText $packet.coverage)</code>; truncated <code>$(ConvertTo-HtmlText $packet.summary.truncated)</code>. <a href="application-handoff.json">Application handoff JSON</a> · <a href="webforms-modernization.snapshot.json">Packet snapshot</a> · <a href="application-outliers.html">Private outlier review</a> · <a href="application-outliers.shareable.html">Alias-only outlier review</a> · <a href="application-outliers.shareable.json">Outlier JSON</a>.</p><p><strong>Call accounting:</strong> P = chain-associated projections; F = unique retained facts; S = normalized source sites. <strong>Handler unavailable</strong> means the event source was retained but no usable handler fact/span was available to continue that chain. It is not proof that the application has no handler. Expand Diagnostics for the complete retained breakdown.</p><div class="table-wrap"><table><thead><tr><th>Page</th><th>Activity</th><th>Calls P/F/S</th><th>Retained flags</th><th>Review</th><th>Diagnostics</th></tr></thead><tbody>$($tableRows -join '')</tbody></table></div><details><summary><strong>Application or unassociated gaps ($($applicationGaps.Count))</strong></summary><ul>$($applicationGapRows -join '')</ul></details><details><summary><strong>Unassociated identity/state ($($unassociatedIdentity.Count))</strong></summary><ul>$($unassociatedIdentityRows -join '')</ul></details><details><summary><strong>Project-scoped batch/data movement ($($applicationBatch.Count))</strong></summary><ul>$($applicationBatchRows -join '')</ul></details><details><summary><strong>Unassociated batch/data movement ($($unassociatedBatch.Count))</strong></summary><ul>$($unassociatedBatchRows -join '')</ul></details><details><summary><strong>Unassociated structural candidates ($($unassociatedCandidates.Count))</strong></summary><ul>$($unassociatedCandidateRows -join '')</ul></details><p>Static evidence does not prove runtime execution, business intent, or migration correctness. Human review remains a separate overlay.</p></main></body></html>
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
Write-Host "applicationPrivateOutlierReview=$(Join-Path $OutputDirectory 'application-outliers.html')"
Write-Host "applicationOutlierReview=$(Join-Path $OutputDirectory 'application-outliers.shareable.html')"
Write-Host 'chunksCorpus=preserved-read-only'
if ($IsWindows) { Start-Process -FilePath (Join-Path $OutputDirectory 'index.html') }
