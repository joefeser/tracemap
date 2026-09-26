[CmdletBinding()]
param(
    [string]$SourceSiteRoot,
    [string]$PublishedRoot,
    [string]$PagePath,
    [string]$HandlerName,
    [string[]]$AdditionalAssemblyName = @(),
    [switch]$OperatorAttestsRemainingOutOfScope,
    [switch]$FailOnUnclassifiedAssemblies,
    [string]$OutputRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$OperatorAttestsExactSourceCommit,
    [switch]$PrepareOnly
)

# Local-only, operator-declared evidence from an existing Web Site publish.
# This does not compile, attribute, or prove execution of the published bytes.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_EXISTING_PUBLISH_POWERSHELL_7_REQUIRED' }

function Read-Required([string]$Value, [string]$Prompt) {
    if (![string]::IsNullOrWhiteSpace($Value)) { return $Value.Trim() }
    $answer = Read-Host $Prompt
    if ([string]::IsNullOrWhiteSpace($answer)) { throw 'WEBFORMS_EXISTING_PUBLISH_VALUE_REQUIRED' }
    return $answer.Trim()
}

function Assert-Child([string]$Root, [string]$Relative, [string]$Kind) {
    $relativePath = $Relative.Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($relativePath) -or $relativePath.StartsWith('/') -or
        [IO.Path]::IsPathRooted($relativePath) -or
        @($relativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -ne 0) {
        throw 'WEBFORMS_EXISTING_PUBLISH_UNSAFE_PATH'
    }
    $candidate = [IO.Path]::GetFullPath((Join-Path $Root $relativePath))
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (!$candidate.StartsWith($Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, $comparison)) {
        throw 'WEBFORMS_EXISTING_PUBLISH_UNSAFE_PATH'
    }
    $current = $Root
    foreach ($segment in $relativePath.Split('/')) {
        $current = Join-Path $current $segment
        if (!(Test-Path -LiteralPath $current)) { throw "WEBFORMS_EXISTING_PUBLISH_${Kind}_UNAVAILABLE" }
        if ((Get-Item -LiteralPath $current).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
            throw 'WEBFORMS_EXISTING_PUBLISH_REPARSE_POINT'
        }
    }
    return $candidate
}

