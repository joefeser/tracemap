$ErrorActionPreference = 'Stop'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-command-facts-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory((Join-Path $folder 'review/run/attempts/public/scan'))
try {
    $path = Join-Path $folder 'review/run/attempts/public/scan/facts.ndjson'
    $helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wcmdfacts.ps1'
    # A type marker straddles the read-buffer boundary; escaped text is not a type.
    [IO.File]::WriteAllText($path, (' ' * 32760) + '{"factType":"ManagedIlCallValuesObserved"}' + "`n" +
        '{"factType":"ManagedIlDatabaseCommandCandidate"}' + "`n" + '{"factType":"ManagedIlCallValuesObserved"}')
    $result = @(& $helper -RunFolder $folder)
    if ($result -notcontains 'commandFacts.ManagedIlCallValuesObserved=2' -or
        $result -notcontains 'commandFacts.ManagedIlDatabaseCommandCandidate=1') { throw 'Chunked counts incorrect' }
    [IO.File]::WriteAllText($path, '{"factType":"AnalysisGap","properties":{"message":"ManagedIlDatabaseCommandCandidate"}}')
    $result = @(& $helper -RunFolder $folder)
    if ($result -notcontains 'commandFacts.ManagedIlCallValuesObserved=0' -or
        $result -notcontains 'commandFacts.ManagedIlDatabaseCommandCandidate=0') { throw 'Zero counts incorrect' }
    [IO.File]::WriteAllText($path, (' ' * 32760) + '{"properties":{"gapKind":"IlCommandUnknownCallEffects"}}' + "`n" +
        '{"properties":{"gapKind":"IlCommandBindingWorkLimit"}}' + "`n" +
        '{"properties":{"gapKind":"IlValueControlFlowAggregateWorkLimit"}}')
    $result = @(& $helper -RunFolder $folder)
    if ($result -notcontains 'commandFacts.gap.IlCommandUnknownCallEffects=1' -or
        $result -notcontains 'commandFacts.gap.IlCommandBindingWorkLimit=1' -or
        $result -notcontains 'commandFacts.gap.IlCommandTextBindingUnavailable=0' -or
        $result -notcontains 'commandFacts.gap.IlValueControlFlowAggregateWorkLimit=1' -or
        $result -notcontains 'commandFacts.gap.IlValueExposureOriginLimit=0') { throw 'Gap counts incorrect' }
    Write-Output 'webFormsCommandFactsPublicTests=passed'
} finally { [IO.Directory]::Delete($folder, $true) }
