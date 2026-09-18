[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^page-[0-9]{3,4}$')][string]$PageId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_PAGE_SHAREABLE_POWERSHELL_7_REQUIRED' }

function Values([object]$Value) { if ($null -eq $Value) { @() } else { @($Value) } }
function Property-Value([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}
function Text-Sha256([string]$Value) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function Read-Json([string]$Path, [long]$MaximumBytes, [string]$ErrorCode) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $ErrorCode }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $ErrorCode }
    return [IO.File]::ReadAllText($Path) | ConvertFrom-Json -Depth 40
}
function Call-Site-Key([object]$Call) {
    $site = [string](Property-Value $Call 'callSiteId')
    if ($site) { return $site }
    $evidence = Property-Value $Call 'evidence'
    return '{0}|{1}|{2}|{3}' -f ([string](Property-Value $evidence 'filePath')), ([int](Property-Value $evidence 'startLine')), ([int](Property-Value $evidence 'endLine')), ([string](Property-Value $Call 'calleeName'))
}
function Callee-Key([object]$Call) {
    return '{0}|{1}|{2}' -f ([string](Property-Value $Call 'assemblyName')), ([string](Property-Value $Call 'declaringType')), ([string](Property-Value $Call 'calleeName'))
}
function New-AliasMap([object[]]$Values, [string]$Prefix) {
    $map = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    $ordinal = 1
    foreach ($value in @($Values | ForEach-Object { [string]$_ } | Where-Object { $_ } | Sort-Object -Unique)) {
        $map[$value] = '{0}-{1:d3}' -f $Prefix, $ordinal
        $ordinal++
    }
    return $map
}
function New-OrderedAliasMap([object[]]$Values, [string]$Prefix) {
    $map = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    $ordinal = 1
    foreach ($value in @($Values | ForEach-Object { [string]$_ } | Where-Object { $_ })) {
        if ($map.ContainsKey($value)) { continue }
        $map[$value] = '{0}-{1:d3}' -f $Prefix, $ordinal
        $ordinal++
    }
    return $map
}
function Alias-OrUnavailable([Collections.Generic.Dictionary[string,string]]$Map, [string]$Key) {
    if ($Key -and $Map.ContainsKey($Key)) { return $Map[$Key] }
    return 'unavailable'
}
function Closed-Value([string]$Value, [string[]]$Allowed, [string]$Fallback = 'other') {
    if ($Value -in $Allowed) { return $Value }
    return $Fallback
}
function Shareable-ChainClassification([string]$Value) {
    if ($Value -in @('StrongStaticPath','ProbableStaticPath')) { return 'terminal-reached' }
    return Closed-Value $Value @('terminal-reached','NoBackendEvidence','handler-unavailable','UnknownAnalysisGap','NeedsReviewStaticPath','AnalysisGap','ReducedCoverage')
}
function Shareable-TerminalKind([string]$Value) {
    if ($Value -in @('sql-query','sql-persistence','database')) { return 'database' }
    if ($Value -in @('http-call','http-client')) { return 'http-client' }
    if ($Value -like 'wcf-*') { return 'wcf-operation' }
    if ($Value -like 'file-*') { return 'file' }
    return Closed-Value $Value @('database','service','http-client','wcf-operation','file','message-queue','message-topic','package-config') 'none-observed'
}
function Structural-Signals([object[]]$Calls) {
    $signals = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($call in $Calls) {
        $name = [string](Property-Value $call 'calleeName')
        $type = [string](Property-Value $call 'declaringType')
        $assembly = [string](Property-Value $call 'assemblyName')
        $identity = "$assembly|$type|$name"
        if ($identity -match '(?i)System\.ServiceModel|ClientBase|ChannelFactory') { $null = $signals.Add('wcf-framework-call') }
        if ($type -match '(?i)(^|[.+])[^.+<>]+Client(?:$|[<])') { $null = $signals.Add('generated-client-proxy-shape') }
        if ($identity -match '(?i)SqlConnection|SqlCommand|SqlDataReader|DbConnection|DbCommand|IDataReader|OleDb|Odbc') { $null = $signals.Add('database-api-call') }
        if ($name -in @('ExecuteReader','ExecuteNonQuery','ExecuteScalar')) { $null = $signals.Add('database-command-execution') }
        if ($name -eq 'Open' -and $identity -match '(?i)Connection') { $null = $signals.Add('connection-open') }
        if ($name -in @('Close','Dispose') -and $identity -match '(?i)Connection') { $null = $signals.Add('connection-close-or-dispose') }
        if ($identity -match '(?i)System\.Net\.Http|HttpClient|WebRequest') { $null = $signals.Add('outbound-http-call') }
        if ($identity -match '(?i)System\.IO\.|FileStream|StreamReader|StreamWriter') { $null = $signals.Add('file-api-call') }
        if ($type -match '(?i)StringBuilder' -and $name -match '^Append') { $null = $signals.Add('dynamic-text-construction') }
        if ($type -match '(?i)HttpResponse' -and $name -eq 'Write') { $null = $signals.Add('response-write') }
        if ($identity -match '(?i)Telerik') { $null = $signals.Add('telerik-call') }
    }
    if ($signals.Count -eq 0) { $null = $signals.Add('unclassified-static-call') }
    return @($signals)
}

