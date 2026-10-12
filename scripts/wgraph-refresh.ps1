#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position=0)][string]$Root,
    [string]$Project,
    [string]$Handler,
    [string]$Method
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$Handler) { $Handler = Read-Host 'Handler name' }
if (!$Method) { $Method = Read-Host 'Method name to inspect' }
if ($Handler -cnotmatch '\A[A-Za-z0-9_]{1,128}\z') { throw 'WEBFORMS_HANDLER_INVALID' }
if (!$Method -or $Method.Length -gt 256) { throw 'WEBFORMS_METHOD_INVALID' }
$locator = @{Root=$Root}
if ($Project) { $locator.Project = $Project }
# Each helper owns its existing admission checks. Stop on any failure: never
# export an old graph after a failed fresh scan or query. No tests or uploads.
& (Join-Path $PSScriptRoot 'wrefresh.ps1') @locator
& (Join-Path $PSScriptRoot 'wrequery.ps1') @locator -LatestRefresh -MethodGraph -Handler $Handler
& (Join-Path $PSScriptRoot 'wgraph-share.ps1') $Root -Method $Method -PrivateMap