function Get-Sha256([string]$Path, [long]$MaximumBytes) {
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -lt 0 -or $file.Length -gt $MaximumBytes) {
        throw 'WEBFORMS_EXISTING_PUBLISH_INPUT_LIMIT'
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-Map([string]$Path) {
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try { return [Xml.Linq.XDocument]::Load($reader).Root }
    finally { $reader.Dispose() }
}

function Get-LinkedCodePaths([string]$MarkupPath, [string]$PageRelativePath) {
    $markup = [IO.File]::ReadAllText($MarkupPath)
    $directive = [regex]::Match($markup, '<%@\s*Page\b(?<attrs>.*?)%>',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [Text.RegularExpressions.RegexOptions]::Singleline)
    $pageDirectory = [IO.Path]::GetDirectoryName($PageRelativePath).Replace('\', '/')
    if ($directive.Success) {
        $attributes = @{}
        foreach ($match in [regex]::Matches($directive.Groups['attrs'].Value,
            '(?<name>[A-Za-z_:][\w:.-]*)\s*=\s*(?:"(?<dq>[^"]*)"|''(?<sq>[^'']*)'')',
            [Text.RegularExpressions.RegexOptions]::Singleline)) {
            $attributes[$match.Groups['name'].Value] = if ($match.Groups['dq'].Success) {
                $match.Groups['dq'].Value
            } else { $match.Groups['sq'].Value }
        }
        foreach ($name in @('CodeBehind', 'CodeFile')) {
            if ($attributes.ContainsKey($name)) {
                $declared = [string]$attributes[$name]
                $relative = $declared.Replace('\', '/').TrimStart('~', '/')
                if ($declared -match '://|:|\.\.|\$|%' -or $declared.StartsWith('/') -or
                    [string]::IsNullOrWhiteSpace($relative)) {
                    throw 'WEBFORMS_EXISTING_PUBLISH_LINKED_CODE_UNSAFE'
                }
                $linked = if ($pageDirectory) { "$pageDirectory/$relative" } else { $relative }
                return ,@($linked)
            }
        }
    }
    return ,@("$PageRelativePath.vb", "$PageRelativePath.cs")
}

function Resolve-PhysicalDirectoryPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $current = [IO.Path]::GetPathRoot($full)
    $remaining = $full.Substring($current.Length).Split([IO.Path]::DirectorySeparatorChar,
        [StringSplitOptions]::RemoveEmptyEntries)
    foreach ($segment in $remaining) {
        $current = [IO.Path]::Combine($current, $segment)
        if ([IO.Directory]::Exists($current)) {
            $item = [IO.DirectoryInfo]::new($current)
            if ($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
                $target = $item.ResolveLinkTarget($true)
                if ($null -eq $target) { throw 'WEBFORMS_EXISTING_PUBLISH_OUTPUT_REPARSE_UNRESOLVED' }
                $current = Resolve-PhysicalDirectoryPath $target.FullName
            }
        }
    }
    return [IO.Path]::GetFullPath($current)
}

$SourceSiteRoot = [IO.Path]::GetFullPath((Read-Required $SourceSiteRoot 'Source Web Site folder')).TrimEnd('\', '/')
$PublishedRoot = [IO.Path]::GetFullPath((Read-Required $PublishedRoot 'Existing published Web Site folder')).TrimEnd('\', '/')
$PagePath = (Read-Required $PagePath 'Page path relative to the Web Site (for example Pages/Lookup.aspx)').Replace('\', '/')
$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $SourceSiteRoot -PathType Container) -or
    !(Test-Path -LiteralPath $PublishedRoot -PathType Container) -or
    !$PagePath.EndsWith('.aspx', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WEBFORMS_EXISTING_PUBLISH_INPUT_UNAVAILABLE'
}
$pageFile = Assert-Child $SourceSiteRoot $PagePath 'SOURCE'
$sourceCommit = ([string](& git -C $SourceSiteRoot rev-parse HEAD)).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'WEBFORMS_EXISTING_PUBLISH_COMMIT_UNAVAILABLE'
}
$sourceRepository = ([string](& git -C $SourceSiteRoot remote get-url origin)).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceRepository)) {
    throw 'WEBFORMS_EXISTING_PUBLISH_REPOSITORY_UNAVAILABLE'
}
$dirty = @(& git -C $SourceSiteRoot status --porcelain --untracked-files=all -- .)
if ($LASTEXITCODE -ne 0 -or $dirty.Count -ne 0) { throw 'WEBFORMS_EXISTING_PUBLISH_SOURCE_DIRTY' }

# One exact page map is required. The source site, published output, and
# compiler used to produce it are operator declarations, not build proof.
$maps = @(Get-ChildItem -LiteralPath $PublishedRoot -Recurse -File -Filter '*.compiled')
if ($maps.Count -gt 4096) { throw 'WEBFORMS_EXISTING_PUBLISH_MAP_LIMIT' }
$pageMaps = @($maps | Where-Object {
    $root = Read-Map $_.FullName
    $root.Name.LocalName -eq 'preserve' -and
        $null -ne $root.Attribute('virtualPath') -and
        $root.Attribute('virtualPath').Value -ceq ('/' + $PagePath)
})
if ($pageMaps.Count -ne 1) {
    Write-Output "existingPublishPageMapCount=$($pageMaps.Count)"
    throw 'WEBFORMS_EXISTING_PUBLISH_PAGE_MAP_NOT_UNIQUE'
}
$map = Read-Map $pageMaps[0].FullName
$assemblyName = if ($null -eq $map.Attribute('assembly')) { '' } else { $map.Attribute('assembly').Value }
$generatedType = if ($null -eq $map.Attribute('type')) { '' } else { $map.Attribute('type').Value }
if ($assemblyName -cnotmatch '^[A-Za-z0-9_.-]{1,200}$' -or
    [string]::IsNullOrWhiteSpace($generatedType)) {
    throw 'WEBFORMS_EXISTING_PUBLISH_MAP_INVALID'
}
$mappedAssembly = Assert-Child $PublishedRoot "bin/$assemblyName.dll" 'ASSEMBLY'
$bin = Assert-Child $PublishedRoot 'bin' 'BIN'
$availableDlls = @(Get-ChildItem -LiteralPath $bin -File -Filter '*.dll' | Sort-Object Name)
if ($availableDlls.Count -lt 1 -or $availableDlls.Count -gt 4096) {
    Write-Output "existingPublishDllCount=$($availableDlls.Count)"
    throw 'WEBFORMS_EXISTING_PUBLISH_ASSEMBLY_LIMIT'
}
if (@($availableDlls | Where-Object { $_.FullName -eq $mappedAssembly }).Count -ne 1) {
    throw 'WEBFORMS_EXISTING_PUBLISH_MAPPED_ASSEMBLY_UNAVAILABLE'
}
$requestedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[void]$requestedNames.Add([IO.Path]::GetFileName($mappedAssembly))
foreach ($file in $availableDlls) {
    if ($file.BaseName -match '^App_Code(?:[._]|$)') { [void]$requestedNames.Add($file.Name) }
}
foreach ($name in $AdditionalAssemblyName) {
    if ($name -cnotmatch '^[A-Za-z0-9_.-]{1,204}\.dll$' -or
        @($availableDlls | Where-Object { $_.Name -ieq $name }).Count -ne 1) {
        throw 'WEBFORMS_EXISTING_PUBLISH_ADDITIONAL_ASSEMBLY_INVALID'
    }
    [void]$requestedNames.Add($name)
}
$dlls = @($availableDlls | Where-Object { $requestedNames.Contains($_.Name) } | Sort-Object Name)
if ($dlls.Count -lt 1 -or $dlls.Count -gt 63) { throw 'WEBFORMS_EXISTING_PUBLISH_ASSEMBLY_LIMIT' }
$excludedDlls = @($availableDlls | Where-Object { !$requestedNames.Contains($_.Name) })
if ($excludedDlls.Count -gt 0 -and !$OperatorAttestsRemainingOutOfScope) {
    Write-Output "selectedDlls=$($dlls.Count)"
    Write-Output "unclassifiedDlls=$($excludedDlls.Count)"
    Write-Output ('unclassifiedDllNames=' + (($excludedDlls | ForEach-Object Name) -join ','))
    $answer = if ($FailOnUnclassifiedAssemblies) { '' } else {
        Read-Host 'Are all unselected DLLs outside this focused proof? Type OUTOFSCOPE to record exclusions'
    }
    if ($answer -cne 'OUTOFSCOPE') {
        Write-Output 'existingPublishScan=gap;reason=unclassified-assemblies'
        return
    }
}
$assemblyInventory = @($availableDlls | ForEach-Object {
    [ordered]@{
        path = 'bin/' + $_.Name
        sha256 = Get-Sha256 $_.FullName 67108864
        disposition = if ($requestedNames.Contains($_.Name)) { 'selected' } else { 'operator-declared-out-of-scope' }
    }
})
$inventoryLines = @($assemblyInventory | ForEach-Object { "$($_.path):$($_.sha256):$($_.disposition)" })
$assemblyInventorySha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes(($inventoryLines -join "`n") + "`n"))).ToLowerInvariant()

