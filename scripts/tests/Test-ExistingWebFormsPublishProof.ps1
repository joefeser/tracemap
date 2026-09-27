[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'EXISTING_PUBLISH_TEST_POWERSHELL_7_REQUIRED' }

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
$source = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-pdb-projectless'
$project = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-pdb-build/CompiledProjectless.VB.vbproj'
$script = Join-Path $TraceMapRoot 'scripts/Invoke-ExistingWebFormsPublishProof.ps1'
& dotnet build $project --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'EXISTING_PUBLISH_TEST_BUILD_FAILED' }
$assembly = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-pdb-build/bin/Debug/net10.0/CompiledProjectless.VB.dll'
if (!(Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'EXISTING_PUBLISH_TEST_ASSEMBLY_UNAVAILABLE' }
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-existing-publish-test-' + [guid]::NewGuid().ToString('N'))
try {
    $publish = Join-Path $temp 'publish'
    [void][IO.Directory]::CreateDirectory((Join-Path $publish 'bin'))
    [void][IO.Directory]::CreateDirectory((Join-Path $publish 'Pages'))
    [IO.File]::Copy($assembly, (Join-Path $publish 'bin/CompiledProjectless.VB.dll'))
    $map = Join-Path $publish 'Pages/Lookup.aspx.compiled'
    [IO.File]::WriteAllText($map,
        '<preserve virtualPath="/Pages/Lookup.aspx" assembly="CompiledProjectless.VB" type="PublicProof.LookupPage" />',
        [Text.UTF8Encoding]::new($false))

    $output = Join-Path $temp 'proof'
    $lines = @(& $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath 'Pages/Lookup.aspx' `
        -HandlerName 'Lookup_Init' -OutputRoot $output -TraceMapRoot $TraceMapRoot `
        -OperatorAttestsExactSourceCommit)
    if ($lines -notcontains 'existingPublishPreparation=valid' -or $lines -notcontains 'existingPublishScan=bound') {
        $probe = [IO.File]::ReadAllText((Join-Path $output 'probe/scan-manifest.json')) | ConvertFrom-Json -Depth 30
        $outcome = @($probe.compiledInputProvenance.outcomes | ForEach-Object {
            "$($_.outcome):$($_.provenanceState):$(@($_.gapKinds) -join ',')"
        }) -join ';'
        throw "EXISTING_PUBLISH_TEST_POSITIVE_FAILED:$($lines -join ',');outcomes=$outcome"
    }
    $receipt = [IO.File]::ReadAllText((Join-Path $output 'publish-receipt.local.json')) | ConvertFrom-Json -Depth 20
    if ($receipt.schemaVersion -ne 'webforms-publish-binding.v1' -or
        $receipt.compilerProvenance -ne 'unavailable-existing-output' -or
        @($receipt.pages).Count -ne 1 -or @($receipt.publishedFiles).Count -ne 2 -or
        @($receipt.sourceFiles).Count -ne 2 -or
        @($receipt.assemblyInventory).Count -ne 1 -or
        $receipt.assemblyInventory[0].disposition -ne 'selected') {
        throw 'EXISTING_PUBLISH_TEST_RECEIPT_INVALID'
    }
    $binding = [IO.File]::ReadAllText((Join-Path $output 'compiled-binding.local.json')) | ConvertFrom-Json -Depth 20
    if ($binding.generatorSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $binding.boundedInputSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $binding.generatorSha256 -cne (Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw 'EXISTING_PUBLISH_TEST_BINDING_PROVENANCE_INVALID'
    }
    $manifest = [IO.File]::ReadAllText((Join-Path $output 'scan/scan-manifest.json')) | ConvertFrom-Json -Depth 20
    $probeManifest = [IO.File]::ReadAllText((Join-Path $output 'probe/scan-manifest.json')) | ConvertFrom-Json -Depth 20
    if ($manifest.webFormsPublishProvenance.status -ne 'bound' -or
        $probeManifest.compiledInputProvenance.effectiveLimits.maxTextLength -ne 8192 -or
        $manifest.compiledInputProvenance.effectiveLimits.maxTextLength -ne 8192 -or
        $manifest.ilBodyProvenance.effectiveLimits.maxTextLength -ne 16384 -or
        $manifest.webFormsPublishProvenance.pageCount -ne 1) {
        throw 'EXISTING_PUBLISH_TEST_MANIFEST_INVALID'
    }
    if (!(Test-Path -LiteralPath (Join-Path $output 'handler-paths.json') -PathType Leaf)) {
        throw 'EXISTING_PUBLISH_TEST_PATH_REPORT_UNAVAILABLE'
    }
    $recheckLines = @(& (Join-Path $TraceMapRoot 'scripts/wp.ps1') `
        -OutputRoot $output -RecheckPathReasons)
    if (@($recheckLines | Where-Object { $_ -cmatch '^pathRecheckPaths=\d+$' }).Count -ne 1 -or
        @($recheckLines | Where-Object { $_ -cmatch '^pathRecheckPublishMemberGaps=\d+$' }).Count -ne 1 -or
        @($recheckLines | Where-Object { $_ -cmatch '^pathRecheckArtifactIlCalls=\d+$' }).Count -ne 1 -or
        $recheckLines -cnotcontains 'pathRecheckReason.other=0') {
        throw 'EXISTING_PUBLISH_TEST_PATH_RECHECK_INVALID'
    }
    $apiLines = @(& (Join-Path $TraceMapRoot 'scripts/wp.ps1') `
        -OutputRoot $output -RecheckCompiledApi)
    if (@($apiLines | Where-Object { $_ -cmatch '^compiledApiPaths=\d+$' }).Count -ne 1 -or
        @($apiLines | Where-Object { $_ -cmatch '^compiledApiSelectorCandidates=\d+$' }).Count -ne 1 -or
        @($apiLines | Where-Object { $_ -cmatch '^compiledApiTruncated=(True|False)$' }).Count -ne 1) {
        throw 'EXISTING_PUBLISH_TEST_COMPILED_API_RECHECK_INVALID'
    }
    function global:Read-Host { param([string]$Prompt) 'yes' }
    try {
        $lowercaseLines = @(& $script -SourceSiteRoot $source -PublishedRoot $publish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Lookup_Init' `
            -OutputRoot (Join-Path $temp 'lowercase-attestation-proof') -TraceMapRoot $TraceMapRoot)
    } finally {
        Remove-Item Function:global:Read-Host -ErrorAction SilentlyContinue
    }
    if ($lowercaseLines -notcontains 'existingPublishScan=bound') {
        throw 'EXISTING_PUBLISH_TEST_LOWERCASE_ATTESTATION_REJECTED'
    }
    [IO.File]::WriteAllText($map,
        '<preserve virtualPath="/VirtualSite/Pages/Lookup.aspx" assembly="CompiledProjectless.VB" type="PublicProof.LookupPage" />',
        [Text.UTF8Encoding]::new($false))
    $prefixedOutput = Join-Path $temp 'prefixed-proof'
    $prefixedLines = @(& $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath 'Pages/Lookup.aspx' `
        -OutputRoot $prefixedOutput -TraceMapRoot $TraceMapRoot -OperatorAttestsExactSourceCommit)
    $prefixedReceipt = [IO.File]::ReadAllText((Join-Path $prefixedOutput 'publish-receipt.local.json')) | ConvertFrom-Json -Depth 20
    $prefixedManifest = [IO.File]::ReadAllText((Join-Path $prefixedOutput 'scan/scan-manifest.json')) | ConvertFrom-Json -Depth 20
    if ($prefixedLines -notcontains 'pageMapMatch=unique-prefixed' -or
        $prefixedLines -notcontains 'existingPublishScan=bound' -or
        $prefixedReceipt.pages[0].virtualPath -ne '/VirtualSite/Pages/Lookup.aspx' -or
        $prefixedReceipt.pages[0].sourcePath -ne 'Pages/Lookup.aspx' -or
        $prefixedManifest.webFormsPublishProvenance.status -ne 'bound') {
        throw 'EXISTING_PUBLISH_TEST_PREFIXED_PAGE_MAP_NOT_BOUND'
    }
    [IO.File]::WriteAllText((Join-Path $publish 'Pages/Other.compiled'),
        '<preserve virtualPath="/AnotherSite/Pages/Lookup.aspx" assembly="CompiledProjectless.VB" type="PublicProof.LookupPage" />',
        [Text.UTF8Encoding]::new($false))
    $captured = $null
    try {
        & $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath 'Pages/Lookup.aspx' `
            -OutputRoot (Join-Path $temp 'ambiguous') -PrepareOnly *> $null
    } catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_PAGE_MAP_NOT_UNIQUE') {
        throw 'EXISTING_PUBLISH_TEST_PREFIXED_AMBIGUITY_NOT_REJECTED'
    }
    Remove-Item -LiteralPath (Join-Path $publish 'Pages/Other.compiled')
    if (!$IsWindows) {
        $aliasMap = Join-Path $publish 'Pages/Alias.compiled'
        [void][IO.File]::CreateSymbolicLink($aliasMap, $map)
        $captured = $null
        try {
            & $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath 'Pages/Lookup.aspx' `
                -OutputRoot (Join-Path $temp 'alias-map') -PrepareOnly *> $null
        } catch { $captured = $_.Exception.Message }
        if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_REPARSE_POINT') {
            throw 'EXISTING_PUBLISH_TEST_MAP_ALIAS_NOT_REJECTED'
        }
        Remove-Item -LiteralPath $aliasMap
    }
    $nestedSource = Join-Path $temp 'nested-source'
    [void][IO.Directory]::CreateDirectory((Join-Path $nestedSource 'Pages/Deep'))
    [IO.File]::WriteAllText((Join-Path $nestedSource 'Pages/Deep/Lookup.aspx'),
        '<%@ Page Language="C#" CodeBehind="CustomHandler.cs" Inherits="PublicProof.LookupPage" %>',
        [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $nestedSource 'Pages/Deep/CustomHandler.cs'),
        'namespace PublicProof { public class LookupPage {} }', [Text.UTF8Encoding]::new($false))
    foreach ($config in @('Web.config', 'Pages/Web.config', 'Pages/Deep/Web.config')) {
        [IO.File]::WriteAllText((Join-Path $nestedSource $config), '<configuration />',
            [Text.UTF8Encoding]::new($false))
    }
    & git -C $nestedSource init -q
    & git -C $nestedSource remote add origin 'https://example.invalid/public-synthetic.git'
    & git -C $nestedSource add .
    & git -C $nestedSource -c user.name=PublicTest -c user.email=public@example.invalid commit -qm synthetic
    if ($LASTEXITCODE -ne 0) { throw 'EXISTING_PUBLISH_TEST_SYNTHETIC_GIT_FAILED' }
    $nestedPublish = Join-Path $temp 'nested-publish'
    [void][IO.Directory]::CreateDirectory((Join-Path $nestedPublish 'bin'))
    [void][IO.Directory]::CreateDirectory((Join-Path $nestedPublish 'Pages/Deep'))
    [IO.File]::Copy($assembly, (Join-Path $nestedPublish 'bin/CompiledProjectless.VB.dll'))
    [IO.File]::Copy($assembly, (Join-Path $nestedPublish 'bin/Unselected.dll'))
    [IO.File]::WriteAllText((Join-Path $nestedPublish 'Pages/Deep/Lookup.aspx.compiled'),
        '<preserve virtualPath="/Pages/Deep/Lookup.aspx" assembly="CompiledProjectless.VB" type="PublicProof.LookupPage" />',
        [Text.UTF8Encoding]::new($false))
    $nestedOutput = Join-Path $temp 'nested-proof'
    $unclassified = @(& $script -SourceSiteRoot $nestedSource -PublishedRoot $nestedPublish `
        -PagePath 'Pages/Deep/Lookup.aspx' -OutputRoot $nestedOutput -TraceMapRoot $TraceMapRoot `
        -FailOnUnclassifiedAssemblies -PrepareOnly)
    if ($unclassified -notcontains 'existingPublishScan=gap;reason=unclassified-assemblies' -or
        (Test-Path -LiteralPath $nestedOutput)) {
        throw 'EXISTING_PUBLISH_TEST_UNCLASSIFIED_DLL_NOT_WITHHELD'
    }
    $nestedLines = @(& $script -SourceSiteRoot $nestedSource -PublishedRoot $nestedPublish `
        -PagePath 'Pages/Deep/Lookup.aspx' -OutputRoot $nestedOutput -TraceMapRoot $TraceMapRoot `
        -OperatorAttestsRemainingOutOfScope -PrepareOnly)
    $nestedReceipt = [IO.File]::ReadAllText((Join-Path $nestedOutput 'publish-receipt.local.json')) | ConvertFrom-Json -Depth 20
    if ($nestedLines -notcontains 'excludedDlls=1' -or
        @($nestedReceipt.sourceFiles).Count -ne 5 -or
        @($nestedReceipt.sourceFiles | Where-Object { $_.path -eq 'Pages/Deep/CustomHandler.cs' }).Count -ne 1 -or
        @($nestedReceipt.sourceFiles | Where-Object { $_.path -eq 'Pages/Web.config' }).Count -ne 1 -or
        @($nestedReceipt.assemblyInventory | Where-Object { $_.disposition -eq 'operator-declared-out-of-scope' }).Count -ne 1) {
        throw 'EXISTING_PUBLISH_TEST_NESTED_SOURCE_OR_INVENTORY_INVALID'
    }
    if (!$IsWindows) {
        $alias = Join-Path $temp 'source-alias'
        [void][IO.Directory]::CreateSymbolicLink($alias, $nestedSource)
        $captured = $null
        $aliasLines = @()
        try {
            $aliasLines = @(& $script -SourceSiteRoot $nestedSource -PublishedRoot $nestedPublish `
                -PagePath 'Pages/Deep/Lookup.aspx' -OutputRoot (Join-Path $alias 'proof') `
                -TraceMapRoot $TraceMapRoot -OperatorAttestsRemainingOutOfScope -PrepareOnly)
        } catch { $captured = $_.Exception.Message }
        if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_OUTPUT_INSIDE_INPUT') {
            throw "EXISTING_PUBLISH_TEST_OUTPUT_ALIAS_NOT_REJECTED:${captured}:$($aliasLines -join ',')"
        }
    }
    if ($IsWindows) {
        $publicSite = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-publish-projectless'
        $publicPublish = Join-Path $temp 'public-publish'
        & (Join-Path $TraceMapRoot 'scripts/validation/Test-PublicWebFormsPublish.ps1') `
            -TraceMapRoot $TraceMapRoot -OutputRoot $publicPublish | Out-Null
        $publicProof = @(& $script -SourceSiteRoot $publicSite -PublishedRoot $publicPublish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
            -OutputRoot (Join-Path $temp 'public-proof') -TraceMapRoot $TraceMapRoot `
            -OperatorAttestsExactSourceCommit)
        if ($publicProof -notcontains 'existingPublishScan=bound' -or
            @($publicProof | Where-Object { $_ -match '^sqlQueryPaths=[1-9][0-9]*$' }).Count -ne 1) {
            throw "EXISTING_PUBLISH_TEST_WINDOWS_CHAIN_FAILED:$($publicProof -join ',')"
        }
        $maplessPublish = Join-Path $temp 'public-mapless-publish'
        & (Join-Path $TraceMapRoot 'scripts/validation/Test-PublicWebFormsPublish.ps1') `
            -TraceMapRoot $TraceMapRoot -OutputRoot $maplessPublish -Updatable | Out-Null
        $maplessProofRoot = Join-Path $temp 'public-mapless-proof'
        $maplessSite = Join-Path $temp 'mapless-source'
        Copy-Item -LiteralPath (Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-publish-mapless') `
            -Destination $maplessSite -Recurse
        & git -C $maplessSite init -q
        & git -C $maplessSite remote add origin 'https://example.invalid/public-mapless.git'
        & git -C $maplessSite add .
        & git -C $maplessSite -c user.name=PublicTest -c user.email=public@example.invalid commit -qm synthetic
        if ($LASTEXITCODE -ne 0) { throw 'EXISTING_PUBLISH_TEST_MAPLESS_SOURCE_GIT_FAILED' }
        $maplessProof = @(& $script -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
            -AdditionalAssemblyName @('App_global.asax.dll', 'App_WebReferences.dll') `
            -OutputRoot $maplessProofRoot -TraceMapRoot $TraceMapRoot -OperatorAttestsExactSourceCommit)
        $maplessPaths = [IO.File]::ReadAllText((Join-Path $maplessProofRoot 'handler-paths.json')) | ConvertFrom-Json -Depth 50
        if ($maplessProof -notcontains 'pageMapMatch=mapless' -or
            $maplessProof -notcontains 'matchedPageMaps=0' -or
            $maplessProof -notcontains 'existingPublishScan=bound' -or
            @($maplessPaths.paths | Where-Object {
                @($_.edges.edgeKind) -contains 'projectless-publish-method-candidate'
            }).Count -lt 1) {
            throw 'EXISTING_PUBLISH_TEST_MAPLESS_CHAIN_FAILED'
        }
        $contextProofRoot = Join-Path $temp 'public-mapless-context-proof'
        $contextProof = @(& $script -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
            -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
            -OutputRoot $contextProofRoot -TraceMapRoot $TraceMapRoot -OperatorAttestsExactSourceCommit)
        $contextReceipt = [IO.File]::ReadAllText((Join-Path $contextProofRoot 'publish-receipt.local.json')) |
            ConvertFrom-Json -Depth 20
        $contextPaths = [IO.File]::ReadAllText((Join-Path $contextProofRoot 'handler-paths.json')) |
            ConvertFrom-Json -Depth 50
        if ($contextProof -notcontains 'selectedDlls=4' -or
            $contextProof -notcontains 'sourceCommitDlls=2' -or
            $contextProof -notcontains 'artifactContextDlls=2' -or
            $contextProof -notcontains 'excludedDlls=0' -or
            $contextProof -notcontains 'compiledBoundInputs=2' -or
            $contextProof -notcontains 'compiledContextUnboundInputs=2' -or
            $contextProof -notcontains 'existingPublishScan=bound-with-unbound-context' -or
            $contextProof -notcontains 'existingPublishPaths=review-candidate' -or
            @($contextReceipt.assemblyInventory | Where-Object {
                $_.disposition -eq 'artifact-context-no-source-commit'
            }).Count -ne 2 -or
            @($contextPaths.paths | Where-Object {
                @($_.edges.edgeKind) -contains 'projectless-publish-method-candidate'
            }).Count -lt 1) {
            throw 'EXISTING_PUBLISH_TEST_MAPLESS_CONTEXT_PROVENANCE_FAILED'
        }
        $snapshotScript = Join-Path $TraceMapRoot 'scripts/wf.ps1'
        & git -C $maplessSite config core.ignorecase true
        $configBlob = ([string](& git -C $maplessSite rev-parse 'HEAD:Web.config')).Trim()
        & git -C $maplessSite update-index --force-remove Web.config
        & git -C $maplessSite update-index --add --cacheinfo "100644,$configBlob,web.config"
        & git -C $maplessSite -c user.name=PublicTest -c user.email=public@example.invalid `
            commit -qm config-case-alias
        if ($LASTEXITCODE -ne 0 -or @(& git -C $maplessSite status --porcelain).Count -ne 0) {
            throw 'EXISTING_PUBLISH_TEST_CONFIG_CASE_ALIAS_GIT_FAILED'
        }
        $originalConfig = [IO.File]::ReadAllBytes((Join-Path $maplessSite 'Web.config'))
        $originalHead = ([string](& git -C $maplessSite rev-parse HEAD)).Trim()
        & git -C $maplessSite update-index --assume-unchanged web.config
        [IO.File]::AppendAllText((Join-Path $maplessSite 'Web.config'),
            "`n<!-- Public CodeDOM configuration change -->`n", [Text.UTF8Encoding]::new($false))
        $snapshotRoot = Join-Path $temp 'local-source-snapshot'
        $snapshotLines = @(& $snapshotScript -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' -SnapshotRoot $snapshotRoot `
            -OutputRoot (Join-Path $temp 'local-snapshot-proof') -TraceMapRoot $TraceMapRoot -PrepareOnly)
        $snapshotCommit = ([string](& git -C $snapshotRoot rev-parse HEAD)).Trim()
        $snapshotChanged = @(& git -C $snapshotRoot diff-tree --no-commit-id --name-only -r HEAD)
        $snapshotReceipt = [IO.File]::ReadAllText(($snapshotRoot + '.receipt.local.json')) |
            ConvertFrom-Json -Depth 10
        if ($snapshotLines -notcontains 'sourceSnapshotConfigDifferences=1' -or
            $snapshotLines -notcontains 'sourceSnapshotOtherDifferences=0' -or
            $snapshotLines -notcontains 'sourceSnapshotHashErrors=0' -or
            $snapshotLines -notcontains 'sourceSnapshotKind=local-config-commit' -or
            $snapshotLines -notcontains 'existingPublishPreparation=valid' -or
            $snapshotCommit -eq $originalHead -or
            ([string](& git -C $snapshotRoot rev-parse HEAD^)).Trim() -ne $originalHead -or
            $snapshotChanged.Count -ne 1 -or $snapshotChanged[0] -cne 'web.config' -or
            $snapshotReceipt.schemaVersion -ne 'webforms-local-config-snapshot.v1' -or
            $snapshotReceipt.generatorSha256 -cne
                (Get-FileHash -LiteralPath $snapshotScript -Algorithm SHA256).Hash.ToLowerInvariant() -or
            $snapshotReceipt.boundedInputSha256 -cnotmatch '^[0-9a-f]{64}$' -or
            !$snapshotReceipt.configOnlyTrackedDifference -or
            $snapshotReceipt.snapshotCommitSha -cne $snapshotCommit -or
            ([string](& git -C $maplessSite rev-parse HEAD)).Trim() -ne $originalHead) {
            throw 'EXISTING_PUBLISH_TEST_LOCAL_CONFIG_SNAPSHOT_INVALID'
        }
        & git -C $maplessSite worktree remove -- $snapshotRoot
        if ($LASTEXITCODE -ne 0) { throw 'EXISTING_PUBLISH_TEST_SNAPSHOT_WORKTREE_REMOVE_FAILED' }
        [IO.File]::WriteAllBytes((Join-Path $maplessSite 'Web.config'), $originalConfig)
        & git -C $maplessSite update-index --no-assume-unchanged web.config
        if (@(& git -C $maplessSite status --porcelain).Count -ne 0) {
            throw 'EXISTING_PUBLISH_TEST_SNAPSHOT_SOURCE_NOT_RESTORED'
        }
        & git -C $maplessSite config core.autocrlf false
        $configText = [IO.File]::ReadAllText((Join-Path $maplessSite 'Web.config'))
        [IO.File]::WriteAllText((Join-Path $maplessSite 'Web.config'),
            $configText.Replace("`r`n", "`n").Replace("`n", "`r`n"),
            [Text.UTF8Encoding]::new($false))
        & git -C $maplessSite add -u -- .
        & git -C $maplessSite -c user.name=PublicTest -c user.email=public@example.invalid `
            commit -qm raw-crlf-config
        if ($LASTEXITCODE -ne 0) { throw 'EXISTING_PUBLISH_TEST_RAW_CONFIG_GIT_FAILED' }
        & git -C $maplessSite config core.autocrlf true
        $rawExpected = ([string](& git -C $maplessSite rev-parse HEAD:web.config)).Trim()
        $rawActual = ([string](& git -C $maplessSite hash-object --no-filters `
            (Join-Path $maplessSite 'Web.config'))).Trim()
        $filteredActual = ([string](& git -C $maplessSite hash-object --path=web.config `
            (Join-Path $maplessSite 'Web.config'))).Trim()
        if ($rawActual -cne $rawExpected -or $filteredActual -ceq $rawExpected) {
            throw 'EXISTING_PUBLISH_TEST_RAW_CONFIG_FILTER_SETUP_FAILED'
        }
        $rawProof = @(& $script -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
            -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
            -OutputRoot (Join-Path $temp 'raw-crlf-proof') -TraceMapRoot $TraceMapRoot -PrepareOnly)
        if ($rawProof -notcontains 'sourceTrackingCaseAliases=1' -or
            $rawProof -notcontains 'existingPublishPreparation=valid') {
            throw 'EXISTING_PUBLISH_TEST_EXACT_RAW_CONFIG_NOT_ADMITTED'
        }
        & git -C $maplessSite config core.autocrlf false
        [IO.File]::WriteAllText((Join-Path $maplessSite '.gitignore'),
            "App_Code/IgnoredPublic.vb`n", [Text.UTF8Encoding]::new($false))
        & git -C $maplessSite add .gitignore
        & git -C $maplessSite -c user.name=PublicTest -c user.email=public@example.invalid commit -qm ignore-public-file
        if ($LASTEXITCODE -ne 0) { throw 'EXISTING_PUBLISH_TEST_IGNORE_GIT_FAILED' }
        $ignoredSource = Join-Path $maplessSite 'App_Code/IgnoredPublic.vb'
        [IO.File]::WriteAllText($ignoredSource, 'Public Class IgnoredPublic : End Class',
            [Text.UTF8Encoding]::new($false))
        $missingLines = @()
        $captured = $null
        try {
            & $script -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
                -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
                -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
                -OutputRoot (Join-Path $temp 'ignored-source-proof') -TraceMapRoot $TraceMapRoot -PrepareOnly |
                ForEach-Object { $missingLines += $_ }
        } catch { $captured = $_.Exception.Message }
        if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_SOURCE_NOT_COMMITTED' -or
            $missingLines -notcontains 'sourceNotCommittedCount=1' -or
            $missingLines -notcontains 'sourceNotCommittedAppCodeCount=1') {
            throw 'EXISTING_PUBLISH_TEST_IGNORED_SOURCE_NOT_REJECTED'
        }
        Remove-Item -LiteralPath $ignoredSource
        & git -C $maplessSite config core.ignorecase true
        $blob = ([string](& git -C $maplessSite rev-parse 'HEAD:App_Code/BusinessLogic.vb')).Trim()
        if ($LASTEXITCODE -ne 0 -or $blob -cnotmatch '^[0-9a-f]{40}$') {
            throw 'EXISTING_PUBLISH_TEST_CASE_ALIAS_BLOB_UNAVAILABLE'
        }
        & git -C $maplessSite update-index --force-remove 'App_Code/BusinessLogic.vb'
        & git -C $maplessSite update-index --add --cacheinfo "100644,$blob,app_code/BusinessLogic.vb"
        & git -C $maplessSite -c user.name=PublicTest -c user.email=public@example.invalid commit -qm case-alias
        if ($LASTEXITCODE -ne 0 -or @(& git -C $maplessSite status --porcelain).Count -ne 0) {
            throw 'EXISTING_PUBLISH_TEST_CASE_ALIAS_GIT_FAILED'
        }
        $caseAliasLines = @(& $script -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
            -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
            -OutputRoot (Join-Path $temp 'case-alias-proof') -TraceMapRoot $TraceMapRoot -PrepareOnly)
        if ($caseAliasLines -notcontains 'sourceTrackingCaseAliases=2' -or
            $caseAliasLines -notcontains 'existingPublishPreparation=valid') {
            throw 'EXISTING_PUBLISH_TEST_TRACKED_CASE_ALIAS_NOT_ADMITTED'
        }
        & git -C $maplessSite update-index --assume-unchanged 'app_code/BusinessLogic.vb'
        [IO.File]::AppendAllText((Join-Path $maplessSite 'App_Code/BusinessLogic.vb'),
            "`n' Public test mismatch`n", [Text.UTF8Encoding]::new($false))
        if (@(& git -C $maplessSite status --porcelain).Count -ne 0) {
            throw 'EXISTING_PUBLISH_TEST_ASSUMED_UNCHANGED_NOT_CLEAN'
        }
        $mismatchLines = @()
        $captured = $null
        try {
            & $script -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
                -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
                -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
                -OutputRoot (Join-Path $temp 'case-alias-mismatch-proof') -TraceMapRoot $TraceMapRoot `
                -PrepareOnly | ForEach-Object { $mismatchLines += $_ }
        } catch { $captured = $_.Exception.Message }
        if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_SOURCE_MISMATCH' -or
            $mismatchLines -notcontains 'sourceTrackingCaseAliases=2' -or
            $mismatchLines -notcontains 'sourceMismatchCount=1' -or
            $mismatchLines -notcontains 'sourceMismatchAppCodeCount=1' -or
            $mismatchLines -notcontains 'sourceMismatchHashErrorCount=0') {
            throw 'EXISTING_PUBLISH_TEST_CHANGED_CASE_ALIAS_NOT_REJECTED'
        }
        $snapshotGapLines = @()
        $captured = $null
        try {
            & $snapshotScript -SourceSiteRoot $maplessSite -PublishedRoot $maplessPublish `
                -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
                -SnapshotRoot (Join-Path $temp 'other-source-snapshot') `
                -TraceMapRoot $TraceMapRoot -PrepareOnly |
                ForEach-Object { $snapshotGapLines += $_ }
        } catch { $captured = $_.Exception.Message }
        if ($captured -ne 'WEBFORMS_SNAPSHOT_SOURCE_NOT_CONFIG_ONLY' -or
            $snapshotGapLines -notcontains 'sourceSnapshotConfigDifferences=0' -or
            $snapshotGapLines -notcontains 'sourceSnapshotOtherDifferences=1' -or
            (Test-Path -LiteralPath (Join-Path $temp 'other-source-snapshot'))) {
            throw 'EXISTING_PUBLISH_TEST_OTHER_SOURCE_SNAPSHOT_NOT_REJECTED'
        }
    }
    $captured = $null
    try {
        & $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath '../Lookup.aspx' `
            -OutputRoot (Join-Path $temp 'unsafe') -PrepareOnly *> $null
    } catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_UNSAFE_PATH') {
        throw 'EXISTING_PUBLISH_TEST_UNSAFE_PATH_NOT_REJECTED'
    }
    [IO.File]::WriteAllText($map,
        '<preserve virtualPath="/Pages/Other.aspx" assembly="CompiledProjectless.VB" type="PublicProof.LookupPage" />',
        [Text.UTF8Encoding]::new($false))
    $captured = $null
    try {
        & $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath 'Pages/Lookup.aspx' `
            -OutputRoot (Join-Path $temp 'mismatch') -PrepareOnly *> $null
    } catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_MAPLESS_WEB_ASSEMBLY_UNAVAILABLE') {
        throw "EXISTING_PUBLISH_TEST_MAP_MISMATCH_NOT_REJECTED:$captured"
    }
    Write-Output 'existingPublishPublicTests=passed'
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
