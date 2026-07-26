param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("doctor", "build", "synthetic")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{40}$")]
    [string]$ExpectedHead,

    [string]$VmName = "Windows 11 - Access Isolated",

    [string]$GuestRoot = "C:\TraceMapDev"
)

$ErrorActionPreference = "Stop"

function Stop-Host([string]$Classification) {
    [Console]::Error.WriteLine("error: $Classification")
    exit 1
}

if (-not (Get-Command "prlctl" -ErrorAction SilentlyContinue)) {
    Stop-Host "AccessParallelsControlUnavailable"
}

$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$vmInfo = (& prlctl list --info $VmName 2>&1 | Out-String)
$vmInfoExit = $LASTEXITCODE
$ErrorActionPreference = $previousPreference
if ($vmInfoExit -ne 0) {
    Stop-Host "AccessParallelsVmUnavailable"
}
if ($vmInfo -notmatch "(?m)^State:\s+running\s*$") {
    Stop-Host "AccessParallelsVmNotRunning"
}
if ($vmInfo -notmatch "(?m)^\s+net0\s+\(-\)") {
    Stop-Host "AccessParallelsNetworkEnabled"
}
if ($vmInfo -notmatch "(?m)^\s+access_input\s+\(\+\).+mode='ro'\s*$" -or
    $vmInfo -notmatch "(?m)^\s+access_output\s+\(\+\).+mode='rw'\s*$") {
    Stop-Host "AccessParallelsScopedSharesUnavailable"
}

$guestScript = "$($GuestRoot.TrimEnd([char]92))\tracemap\scripts\access-validation\Invoke-AccessGuestSource.ps1"
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$guestOutput = (& prlctl exec $VmName powershell.exe `
    -NoProfile `
    -NonInteractive `
    -ExecutionPolicy Bypass `
    -File $guestScript `
    -Action $Action `
    -ExpectedHead $ExpectedHead `
    -GuestRoot $GuestRoot 2>&1 | Out-String).Trim()
$guestExit = $LASTEXITCODE
$ErrorActionPreference = $previousPreference
if ($guestExit -ne 0) {
    Stop-Host "AccessParallelsGuestActionFailed"
}

$expectedPrefix = switch ($Action) {
    "doctor" { "access-parallels-doctor=ready" }
    "build" { "access-parallels-build=completed" }
    "synthetic" { "access-parallels-synthetic=completed" }
}
$safePattern = "^$([Regex]::Escape($expectedPrefix));head=$ExpectedHead;(?:[A-Za-z][A-Za-z0-9]*=(?:true|false|completed|ready);?)+$"
if ($guestOutput -notmatch $safePattern -or
    $guestOutput -match "[\\/:]" -or
    $guestOutput.Contains([Environment]::NewLine, [StringComparison]::Ordinal)) {
    Stop-Host "AccessParallelsGuestOutputInvalid"
}

Write-Output $guestOutput