# The bounded source set supports candidate joins; it is explicitly not a
# complete inventory of inputs to the historical publish operation.
$sourceFiles = [Collections.Generic.List[string]]::new()
$sourceFiles.Add($pageFile)
foreach ($linkedCode in @(Get-LinkedCodePaths $pageFile $PagePath)) {
    $linkedAbsolute = Join-Path $SourceSiteRoot $linkedCode
    if (Test-Path -LiteralPath $linkedAbsolute -PathType Leaf) {
        $sourceFiles.Add((Assert-Child $SourceSiteRoot $linkedCode 'SOURCE'))
    } elseif ($linkedCode -notin @("$PagePath.vb", "$PagePath.cs")) {
        throw 'WEBFORMS_EXISTING_PUBLISH_LINKED_CODE_UNAVAILABLE'
    }
}
$configPaths = [Collections.Generic.List[string]]::new()
$configPaths.Add('Web.config')
$segments = $PagePath.Split('/')
for ($i = 1; $i -lt $segments.Length; $i++) {
    $configPaths.Add((($segments[0..($i - 1)] -join '/') + '/Web.config'))
}
foreach ($configPath in $configPaths) {
    if (Test-Path -LiteralPath (Join-Path $SourceSiteRoot $configPath) -PathType Leaf) {
        $sourceFiles.Add((Assert-Child $SourceSiteRoot $configPath 'SOURCE'))
    }
}
$appCode = Join-Path $SourceSiteRoot 'App_Code'
if (Test-Path -LiteralPath $appCode -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $appCode -Recurse -File |
        Where-Object { $_.Extension -in @('.vb', '.cs') } | Sort-Object FullName)) {
        $relative = [IO.Path]::GetRelativePath($SourceSiteRoot, $file.FullName).Replace('\', '/')
        $sourceFiles.Add((Assert-Child $SourceSiteRoot $relative 'SOURCE'))
        if ($sourceFiles.Count -gt 256) { throw 'WEBFORMS_EXISTING_PUBLISH_SOURCE_LIMIT' }
    }
}
$relativeSourcePaths = @($sourceFiles | ForEach-Object {
    [IO.Path]::GetRelativePath($SourceSiteRoot, $_).Replace('\', '/')
})
$relativeSourcePaths = [string[]]@($relativeSourcePaths | Select-Object -Unique)
[Array]::Sort($relativeSourcePaths, [StringComparer]::Ordinal)
$sourceRows = @($relativeSourcePaths | ForEach-Object {
    & git -C $SourceSiteRoot ls-files --error-unmatch -- $_ *> $null
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_EXISTING_PUBLISH_SOURCE_NOT_COMMITTED' }
    [ordered]@{ path = $_; sha256 = Get-Sha256 (Assert-Child $SourceSiteRoot $_ 'SOURCE') 67108864 }
})
if ($sourceRows.Count -gt 256) { throw 'WEBFORMS_EXISTING_PUBLISH_SOURCE_LIMIT' }
$digestLines = @($sourceRows | ForEach-Object { "$($_.path):$($_.sha256)" })
$digestBytes = [Text.Encoding]::UTF8.GetBytes(($digestLines -join "`n") + "`n")
$boundedInputSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($digestBytes)).ToLowerInvariant()

