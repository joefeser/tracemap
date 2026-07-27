param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("doctor", "build", "synthetic")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{40}$")]
    [string]$ExpectedHead,

    [string]$GuestRoot = "C:\TraceMapDev"
)

$ErrorActionPreference = "Stop"

function Stop-Guest([string]$Classification) {
    throw $Classification
}

$repository = Join-Path $GuestRoot "tracemap"
$git = Join-Path $GuestRoot "tools\mingit\cmd\git.exe"
$dotnet = Join-Path $GuestRoot "tools\dotnet\dotnet.exe"
$accessCli = Join-Path $repository "src\dotnet\TraceMap.Access.Cli\bin\Debug\net10.0\tracemap-access.exe"
$traceMapCli = Join-Path $repository "src\dotnet\TraceMap.Cli\bin\Debug\net10.0\tracemap.exe"
$solution = Join-Path $repository "src\dotnet\TraceMap.sln"
$tests = Join-Path $repository "src\dotnet\tests\TraceMap.Tests\TraceMap.Tests.csproj"
$generator = Join-Path $repository "scripts\access-validation\New-SyntheticAccessFixture.ps1"
$harness = Join-Path $repository "scripts\access-validation\Invoke-AccessSmoke.ps1"

foreach ($required in @($repository, $git, $dotnet, $solution, $tests)) {
    if (-not (Test-Path $required)) {
        Stop-Guest "AccessGuestSourceInputMissing"
    }
}

$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$head = (& $git -C $repository rev-parse HEAD 2>$null | Out-String).Trim()
$headExit = $LASTEXITCODE
$status = (& $git -C $repository status --porcelain 2>$null | Out-String)
$statusExit = $LASTEXITCODE
$dirty = -not [string]::IsNullOrWhiteSpace($status)
$remotes = (& $git -C $repository remote 2>$null | Out-String).Trim()
$remoteExit = $LASTEXITCODE
$ErrorActionPreference = $previousPreference
if ($headExit -ne 0 -or
    $statusExit -ne 0 -or
    $remoteExit -ne 0 -or
    $head -ne $ExpectedHead -or
    $dirty -or
    $remotes) {
    Stop-Guest "AccessGuestSourceIdentityMismatch"
}

$accessRegistration = Get-ItemProperty `
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\MSACCESS.EXE" `
    -ErrorAction SilentlyContinue
if ($null -eq $accessRegistration) {
    Stop-Guest "AccessGuestApplicationUnavailable"
}

$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:PATH = "$(Split-Path -Parent $git);$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:NUGET_PACKAGES = Join-Path $GuestRoot "packages"

if ($Action -eq "doctor") {
    Write-Output "access-parallels-doctor=ready;head=$head;sourceClean=true;remoteAbsent=true;accessAvailable=true"
    exit 0
}

if ($Action -eq "build") {
    $testClasses = @(
        "TraceMap.Tests.AccessFoundationTests",
        "TraceMap.Tests.AccessUiProjectionTests",
        "TraceMap.Tests.AccessVbaProjectionTests",
        "TraceMap.Tests.AccessMacroReportingTests",
        "TraceMap.Tests.AccessLocalReviewBundleTests",
        "TraceMap.Tests.AccessParallelsSourceRunnerTests"
    )
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $dotnet build $solution --no-restore --verbosity quiet *> $null
    $buildExit = $LASTEXITCODE
    $testExit = 0
    if ($buildExit -eq 0) {
        foreach ($testClass in $testClasses) {
            & $dotnet test $tests `
                --no-build `
                --no-restore `
                --filter "FullyQualifiedName~$testClass" `
                --logger "console;verbosity=quiet" *> $null
            if ($LASTEXITCODE -ne 0) {
                $testExit = $LASTEXITCODE
                break
            }
        }
    }
    $ErrorActionPreference = $previousPreference
    if ($buildExit -ne 0 -or $testExit -ne 0) {
        Stop-Guest "AccessGuestSourceBuildFailed"
    }
    Write-Output "access-parallels-build=completed;head=$head;buildPassed=true;accessTestsPassed=true;sourceClean=true"
    exit 0
}

$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
& $dotnet build $solution --no-restore --verbosity quiet *> $null
$syntheticBuildExit = $LASTEXITCODE
$ErrorActionPreference = $previousPreference
if ($syntheticBuildExit -ne 0) {
    Stop-Guest "AccessGuestSyntheticBuildFailed"
}

foreach ($required in @($accessCli, $traceMapCli, $generator, $harness)) {
    if (-not (Test-Path $required)) {
        Stop-Guest "AccessGuestSyntheticInputMissing"
    }
}

$runId = [Guid]::NewGuid().ToString("N")
$smokeRoot = Join-Path $GuestRoot "runs\$runId"
$checkpoint = Join-Path $GuestRoot "checkpoints\$runId.json"
$reviewBundle = Join-Path $GuestRoot "review-bundles\$runId"
$previousPreference = $ErrorActionPreference
$harnessExit = 1
$smokeCleanupFailed = $false
try {
    $ErrorActionPreference = "Continue"
    & $harness `
        -AccessCli $accessCli `
        -TraceMapCli $traceMapCli `
        -Generator $generator `
        -SmokeRoot $smokeRoot `
        -Phase9CheckpointPath $checkpoint `
        -ReviewBundlePath $reviewBundle *> $null
    $harnessExit = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousPreference
    try {
        if (Test-Path $smokeRoot) {
            Remove-Item $smokeRoot -Recurse -Force -ErrorAction Stop
        }
    }
    catch {
        $smokeCleanupFailed = $true
    }
}
if ($smokeCleanupFailed -or (Test-Path $smokeRoot)) {
    Stop-Guest "AccessGuestSyntheticCleanupFailed"
}
if ($harnessExit -ne 0) {
    Stop-Guest "AccessGuestSyntheticFailed"
}

$checkpointFiles = @(
    Get-ChildItem -Path "$(Split-Path -Parent $checkpoint)\$(Split-Path -Leaf $checkpoint).*" -File |
        Where-Object { $_.Name -match '\.\d+$' }
)
if ($checkpointFiles.Count -eq 0) {
    Stop-Guest "AccessGuestSyntheticCheckpointMissing"
}
$highest = $checkpointFiles |
    ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json } |
    Sort-Object checkpointSequence -Descending |
    Select-Object -First 1
if ($highest.phase9ConsumerContracts -ne "completed" -or
    -not $highest.localReviewBundleContractCorrect -or
    -not (Test-Path (Join-Path $reviewBundle "access-review-manifest.json"))) {
    Stop-Guest "AccessGuestSyntheticContractFailed"
}
if (Get-Process -Name "MSACCESS", "tracemap-access" -ErrorAction SilentlyContinue) {
    Stop-Guest "AccessGuestSyntheticProcessCleanupFailed"
}

$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$statusAfter = (& $git -C $repository status --porcelain 2>$null | Out-String)
$statusAfterExit = $LASTEXITCODE
$dirtyAfter = -not [string]::IsNullOrWhiteSpace($statusAfter)
$ErrorActionPreference = $previousPreference
if ($statusAfterExit -ne 0 -or $dirtyAfter) {
    Stop-Guest "AccessGuestSourceChanged"
}

Write-Output "access-parallels-synthetic=completed;head=$head;consumerContracts=completed;reviewBundleRetained=true;processCleanup=true;sourceClean=true"
