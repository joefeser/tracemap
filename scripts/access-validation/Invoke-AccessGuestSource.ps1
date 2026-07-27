param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("doctor", "build", "synthetic")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{40}$")]
    [string]$ExpectedHead,

    [string]$GuestRoot = "C:\TraceMapDev",

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{64}$")]
    [string]$ExpectedGitSha256,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{64}$")]
    [string]$ExpectedDotnetSha256
)

$ErrorActionPreference = "Stop"

function Stop-Guest([string]$Classification) {
    throw $Classification
}

function Test-TrustedPath(
    [string]$Path,
    [string]$Boundary,
    [bool]$RequireLeaf = $false
) {
    try {
        $fullPath = [IO.Path]::GetFullPath($Path)
        $fullBoundary = [IO.Path]::GetFullPath($Boundary).TrimEnd([char]92)
        if ($fullPath -ne $fullBoundary -and
            -not $fullPath.StartsWith(
                "$fullBoundary\",
                [StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }

        $current = Get-Item -LiteralPath $fullPath -Force -ErrorAction Stop
        if ($RequireLeaf -and $current.PSIsContainer) {
            return $false
        }
        while ($null -ne $current) {
            if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                return $false
            }
            if ($current.FullName.TrimEnd([char]92).Equals(
                    $fullBoundary,
                    [StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
            if ($current.PSIsContainer) {
                $current = $current.Parent
            }
            else {
                $current = $current.Directory
            }
        }
    }
    catch {
        return $false
    }
    return $false
}

function Get-SourceIdentity([string]$Git, [string]$Repository) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $head = (& $Git -C $Repository rev-parse HEAD 2>$null | Out-String).Trim()
    $headExit = $LASTEXITCODE
    $status = (& $Git -C $Repository status --porcelain 2>$null | Out-String)
    $statusExit = $LASTEXITCODE
    $remotes = (& $Git -C $Repository remote 2>$null | Out-String).Trim()
    $remoteExit = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference
    return [pscustomobject]@{
        Head = $head
        HeadExit = $headExit
        StatusExit = $statusExit
        Dirty = -not [string]::IsNullOrWhiteSpace($status)
        RemoteExit = $remoteExit
        Remotes = $remotes
    }
}

function Test-ExpectedIdentity($Identity, [string]$ExpectedHead) {
    return $Identity.HeadExit -eq 0 -and
        $Identity.StatusExit -eq 0 -and
        $Identity.RemoteExit -eq 0 -and
        $Identity.Head -eq $ExpectedHead -and
        -not $Identity.Dirty -and
        [string]::IsNullOrWhiteSpace($Identity.Remotes)
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

if (-not (Test-TrustedPath $repository $GuestRoot)) {
    Stop-Guest "AccessGuestSourceInputMissing"
}
foreach ($required in @($git, $dotnet, $solution, $tests, $generator, $harness)) {
    if (-not (Test-TrustedPath $required $GuestRoot $true)) {
        Stop-Guest "AccessGuestSourceInputMissing"
    }
}
if ((Get-FileHash -LiteralPath $git -Algorithm SHA256).Hash.ToLowerInvariant() -ne
        $ExpectedGitSha256 -or
    (Get-FileHash -LiteralPath $dotnet -Algorithm SHA256).Hash.ToLowerInvariant() -ne
        $ExpectedDotnetSha256) {
    Stop-Guest "AccessGuestToolchainIdentityMismatch"
}

$identity = Get-SourceIdentity $git $repository
if (-not (Test-ExpectedIdentity $identity $ExpectedHead)) {
    Stop-Guest "AccessGuestSourceIdentityMismatch"
}
$head = $identity.Head

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

    $identityAfterBuild = Get-SourceIdentity $git $repository
    if (-not (Test-ExpectedIdentity $identityAfterBuild $ExpectedHead)) {
        Stop-Guest "AccessGuestSourceChanged"
    }

    Write-Output "access-parallels-build=completed;head=$head;buildPassed=true;accessTestsPassed=true;sourceClean=true"
    exit 0
}

$cliOutputDirectories = @(
    Split-Path -Parent $accessCli
    Split-Path -Parent $traceMapCli
)
foreach ($outputDirectory in $cliOutputDirectories) {
    if (Test-Path -LiteralPath $outputDirectory) {
        if (-not (Test-TrustedPath $outputDirectory $GuestRoot)) {
            Stop-Guest "AccessGuestSyntheticInputMissing"
        }
        Remove-Item $outputDirectory -Recurse -Force -ErrorAction Stop
    }
}

$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
& $dotnet build $solution --no-restore --verbosity quiet *> $null
$syntheticBuildExit = $LASTEXITCODE
$ErrorActionPreference = $previousPreference
if ($syntheticBuildExit -ne 0) {
    Stop-Guest "AccessGuestSyntheticBuildFailed"
}
$identityAfterSyntheticBuild = Get-SourceIdentity $git $repository
if (-not (Test-ExpectedIdentity $identityAfterSyntheticBuild $ExpectedHead)) {
    Stop-Guest "AccessGuestSourceChanged"
}

foreach ($required in @($accessCli, $traceMapCli)) {
    if (-not (Test-TrustedPath $required $GuestRoot $true)) {
        Stop-Guest "AccessGuestSyntheticInputMissing"
    }
}
$accessCliHash = (Get-FileHash -LiteralPath $accessCli -Algorithm SHA256).Hash
$traceMapCliHash = (Get-FileHash -LiteralPath $traceMapCli -Algorithm SHA256).Hash

$runId = [Guid]::NewGuid().ToString("N")
$smokeRoot = Join-Path $GuestRoot "runs\$runId"
$checkpoint = Join-Path $GuestRoot "checkpoints\$runId.json"
$reviewBundle = Join-Path $GuestRoot "review-bundles\$runId"
$syntheticSucceeded = $false
try {
    $previousPreference = $ErrorActionPreference
    $harnessExit = 1
    $smokeCleanupFailed = $false
    try {
        $ErrorActionPreference = "Continue"
        try {
            & $harness `
                -AccessCli $accessCli `
                -TraceMapCli $traceMapCli `
                -Generator $generator `
                -SmokeRoot $smokeRoot `
                -Phase9CheckpointPath $checkpoint `
                -ReviewBundlePath $reviewBundle *> $null
            $harnessExit = 0
        }
        catch {
            $harnessExit = 1
        }
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
    if ((Get-FileHash -LiteralPath $accessCli -Algorithm SHA256).Hash -ne
            $accessCliHash -or
        (Get-FileHash -LiteralPath $traceMapCli -Algorithm SHA256).Hash -ne
            $traceMapCliHash) {
        Stop-Guest "AccessGuestSyntheticExecutableChanged"
    }

    $identityAfterSynthetic = Get-SourceIdentity $git $repository
    if (-not (Test-ExpectedIdentity $identityAfterSynthetic $ExpectedHead)) {
        Stop-Guest "AccessGuestSourceChanged"
    }

    $syntheticSucceeded = $true
}
finally {
    if (-not $syntheticSucceeded) {
        try {
            if (Test-Path $reviewBundle) {
                Remove-Item $reviewBundle -Recurse -Force -ErrorAction Stop
            }
            Get-ChildItem `
                -Path "$(Split-Path -Parent $checkpoint)\$(Split-Path -Leaf $checkpoint).*" `
                -File `
                -ErrorAction SilentlyContinue |
                Remove-Item -Force -ErrorAction Stop
        }
        catch {
            Stop-Guest "AccessGuestSyntheticCleanupFailed"
        }
    }
}

Write-Output "access-parallels-synthetic=completed;head=$head;consumerContracts=completed;reviewBundleRetained=true;processCleanup=true;sourceClean=true"
exit 0
