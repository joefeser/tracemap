$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wprewalk.ps1'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-prewalk-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($folder)
try {
    $inputPath = Join-Path $folder 'compiled-paths.local.html'
    # Split the marker across a reader chunk boundary; no private content is printed.
    [IO.File]::WriteAllText($inputPath, ('x' * 4090) + 'TerminalReachabilityPrewalk' + 'private-local-content')
    $before = (Get-FileHash -LiteralPath $inputPath).Hash
    $output = @(& $helper -ReportFolder $folder)
    if ($output -notcontains 'baseline.prewalkPresent=true') { throw 'Missing split marker' }
    if (($output -join "`n") -match 'private-local-content') { throw 'Content leaked' }
    if ((Get-FileHash -LiteralPath $inputPath).Hash -ne $before) { throw 'Input changed' }
    [IO.File]::WriteAllText($inputPath, 'compiled-il-call only')
    if (@(& $helper -ReportFolder $folder) -notcontains 'baseline.prewalkPresent=false') { throw 'False marker match' }
    Remove-Item -LiteralPath $inputPath
    try { & $helper -ReportFolder $folder; throw 'Missing input accepted' }
    catch { if ($_.Exception.Message -eq 'Missing input accepted') { throw } }
    Write-Output 'webFormsPrewalkPublicTests=passed'
} finally { [IO.Directory]::Delete($folder, $true) }
