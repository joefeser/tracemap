Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../WebFormsRequeryDiagnostics.ps1')
$global:DiagnosticCalls = [Collections.Generic.List[string]]::new()
$global:DiagnosticError = 'error: WEBFORMS_EXECUTION_CHECKPOINT_INVALID'
function global:dotnet {
    $run = [string]$args[4]
    $global:DiagnosticCalls.Add($run)
    if ($run -cmatch '^C:') {
        $global:LASTEXITCODE=1
        $global:DiagnosticError
    } else {
        $global:LASTEXITCODE=0
        '{"state":"reports-completed-review-only"}'
    }
}
try {
    $result = Get-WebFormsVerifiedStatus 'synthetic.dll' 'C:\work\review\run'
    if ($result.Run -cne 'c:\work\review\run' -or $global:DiagnosticCalls.Count -ne 2) { throw 'Drive casing retry failed.' }
    $global:DiagnosticCalls.Clear()
    $global:DiagnosticError='error: WEBFORMS_EXECUTION_CHECKPOINT_PAYLOAD_CHANGED'
    try { Get-WebFormsVerifiedStatus 'synthetic.dll' 'C:\work\review\run'; throw 'Expected failure' }
    catch { if ($_.Exception.Message -notlike 'WEBFORMS_HANDLER_STATUS_FAILED*') { throw } }
    if ($global:DiagnosticCalls.Count -ne 1) { throw 'Unexpected retry of integrity failure.' }
    $global:DiagnosticCalls.Clear()
    $null = Get-WebFormsVerifiedStatus 'synthetic.dll' '/tmp/review/run'
    if ($global:DiagnosticCalls.Count -ne 1) { throw 'Unexpected non-Windows retry.' }
    Write-Output 'Web Forms casing diagnostics tests passed (synthetic Windows paths).'
} finally {
    Remove-Item Function:\dotnet
    Remove-Variable DiagnosticCalls,DiagnosticError -Scope Global
}