$root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
$receiptPath = Join-Path $root 'run-receipt.json'
$receipt = Read-Json $receiptPath 16MB 'WEBFORMS_PAGE_SHAREABLE_RECEIPT_UNAVAILABLE'
if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or $receipt.run.state -ne 'completed' -or $receipt.stages.workbench.state -ne 'completed') {
    throw 'WEBFORMS_PAGE_SHAREABLE_RUN_INCOMPLETE'
}
function Get-ReceiptedArtifact([string]$RelativePath) {
    $artifact = @($receipt.stages.workbench.artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals($RelativePath, [StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1)
    if ($artifact.Count -ne 1) { throw 'WEBFORMS_PAGE_SHAREABLE_ARTIFACT_NOT_RECEIPTED' }
    $path = Join-Path $root $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'WEBFORMS_PAGE_SHAREABLE_ARTIFACT_UNAVAILABLE' }
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne [long]$artifact[0].bytes -or $hash -ne [string]$artifact[0].sha256) { throw 'WEBFORMS_PAGE_SHAREABLE_ARTIFACT_MISMATCH' }
    return $path
}

$applicationPath = Get-ReceiptedArtifact 'workbench/application-handoff.json'
$outlierPath = Get-ReceiptedArtifact 'workbench/application-outliers.shareable.json'
$application = Read-Json $applicationPath 128MB 'WEBFORMS_PAGE_SHAREABLE_APPLICATION_UNAVAILABLE'
$outliers = Read-Json $outlierPath 32MB 'WEBFORMS_PAGE_SHAREABLE_OUTLIERS_UNAVAILABLE'
if ($application.schemaVersion -ne 'webforms-application-handoff.v1' -or $outliers.schemaVersion -ne 'webforms-application-outliers.v1' -or $outliers.privacy -ne 'anonymous-counts-only') {
    throw 'WEBFORMS_PAGE_SHAREABLE_SCHEMA_UNSUPPORTED'
}
$applicationPage = @($application.pages | Where-Object { $_.pageId -eq $PageId } | Select-Object -First 1)
$outlierPage = @($outliers.pages | Where-Object { $_.pageId -eq $PageId } | Select-Object -First 1)
if ($applicationPage.Count -ne 1 -or $outlierPage.Count -ne 1 -or [string]$applicationPage[0].handoff -ne "$PageId.handoff.json") {
    throw 'WEBFORMS_PAGE_SHAREABLE_PAGE_UNAVAILABLE'
}
$pagePath = Get-ReceiptedArtifact "workbench/$PageId.handoff.json"
$page = Read-Json $pagePath 128MB 'WEBFORMS_PAGE_SHAREABLE_PAGE_HANDOFF_UNAVAILABLE'
if ($page.schemaVersion -ne 'webforms-application-page-handoff.v1' -or $page.claimLevel -ne 'local-only' -or $page.pageId -ne $PageId) {
    throw 'WEBFORMS_PAGE_SHAREABLE_PAGE_HANDOFF_INVALID'
}
if ($page.provenance.generatorSha256 -ne $application.provenance.generatorSha256 -or $page.provenance.inputSha256 -ne $application.provenance.inputSha256) {
    throw 'WEBFORMS_PAGE_SHAREABLE_PROVENANCE_MISMATCH'
}