$output = if ($OutputRoot) { [IO.Path]::GetFullPath($OutputRoot) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('tracemap-existing-publish-' + [guid]::NewGuid().ToString('N'))
}
if (Test-Path -LiteralPath $output) { throw 'WEBFORMS_EXISTING_PUBLISH_OUTPUT_NOT_FRESH' }
$physicalOutput = Resolve-PhysicalDirectoryPath $output
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
foreach ($root in @($SourceSiteRoot, $PublishedRoot, $TraceMapRoot)) {
    $physicalRoot = Resolve-PhysicalDirectoryPath $root
    if ($physicalOutput.Equals($physicalRoot, $comparison) -or
        $physicalOutput.StartsWith($physicalRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) {
        throw 'WEBFORMS_EXISTING_PUBLISH_OUTPUT_INSIDE_INPUT'
    }
}

[void][IO.Directory]::CreateDirectory((Join-Path $output 'bin'))
$publishedRows = [Collections.Generic.List[object]]::new()
foreach ($file in $dlls) {
    $relative = 'bin/' + $file.Name
    $source = Assert-Child $PublishedRoot $relative 'ASSEMBLY'
    $destination = Join-Path $output $relative
    [IO.File]::Copy($source, $destination)
    $sourceHash = Get-Sha256 $source 67108864
    if ((Get-Sha256 $destination 67108864) -cne $sourceHash) {
        throw 'WEBFORMS_EXISTING_PUBLISH_COPY_MISMATCH'
    }
    $publishedRows.Add([ordered]@{ path = $relative; sha256 = $sourceHash; kind = 'assembly' })
}
$mapRelative = [IO.Path]::GetRelativePath($PublishedRoot, $pageMaps[0].FullName).Replace('\', '/')
$mapSource = Assert-Child $PublishedRoot $mapRelative 'MAP'
$mapDestination = Join-Path $output $mapRelative
[void][IO.Directory]::CreateDirectory((Split-Path -Parent $mapDestination))
[IO.File]::Copy($mapSource, $mapDestination)
$mapHash = Get-Sha256 $mapSource 1048576
if ((Get-Sha256 $mapDestination 1048576) -cne $mapHash) {
    throw 'WEBFORMS_EXISTING_PUBLISH_COPY_MISMATCH'
}
$publishedRows.Add([ordered]@{ path = $mapRelative; sha256 = $mapHash; kind = 'compiled-map' })

$receiptGeneratorSha256 = Get-Sha256 $PSCommandPath 1048576
$sourceRepositorySha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($sourceRepository))).ToLowerInvariant()
$receiptInputSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes("source:$sourceRepositorySha256`ncommit:$sourceCommit`nsource:$boundedInputSha256`nassemblies:$assemblyInventorySha256`nmap:$mapHash`n"))).ToLowerInvariant()
# Existing output cannot identify its true compiler. This is an explicit
# unknown marker, not an attribution to the public test compiler.
$compilerSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes('operator-declared-existing-publish-compiler-unavailable.v1'))).ToLowerInvariant()
$receipt = [ordered]@{
    schemaVersion = 'webforms-publish-binding.v1'
    visibility = 'local-only'
    receiptGeneratorSha256 = $receiptGeneratorSha256
    compilerSha256 = $compilerSha256
    compilerProvenance = 'unavailable-existing-output'
    sourceCommitSha = $sourceCommit
    boundedInputSha256 = $boundedInputSha256
    receiptInputSha256 = $receiptInputSha256
    assemblyInventorySha256 = $assemblyInventorySha256
    assemblyInventory = $assemblyInventory
    sourceFiles = $sourceRows
    publishedFiles = @($publishedRows)
    pages = @([ordered]@{
        virtualPath = '/' + $PagePath
        assembly = $assemblyName
        generatedType = $generatedType
        mapPath = $mapRelative
    })
}
$receiptPath = Join-Path $output 'publish-receipt.local.json'
[IO.File]::WriteAllText($receiptPath, (($receipt | ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output 'existingPublishPreparation=valid'
Write-Output "sourceRepositorySha256=$sourceRepositorySha256"
Write-Output "sourceCommitSha=$sourceCommit"
Write-Output "sourceFiles=$($sourceRows.Count)"
Write-Output "availableDlls=$($availableDlls.Count)"
Write-Output "selectedDlls=$($dlls.Count)"
Write-Output "excludedDlls=$($excludedDlls.Count)"
Write-Output 'scope=selected-published-assemblies-only;no-complete-publish-claim'
Write-Output 'matchedPageMaps=1'
Write-Output "boundedInputSha256=$boundedInputSha256"
Write-Output 'compilerProvenance=unavailable-existing-output'
Write-Output 'claim=operator-declared-publish-bytes;review-only-candidates;no-build-or-runtime-proof'
Write-Output "localOutputRoot=$output"
if ($PrepareOnly) { return }

function Invoke-CompiledScan([string]$ScanPath, [string]$LogPath, [string]$BindingPath, [bool]$IncludeIl) {
    $arguments = [Collections.Generic.List[string]]::new()
    foreach ($argument in @('run', '--project', (Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli'),
            '--', 'scan', '--repo', $SourceSiteRoot, '--out', $ScanPath,
            '--webforms-publish-receipt', $receiptPath, '--compiled-max-artifacts', '64')) {
        $arguments.Add($argument)
    }
    if ($BindingPath) { $arguments.Add('--compiled-binding-receipt'); $arguments.Add($BindingPath) }
    if ($IncludeIl) { $arguments.Add('--il-body-evidence') }
    foreach ($file in $dlls) {
        $arguments.Add('--compiled-input')
        $arguments.Add((Join-Path $output ('bin/' + $file.Name)))
    }
    foreach ($source in $sourceRows) { $arguments.Add('--include'); $arguments.Add([string]$source.path) }
    & dotnet @arguments *> $LogPath
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_EXISTING_PUBLISH_SCAN_FAILED' }
    return [IO.File]::ReadAllText((Join-Path $ScanPath 'scan-manifest.json')) | ConvertFrom-Json -Depth 30
}

# First pass discovers the scanner's exact safe locators and metadata identity.
# It makes no bound-source or IL path claim.
$probe = Invoke-CompiledScan (Join-Path $output 'probe') (Join-Path $output 'probe.local.log') '' $false
$probeOutcomes = @($probe.compiledInputProvenance.outcomes)
$ready = $probeOutcomes.Count -eq $dlls.Count -and
    [int]$probe.compiledInputProvenance.omittedInputCount -eq 0 -and
    @($probeOutcomes | Where-Object {
        $_.outcome -ne 'admitted' -or
        [string]::IsNullOrWhiteSpace([string]$_.safeLocator) -or
        [string]::IsNullOrWhiteSpace([string]$_.rawFileSha256) -or
        [string]::IsNullOrWhiteSpace([string]$_.assemblyIdentity)
    }).Count -eq 0
Write-Output "compiledProbeInputs=$($probeOutcomes.Count)"
Write-Output "compiledProbeReady=$ready"
if (!$ready) {
    Write-Output 'existingPublishScan=gap;reason=compiled-admission-probe'
    return
}
if ($probe.webFormsPublishProvenance.status -ne 'bound') {
    Write-Output 'existingPublishScan=gap;reason=publish-receipt'
    return
}

if (!$OperatorAttestsExactSourceCommit) {
    $answer = Read-Host 'Do you attest the selected published DLLs were built from this exact clean source commit? Type YES to continue'
    if ($answer -cne 'YES') {
        Write-Output 'existingPublishScan=stopped;reason=exact-source-commit-not-attested'
        return
    }
}
$bindings = @($probeOutcomes | ForEach-Object {
    [ordered]@{
        schemaVersion = 'compiled-input-binding.v1'
        safeLocator = [string]$_.safeLocator
        artifactSha256 = [string]$_.rawFileSha256
        assemblyIdentity = [string]$_.assemblyIdentity
        binarySourceRepository = $sourceRepository
        binarySourceCommitSha = $sourceCommit
        binaryBuildIdentity = 'operator-attested-existing-publish:' + $boundedInputSha256
    }
})
$bindingPath = Join-Path $output 'compiled-binding.local.json'
$bindingInputLines = @($bindings | Sort-Object safeLocator | ForEach-Object {
    "$($_.safeLocator):$($_.artifactSha256):$($_.assemblyIdentity):$($_.binarySourceCommitSha)"
})
$bindingInputLines += "source:$boundedInputSha256"
$bindingInputLines += "source-repository:$sourceRepositorySha256"
$bindingInputLines += "assembly-inventory:$assemblyInventorySha256"
$bindingInputLines += "page-map:$mapHash"
$bindingInputSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes(($bindingInputLines -join "`n") + "`n"))).ToLowerInvariant()
[IO.File]::WriteAllText($bindingPath,
    (([ordered]@{
        schemaVersion = 'compiled-input-binding-set.v1'
        generatorSha256 = $receiptGeneratorSha256
        boundedInputSha256 = $bindingInputSha256
        bindings = $bindings
    } |
        ConvertTo-Json -Depth 10) + "`n"), [Text.UTF8Encoding]::new($false))

$scanPath = Join-Path $output 'scan'
$manifest = Invoke-CompiledScan $scanPath (Join-Path $output 'scan.local.log') $bindingPath $true
$status = [string]$manifest.webFormsPublishProvenance.status
$boundCount = @($manifest.compiledInputProvenance.outcomes | Where-Object { $_.provenanceState -eq 'bound' }).Count
$ilGaps = @($manifest.ilBodyProvenance.outcomes | Where-Object { @($_.gapKinds).Count -gt 0 }).Count
Write-Output "existingPublishScan=$status"
Write-Output "compiledBoundInputs=$boundCount"
Write-Output "compiledCoverage=$($manifest.compiledInputProvenance.coverageState)"
Write-Output "ilCoverage=$($manifest.ilBodyProvenance.coverageState)"
Write-Output "ilInputsWithGaps=$ilGaps"
Write-Output "publishGapKinds=$(@($manifest.webFormsPublishProvenance.gapKinds).Count)"
if ($status -ne 'bound' -or $boundCount -ne $dlls.Count -or $ilGaps -ne 0) {
    Write-Output 'existingPublishPaths=withheld;reason=incomplete-compiled-evidence'
    return
}

$combined = Join-Path $output 'combined.sqlite'
$combineArguments = @('run', '--project', (Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli'),
    '--', 'combine', '--index', (Join-Path $scanPath 'index.sqlite'), '--out', $combined,
    '--label', 'existing-publish')
& dotnet @combineArguments *> (Join-Path $output 'combine.local.log')
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_EXISTING_PUBLISH_COMBINE_FAILED' }
if ($HandlerName) {
    $paths = Join-Path $output 'handler-paths.json'
    $pathArguments = @('run', '--project', (Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli'),
        '--', 'paths', '--index', $combined, '--out', $paths, '--format', 'json',
        '--from-symbol', $HandlerName, '--to-surface', 'sql-query', '--max-depth', '20',
        '--max-paths', '256')
    & dotnet @pathArguments *> (Join-Path $output 'paths.local.log')
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_EXISTING_PUBLISH_PATHS_FAILED' }
    $report = [IO.File]::ReadAllText($paths) | ConvertFrom-Json -Depth 50
    Write-Output "sqlQueryPaths=$(@($report.paths).Count)"
    Write-Output "pathGaps=$(@($report.gaps).Count)"
}
