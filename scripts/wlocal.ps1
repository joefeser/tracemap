#Requires -Version 7.0
[CmdletBinding()]
param([string]$OutputRoot, [switch]$RequireWindowsPublish)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# One fresh local replay; never reads private operator configuration or old runs.
& (Join-Path $PSScriptRoot 'validation/Test-DeepProjectlessCorpus.ps1') @PSBoundParameters