# The workbench has already ordered chains by retained source evidence. Preserve
# that order here: opaque fact-derived chain IDs can all change after an extractor
# version bump and are therefore a poor cross-run presentation sort key.
$chains = @(Values $page.eventChains)
$calls = @($chains | ForEach-Object { Values $_.callEvidence })
$handlerKeys = @($chains | ForEach-Object {
    $key = [string](Property-Value $_ 'handlerFactId')
    if (!$key) { $key = [string](Property-Value $_ 'handlerId') }
    if (!$key) { $key = [string](Property-Value $_ 'handlerSymbol') }
    $key
})
$handlerAliases = New-AliasMap $handlerKeys 'handler'
$chainAliases = New-OrderedAliasMap @($chains | ForEach-Object { $_.chainId }) 'chain'
$siteAliases = New-AliasMap @($calls | ForEach-Object { Call-Site-Key $_ }) 'call-site'
$calleeAliases = New-AliasMap @($calls | ForEach-Object { Callee-Key $_ }) 'callee'
$fileAliases = New-AliasMap @($calls | ForEach-Object { [string]$_.evidence.filePath }) 'source-file'

$clientBindings = @{}
$endpointKeys = @($page.inventories.clientBehavior | ForEach-Object {
    $endpoint = [string](Property-Value $_.safeMetadata 'endpointDeclarationFile')
    if (!$endpoint) { $endpoint = [string](Property-Value $_.safeMetadata 'endpointName') }
    if ($endpoint) { $clientBindings[[string]$_.evidenceFactId] = $endpoint; $endpoint }
})
$endpointAliases = New-AliasMap $endpointKeys 'endpoint'

$siteRows = @($calls | Group-Object { Call-Site-Key $_ } | Sort-Object Name | ForEach-Object {
    $groupCalls = @($_.Group)
    $preferred = @($groupCalls | Sort-Object @{ Expression = { if (([string]$_.resolution) -eq 'compiler-resolved' -or ([string]$_.callKind) -like 'Semantic*') { 0 } else { 1 } } }, callEvidenceId)[0]
    $evidence = $preferred.evidence
    [ordered]@{
        callSiteAlias = Alias-OrUnavailable $siteAliases ([string]$_.Name)
        sourceFileAlias = Alias-OrUnavailable $fileAliases ([string]$evidence.filePath)
        calleeAlias = Alias-OrUnavailable $calleeAliases (Callee-Key $preferred)
        evidenceFactCount = $groupCalls.Count
        semanticEvidence = @($groupCalls | Where-Object { ([string]$_.resolution) -eq 'compiler-resolved' -or ([string]$_.callKind) -like 'Semantic*' }).Count -gt 0
        syntaxEvidence = @($groupCalls | Where-Object { ([string]$_.resolution) -eq 'syntax-only' -or ([string]$_.callKind) -like 'Syntax*' }).Count -gt 0
        callKinds = @($groupCalls | ForEach-Object { Closed-Value ([string]$_.callKind) @('SemanticMethodInvocation','SemanticObjectCreation','SyntaxInvocation','SyntaxObjectCreation') } | Sort-Object -Unique)
        resolutions = @($groupCalls | ForEach-Object { Closed-Value ([string]$_.resolution) @('compiler-resolved','syntax-only') 'unavailable' } | Sort-Object -Unique)
        technologyFamilies = @($groupCalls | ForEach-Object { Closed-Value ([string]$_.technologyFamily) @('application','framework','telerik','third-party','unresolved') 'other' } | Sort-Object -Unique)
        ruleIds = @($groupCalls | ForEach-Object { [string]$_.evidence.ruleId } | Where-Object { $_ -match '^[a-z0-9.-]+\.v[0-9]+$' } | Sort-Object -Unique)
        evidenceTiers = @($groupCalls | ForEach-Object { Closed-Value ([string]$_.evidence.evidenceTier) @('Tier1Semantic','Tier2Structural','Tier3SyntaxOrTextual','Tier4Unknown') 'Tier4Unknown' } | Sort-Object -Unique)
        coverageLabels = @($groupCalls | ForEach-Object { [string]$_.evidence.coverageLabel } | Where-Object { $_ -match '^[a-z0-9-]{1,96}$' } | Sort-Object -Unique)
        structuralSignals = @(Structural-Signals $groupCalls)
    }
})

