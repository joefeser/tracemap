param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("doctor", "build", "synthetic")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{40}$")]
    [string]$ExpectedHead,

    [string]$VmName = "Windows 11 - Access Isolated",

    [string]$GuestRoot = "C:\TraceMapDev",

    [ValidatePattern("^[0-9a-f]{64}$")]
    [string]$ExpectedGitSha256 = "b05b2d7eb80933c602272b5ddf132adf288cf78ad8e32a7a47ca7e200076b9f3",

    [ValidatePattern("^[0-9a-f]{64}$")]
    [string]$ExpectedDotnetSha256 = "05602a1b5eff9cd0be076c25ac9ab31c5e2f76df824a35b8bc9a16ab340767b6"
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
if ($vmInfo -notmatch "(?m)^\s+net0\s+\(-\)" -or
    $vmInfo -match "(?m)^\s+net\d+\s+\(\+\)") {
    Stop-Host "AccessParallelsNetworkEnabled"
}
$shareMatches = [Regex]::Matches(
    $vmInfo,
    "(?m)^\s+(?<name>.+?)\s+\(\+\)\s+path='[^']*'\s+mode='(?<mode>ro|rw)'\s*$")
$shares = @(
    $shareMatches |
        ForEach-Object {
            "$($_.Groups["name"].Value)|$($_.Groups["mode"].Value)"
        }
)
if ($shares.Count -ne 2 -or
    $shares -notcontains "access_input|ro" -or
    $shares -notcontains "access_output|rw") {
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
    -GuestRoot $GuestRoot `
    -ExpectedGitSha256 $ExpectedGitSha256 `
    -ExpectedDotnetSha256 $ExpectedDotnetSha256 2>&1 | Out-String).Trim()
$guestExit = $LASTEXITCODE
$ErrorActionPreference = $previousPreference
if ($guestExit -ne 0) {
    Stop-Host "AccessParallelsGuestActionFailed"
}

$expectedOutput = switch ($Action) {
    "doctor" {
        "access-parallels-doctor=ready;head=$ExpectedHead;sourceClean=true;remoteAbsent=true;accessAvailable=true"
    }
    "build" {
        "access-parallels-build=completed;head=$ExpectedHead;buildPassed=true;accessTestsPassed=true;sourceClean=true"
    }
    "synthetic" {
        "access-parallels-synthetic=completed;head=$ExpectedHead;consumerContracts=completed;reviewBundleRetained=true;processCleanup=true;sourceClean=true"
    }
}
if (-not [string]::Equals($guestOutput, $expectedOutput, [StringComparison]::Ordinal) -or
    $guestOutput -match "[\\/:]" -or
    $guestOutput.Contains([Environment]::NewLine, [StringComparison]::Ordinal)) {
    Stop-Host "AccessParallelsGuestOutputInvalid"
}

Write-Output $guestOutput
