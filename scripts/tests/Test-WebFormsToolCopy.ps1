$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wtoolcopy.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-tool-copy-test-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $temp 'source'
$destination = Join-Path $temp 'verification/tool'
[void][IO.Directory]::CreateDirectory((Join-Path $source 'nested'))
[IO.File]::WriteAllText((Join-Path $source 'tracemap.dll'), 'public synthetic entry')
[IO.File]::WriteAllText((Join-Path $source 'nested/dependency.dll'), 'public synthetic dependency')
try {
    $output = @(& $helper -SourceRoot $source -DestinationRoot $destination)
    if ($output -notcontains 'toolSnapshot=verified;files=2;originalCheckoutPreserved=true') { throw 'Tool snapshot was not verified' }
    foreach ($relative in @('tracemap.dll', 'nested/dependency.dll')) {
        $original = (Get-FileHash -LiteralPath (Join-Path $source $relative) -Algorithm SHA256).Hash
        $copied = (Get-FileHash -LiteralPath (Join-Path $destination $relative) -Algorithm SHA256).Hash
        if ($original -cne $copied) { throw 'Copied bytes differed' }
    }
    try { & $helper -SourceRoot $source -DestinationRoot $destination | Out-Null; throw 'Existing output was accepted' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_TOOL_COPY_ROOT_INVALID') { throw } }
    if (($output -join "`n") -match 'tracemap-tool-copy-test|public synthetic') { throw 'Private paths or bytes leaked' }
    Write-Output 'webFormsToolCopyPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