$chainRows = @($chains | ForEach-Object {
    $chain = $_
    $handlerKey = [string](Property-Value $chain 'handlerFactId')
    if (!$handlerKey) { $handlerKey = [string](Property-Value $chain 'handlerId') }
    if (!$handlerKey) { $handlerKey = [string](Property-Value $chain 'handlerSymbol') }
    $origin = if (@(Values $chain.evidence | Where-Object { $_.ruleId -eq 'legacy.webforms.inline-client-http-request.v1' }).Count) { 'client-http' } else { 'server-event-or-lifecycle' }
    $endpointKey = [string]$clientBindings[[string]$chain.bindingFactId]
    [ordered]@{
        chainAlias = Alias-OrUnavailable $chainAliases ([string]$chain.chainId)
        originKind = $origin
        endpointAlias = Alias-OrUnavailable $endpointAliases $endpointKey
        handlerState = if ($handlerKey) { 'resolved-static-handler' } else { 'handler-unavailable' }
        handlerResolution = Closed-Value ([string](Property-Value $chain 'handlerResolution')) @('resolved-static-handler','missing-linked-method','ambiguous-linked-method','unproven-cross-file','unavailable-unclassified') 'unavailable-unclassified'
        handlerAlias = Alias-OrUnavailable $handlerAliases $handlerKey
        classification = Shareable-ChainClassification ([string]$chain.classification)
        terminalKind = Shareable-TerminalKind ([string]$chain.terminalKind)
        traversalStopState = Closed-Value ([string]$chain.traversalStopState) @('observed-downstream-without-supported-terminal','no-observed-downstream-edge','bounded-traversal-truncated') 'other-or-unavailable'
        retainedCallFactCount = @(Values $chain.callEvidence).Count
        reportedCallFactCount = [int]$chain.callEvidenceTotalCount
        callEvidenceTruncated = [bool]$chain.callEvidenceTruncated
        retainedCallSiteAliases = @($chain.callEvidence | ForEach-Object { Alias-OrUnavailable $siteAliases (Call-Site-Key $_) } | Sort-Object -Unique)
    }
})

$clientRows = @($page.inventories.clientBehavior | Sort-Object id | ForEach-Object {
    $endpointKey = [string](Property-Value $_.safeMetadata 'endpointDeclarationFile')
    if (!$endpointKey) { $endpointKey = [string](Property-Value $_.safeMetadata 'endpointName') }
    [ordered]@{
        behaviorKind = Closed-Value ([string]$_.kind) @('client-http-request','client-event-binding')
        endpointAlias = Alias-OrUnavailable $endpointAliases $endpointKey
        endpointKind = Closed-Value ([string](Property-Value $_.safeMetadata 'endpointKind')) @('ashx','asmx','api-route','relative-url','absolute-url') 'other-or-unavailable'
        httpMethod = Closed-Value ([string](Property-Value $_.safeMetadata 'httpMethod')) @('GET','POST','PUT','PATCH','DELETE') 'other-or-unavailable'
        targetResolution = Closed-Value ([string]$_.targetResolution) @('unique-repository-handler-file','unique-static-target','ambiguous','unresolved') 'other-or-unavailable'
    }
})

