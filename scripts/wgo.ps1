[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceSiteRoot,
    [Parameter(Mandatory)][string]$TypeName,
    [Parameter(Mandatory)][string]$MethodName,
    [string]$OutputRoot
)

# Fast check first; replay the saved publish only if every selected overload
# is independently agreed. The replay writes scan-ilwork-30000000 separately.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_GO_POWERSHELL_7_REQUIRED' }
$diagnosticArguments = @{ TypeName = $TypeName; MethodName = $MethodName }
if ($OutputRoot) { $diagnosticArguments.OutputRoot = $OutputRoot }
$lines = @(& (Join-Path $PSScriptRoot 'wid.ps1') @diagnosticArguments)
foreach ($line in $lines) {
    if ($line -cmatch '^ilReaderProbe(?:Status|DisputedBodies|FirstCecilOpcode|FirstRawOpcode|Selected[A-Za-z0-9]+)=' -or
        $line -cmatch '^methodOverload[0-9]+Has(?:ArrayList|ArrayParameter|ByRef)=') {
        Write-Output $line
    }
}
$selected = @($lines | Where-Object { $_ -cmatch '^ilReaderProbeSelectedMethods=[0-9]+$' })
$agreed = @($lines | Where-Object { $_ -cmatch '^ilReaderProbeSelectedAgreed=[0-9]+$' })
if ($selected.Count -ne 1 -or $agreed.Count -ne 1) { throw 'WEBFORMS_GO_DIAGNOSTIC_UNAVAILABLE' }
$selectedCount = [int](($selected[0] -split '=')[1])
$agreedCount = [int](($agreed[0] -split '=')[1])
if ($selectedCount -lt 1 -or $agreedCount -ne $selectedCount) {
    Write-Output 'replaySkipped=selected-method-disagreement'
    return
}
$replayArguments = @{
    SourceSiteRoot = $SourceSiteRoot
    TypeName = $TypeName
    MethodName = $MethodName
    IlMaxWork = 30000000
}
if ($OutputRoot) { $replayArguments.OutputRoot = $OutputRoot }
& (Join-Path $PSScriptRoot 'wr.ps1') @replayArguments
