#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
    [string]$OutputRoot,
    [switch]$RequireWindowsPublish
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RequireWindowsPublish -and !$IsWindows) { throw 'DEEP_CORPUS_WINDOWS_REQUIRED' }
$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
$output = if ($OutputRoot) { [IO.Path]::GetFullPath($OutputRoot) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('tracemap-deep-corpus-' + [guid]::NewGuid().ToString('N'))
}
if (Test-Path -LiteralPath $output) { throw 'DEEP_CORPUS_OUTPUT_NOT_FRESH' }
[void][IO.Directory]::CreateDirectory($output)
$previous = $env:TRACEMAP_DEEP_CORPUS_ROOT
$previousOperator = $env:TRACEMAP_OPERATOR_ROOT
try {
    $env:TRACEMAP_DEEP_CORPUS_ROOT = $output
    $env:TRACEMAP_OPERATOR_ROOT = Join-Path $output 'operator'
    $sourceInputs = @(
        'samples/messy-dotnet-workspace/vb-deep-projectless/Lookup.aspx',
        'samples/messy-dotnet-workspace/vb-deep-projectless/Lookup.aspx.vb',
        'samples/messy-dotnet-workspace/vb-deep-projectless/Web.config',
        'samples/messy-dotnet-workspace/vb-publish-crossdll-framework/PublicProof.Framework.vbproj',
        'samples/fixture-build/deep-projectless/DeepWebsite.vbproj',
        'src/dotnet/tests/TraceMap.Tests/IlCommandBindingExtractorTests.cs',
        'src/dotnet/tests/TraceMap.Tests/DeepProjectlessNativeWorkflowTests.cs',
        'src/dotnet/tests/TraceMap.Tests/LazyConstructorLoggingTests.cs',
        'src/dotnet/tests/TraceMap.Tests/WebFormsOperatorWorkflowTests.cs',
        'scripts/wlocal.ps1',
        'scripts/wcompare.ps1',
        'scripts/wsqlroute.ps1',
        'scripts/tests/Test-WebFormsChainComparison.ps1',
        'scripts/tests/Test-WebFormsSqlRoute.ps1',
        'scripts/tests/Test-WebFormsCapShortcut.ps1',
        'scripts/wcap.ps1',
        'samples/fixture-build/lazy-constructor/LazyWebsite.vbproj',
        'samples/fixture-build/lazy-constructor/provider/LoggingProvider.vbproj',
        'samples/messy-dotnet-workspace/vb-lazy-constructor/Overview.aspx',
        'samples/messy-dotnet-workspace/vb-lazy-constructor/Overview.aspx.vb',
        'samples/messy-dotnet-workspace/vb-lazy-logging-provider/PublicLog.vb',
        'src/dotnet/tests/TraceMap.Tests/WindowsDeepCorpusTheoryAttribute.cs',
        'src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj',
        'src/dotnet/tests/TraceMap.Tests/CombinedDependencyPathTests.cs',
        'src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs',
        'src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.IndexedGraph.cs',
        'scripts/validation/Test-PublicWebFormsPublish.ps1'
    )
    function SourceRoster {
        $provider = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-publish-crossdll-framework'
        $inputs = @($sourceInputs) + @(Get-ChildItem -LiteralPath $provider -File -Filter '*.vb' |
            ForEach-Object { [IO.Path]::GetRelativePath($TraceMapRoot, $_.FullName).Replace('\', '/') })
        @($inputs | Sort-Object | ForEach-Object {
            $path = Join-Path $TraceMapRoot $_
            if ((Get-Item -LiteralPath $path).Length -gt 1048576) { throw 'DEEP_CORPUS_INPUT_LIMIT' }
            $_ + ':' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        })
    }
    $beforeInputs = @(SourceRoster)
    $generatorSha = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $project = Join-Path $TraceMapRoot 'src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj'
    & dotnet test $project --filter 'FullyQualifiedName~Deep_projectless|FullyQualifiedName~Property_profile_dynamic_lookup|FullyQualifiedName~WebFormsOperatorWorkflowTests' --verbosity minimal `
        --logger 'trx;LogFileName=deep-corpus.trx' --results-directory (Join-Path $output 'tests')
    if ($LASTEXITCODE -ne 0) { throw 'DEEP_CORPUS_TEST_FAILED;outputs-preserved' }
    $trx = Join-Path $output 'tests/deep-corpus.trx'
    if (!(Test-Path -LiteralPath $trx -PathType Leaf)) { throw 'DEEP_CORPUS_TEST_RECEIPT_MISSING' }
    [xml]$results = [IO.File]::ReadAllText($trx)
    $rows = @($results.SelectNodes('//*[local-name()="UnitTestResult"]'))
    $passed = @($rows | Where-Object { $_.outcome -ceq 'Passed' }).Count
    $skipped = @($rows | Where-Object { $_.outcome -ceq 'NotExecuted' }).Count
    $windowsPassed = @($rows | Where-Object {
        $_.testName -like '*Deep_projectless_Windows_ASPNET_publish*' -and $_.outcome -ceq 'Passed'
    }).Count
    $nativePassed = @($rows | Where-Object {
        $_.testName -like '*Deep_projectless_native_start*' -and $_.outcome -ceq 'Passed'
    }).Count
    $propertyPassed = @($rows | Where-Object {
        $_.testName -like '*Deep_projectless_property_constructor_logging*' -and $_.outcome -ceq 'Passed'
    }).Count
    $returnPassed = @($rows | Where-Object {
        $_.testName -like '*Deep_projectless_return_value_projection*' -and $_.outcome -ceq 'Passed'
    }).Count
    $profilePassed = @($rows | Where-Object {
        $_.testName -like '*Property_profile_dynamic_lookup*' -and $_.outcome -ceq 'Passed'
    }).Count
    $operatorPassed = @($rows | Where-Object {
        $_.testName -like '*WebForms_operator_source_compiled_and_separate_dll_reports*' -and $_.outcome -ceq 'Passed'
    }).Count
    if ($passed -lt 32 -or $nativePassed -ne 1 -or $propertyPassed -ne 2 -or $returnPassed -ne 11 -or $profilePassed -ne 2 -or $operatorPassed -ne 5 -or @($rows | Where-Object { $_.outcome -notin @('Passed', 'NotExecuted') }).Count -ne 0) {
        throw 'DEEP_CORPUS_TEST_RECEIPT_NOT_ADMITTED'
    }
    foreach ($layout in @('attached', 'separate', 'separate-dll-only', 'reversed', 'missing')) {
        $suffix = '(layout: "' + $layout + '")'
        if (@($rows | Where-Object {
            $_.testName -like '*WebForms_operator_source_compiled_and_separate_dll_reports*' -and
            $_.testName.EndsWith($suffix, [StringComparison]::Ordinal) -and $_.outcome -ceq 'Passed'
        }).Count -ne 1) { throw 'DEEP_CORPUS_OPERATOR_LAYOUT_MISSING' }
    }
    if ($RequireWindowsPublish -and $windowsPassed -ne 2) { throw 'DEEP_CORPUS_WINDOWS_ACCEPTANCE_MISSING' }
    foreach ($test in @('Test-WebFormsChainComparison.ps1', 'Test-WebFormsSqlRoute.ps1', 'Test-WebFormsCapShortcut.ps1')) {
        & (Join-Path $TraceMapRoot ('scripts/tests/' + $test))
    }
    foreach ($layout in @('attached', 'separate', 'separate-dll-only', 'reversed')) {
        $folder = Join-Path $output ('operator/' + $layout)
        $current = Join-Path $folder 'all/paths-report.json'
        $comparison = @(& (Join-Path $TraceMapRoot 'scripts/wcompare.ps1') `
            -Historical (Join-Path $folder 'capped/paths-report.json') -Current $current -Mixed $current `
            -Handler Profile_Click -OutputPath (Join-Path $folder 'cap-comparison.local.html'))
        if ($comparison -notcontains 'compare.sharedExact=1;historicalOnly=0;currentOnly=2;variantCountDifferences=0;symbolSequenceMatches=1') {
            throw 'DEEP_CORPUS_OPERATOR_COMPARISON_MISMATCH'
        }
        $ledger = @(& (Join-Path $TraceMapRoot 'scripts/wsqlroute.ps1') -Report $current -Handler Profile_Click `
            -UnresolvedOnly -OutputPath (Join-Path $folder 'unresolved-command.local.html'))
        if ($ledger -notcontains 'sqlRoute.unresolvedOnly=true;unresolvedGroups=1;displayedGroups=1') {
            throw 'DEEP_CORPUS_OPERATOR_LEDGER_MISMATCH'
        }
    }
    $inputLines = @(SourceRoster)
    if (($inputLines -join "`n") -cne ($beforeInputs -join "`n") -or
        $generatorSha -cne (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw 'DEEP_CORPUS_INPUT_CHANGED;outputs-preserved-not-admitted'
    }
    $execution = Join-Path $TraceMapRoot 'src/dotnet/tests/TraceMap.Tests/bin/Debug/net10.0'
    $executionPaths = @('TraceMap.Tests.dll', 'tracemap.dll', 'TraceMap.Core.dll', 'TraceMap.Reporting.dll', 'TraceMap.Combine.dll', 'TraceMap.Storage.dll')
    $inputLines += @($executionPaths | ForEach-Object {
        'execution/' + $_ + ':' + (Get-FileHash -LiteralPath (Join-Path $execution $_) -Algorithm SHA256).Hash.ToLowerInvariant()
    })
    foreach ($fixture in @('PublicLazy.Website.dll', 'PublicLazy.Framework.dll')) {
        $path = Join-Path $TraceMapRoot ('samples/fixture-build/lazy-constructor/bin/Debug/net48/' + $fixture)
        $inputLines += 'fixture/' + $fixture + ':' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $inputLines += 'test-result:' + (Get-FileHash -LiteralPath $trx -Algorithm SHA256).Hash.ToLowerInvariant()
    $inputSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes(($inputLines -join "`n") + "`n"))).ToLowerInvariant()
    $receipt = [ordered]@{
        schemaVersion = 'deep-projectless-corpus-validation.v1'
        ruleId = 'validation.deep-projectless-corpus.v1'
        evidenceTier = 'Tier2Structural'
        visibility = 'local-only'
        generatorSha256 = $generatorSha
        boundedInputSha256 = $inputSha
        boundedInputs = $inputLines
        passedTests = $passed
        skippedTests = $skipped
        windowsPublishTestsPassed = $windowsPassed
        profileDynamicLookupTestsPassed = $profilePassed
        operatorWorkflowTestsPassed = $operatorPassed
        operatorDiagnosticLayoutsPassed = 4
        windowsPublishAcceptance = if ($windowsPassed -eq 2) { 'tested' } else { 'not-run' }
        limitations = @('Synthetic static corpus only; no database methods executed.',
            'Logical graph payload counters and artifact caps are not physical drive-read measurements.',
            'No private-assembly, parameter-value, runtime-dispatch or migration-completeness claim.')
    }
    [IO.File]::WriteAllText((Join-Path $output 'validation.local.json'),
        (($receipt | ConvertTo-Json -Depth 6) + "`n"), [Text.UTF8Encoding]::new($false))
    Write-Output "deepCorpus.passed=$passed;skipped=$skipped;windowsPublishPassed=$windowsPassed;profileDynamicLookupPassed=$profilePassed;operatorWorkflowPassed=$operatorPassed"
    Write-Output "deepCorpus.output=$output"
    Write-Output 'deepCorpus=synthetic-static-validation;no-sql-executed;not-private-acceptance'
} finally {
    $env:TRACEMAP_DEEP_CORPUS_ROOT = $previous
    $env:TRACEMAP_OPERATOR_ROOT = $previousOperator
}