$projection = [ordered]@{
    pageId = $PageId
    analysis = [ordered]@{
        status = Closed-Value ([string]$page.analysis.status) @('complete','partial') 'partial'
        coverage = Closed-Value ([string]$page.analysis.coverage) @('bounded-static-webforms-modernization','reduced-static-webforms-modernization','reduced') 'unavailable'
        packetTruncated = [bool]$page.analysis.packetTruncated
    }
    counts = [ordered]@{
        controls = [int]$outlierPage[0].counts.controls
        eventChains = [int]$outlierPage[0].counts.eventChains
        clientBehaviors = [int]$outlierPage[0].counts.clientBehaviors
        serverBehaviors = [int]$outlierPage[0].counts.serverBehaviors
        boundaries = [int]$outlierPage[0].counts.boundaries
        callProjections = [int]$outlierPage[0].counts.callProjections
        reportedCallProjections = [int]$outlierPage[0].counts.reportedCallProjections
        omittedCallProjections = [int]$outlierPage[0].counts.omittedCallProjections
        uniqueCallFacts = [int]$outlierPage[0].counts.uniqueCallFacts
        normalizedCallSites = [int]$outlierPage[0].counts.normalizedCallSites
        callEvidenceCeilingChains = [int]$outlierPage[0].counts.callEvidenceCeilingChains
        projectionReuse = [int]$outlierPage[0].counts.projectionReuse
        gaps = [int]$outlierPage[0].counts.gaps
    }
    chainOutcomes = [ordered]@{
        unresolvedHandlers = [int]$outlierPage[0].chainOutcomes.unresolvedHandlers
        downstreamWithoutSupportedTerminal = [int]$outlierPage[0].chainOutcomes.downstreamWithoutSupportedTerminal
        noObservedDownstream = [int]$outlierPage[0].chainOutcomes.noObservedDownstream
        truncated = [int]$outlierPage[0].chainOutcomes.truncated
        otherIncomplete = [int]$outlierPage[0].chainOutcomes.otherIncomplete
    }
    clientBehaviors = $clientRows
    chains = $chainRows
    callSites = $siteRows
    limitations = @(
        'Aliases preserve equality only within this artifact and disclose no original identity.',
        'Chain aliases follow deterministic retained source-evidence order; they are not runtime execution order.',
        'Retained call sites are static evidence grouped by source location; their order is not runtime execution order.',
        'Structural signals are bounded name/type-shape classifiers and do not prove WCF, database, file, HTTP, or SQL execution.',
        'Absence of a structural signal is not evidence that the behavior is absent.'
    )
}
$projectionJson = ConvertTo-Json -InputObject $projection -Depth 20 -Compress
$generatorSha = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$artifact = [ordered]@{
    schemaVersion = 'webforms-page-paths-shareable.v1'
    ruleId = 'diagnostic.webforms.page-paths-shareable.v1'
    privacy = 'anonymous-structure-only'
    provenance = [ordered]@{
        generator = 'scripts/Export-FocusedWebFormsPageShareable.ps1'
        generatorSha256 = $generatorSha
        generatorCanonicalization = 'raw-file-bytes'
        inputKind = 'anonymous-page-path-projection'
        inputSha256 = Text-Sha256 $projectionJson
        inputCanonicalization = 'powershell-json-compact-depth-20-utf8-v1'
        sourceWorkbenchGeneratorSha256 = [string]$page.provenance.generatorSha256
    }
    projection = $projection
}
$jsonPath = Join-Path (Join-Path $root 'workbench') "$PageId.paths.shareable.json"
$zipPath = Join-Path (Join-Path $root 'workbench') "$PageId.paths.shareable.zip"
[IO.File]::WriteAllText($jsonPath, (($artifact | ConvertTo-Json -Depth 24) + "`n"), [Text.UTF8Encoding]::new($false))
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -LiteralPath $jsonPath -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output 'pageShareable=valid'
Write-Output "pageId=$PageId"
Write-Output "jsonPath=$jsonPath"
Write-Output "zipPath=$zipPath"
Write-Output "chains=$($chainRows.Count)"
Write-Output "callSites=$($siteRows.Count)"
Write-Output "generatorSha256=$generatorSha"
Write-Output "inputSha256=$($artifact.provenance.inputSha256)"
