[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$head = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[0-9a-f]{40}$') {
    throw "LocalDistributionGitAuthorityUnavailable"
}
if ((& git -C $repo status --porcelain --untracked-files=all).Count -ne 0) {
    throw "LocalDistributionSourceNotClean"
}

$ownsRoot = [string]::IsNullOrWhiteSpace($OutputDirectory)
$root = if ($ownsRoot) {
    Join-Path ([IO.Path]::GetTempPath()) ("tracemap-distribution-" + [Guid]::NewGuid().ToString("N"))
} else {
    [IO.Path]::GetFullPath($OutputDirectory)
}
if (Test-Path -LiteralPath $root) {
    throw "LocalDistributionOutputExists"
}
New-Item -ItemType Directory -Path $root | Out-Null

$project = Join-Path $repo "src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj"
$feed = Join-Path $root "feed"
$toolPath = Join-Path $root "tool"
$fixture = Join-Path $root "fixture"
$review = Join-Path $root "review"
$framework = Join-Path $root "framework-dependent"
$selfContained = Join-Path $root "self-contained"
$nugetConfig = Join-Path $root "NuGet.Config"
$versionOne = "0.1.0-probe.1"
$versionTwo = "0.1.0-probe.2"

New-Item -ItemType Directory -Path $feed, $fixture | Out-Null
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfig -Encoding utf8NoBOM

function Invoke-Checked {
    param([string]$File, [string[]]$Arguments, [string]$WorkingDirectory = $repo)
    Push-Location $WorkingDirectory
    try {
        & $File @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "LocalDistributionCommandFailed:$File"
        }
    } finally {
        Pop-Location
    }
}

function Get-DirectorySize([string]$Path) {
    [long]$total = 0
    Get-ChildItem -LiteralPath $Path -File -Recurse | ForEach-Object { $total += $_.Length }
    return $total
}

function Read-Version([string]$Executable, [string[]]$Prefix = @()) {
    $json = (& $Executable @Prefix version --json | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "LocalDistributionVersionFailed" }
    $value = $json | ConvertFrom-Json
    if ($value.schemaVersion -ne "tracemap-version.v1") { throw "LocalDistributionVersionSchemaMismatch" }
    if ($value.sourceState -ne "clean") { throw "LocalDistributionSourceStateMismatch" }
    return $value
}

function Get-RuntimeIdentifier {
    $architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    if ($IsWindows) { return "win-$architecture" }
    if ($IsMacOS) { return "osx-$architecture" }
    if ($IsLinux) { return "linux-$architecture" }
    throw "LocalDistributionHostUnsupported"
}

try {
    Invoke-Checked dotnet @("pack", $project, "--no-restore", "-c", $Configuration, "-o", $feed,
        "-p:TraceMapPackAsTool=true", "-p:TraceMapDistributionKind=dotnet-tool", "-p:Version=$versionOne",
        "-p:DebugSymbols=false", "-p:DebugType=None")
    Invoke-Checked dotnet @("pack", $project, "--no-restore", "-c", $Configuration, "-o", $feed,
        "-p:TraceMapPackAsTool=true", "-p:TraceMapDistributionKind=dotnet-tool", "-p:Version=$versionTwo",
        "-p:DebugSymbols=false", "-p:DebugType=None")

    $packages = @(Get-ChildItem -LiteralPath $feed -Filter "TraceMap.Tool.*.nupkg" | Sort-Object Name)
    if ($packages.Count -ne 2) { throw "LocalDistributionPackageCountMismatch" }
    foreach ($package in $packages) {
        if ($package.Length -gt 40MB) { throw "LocalDistributionPackageSizeExceeded" }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $names = @($archive.Entries | ForEach-Object FullName)
            if (-not ($names -contains "tools/net10.0/any/tracemap.dll")) { throw "LocalDistributionPackagePayloadMissing" }
            if ($names | Where-Object { $_ -match '(^|/)(\.git|obj|src|samples)/' -or $_ -match '\.pdb$' }) {
                throw "LocalDistributionPackageUnexpectedContent"
            }
        } finally {
            $archive.Dispose()
        }
    }

    Invoke-Checked dotnet @("tool", "install", "TraceMap.Tool", "--tool-path", $toolPath,
        "--version", $versionOne, "--configfile", $nugetConfig, "--no-cache")
    $tool = Join-Path $toolPath $(if ($IsWindows) { "tracemap.exe" } else { "tracemap" })
    $firstVersion = Read-Version $tool
    if ($firstVersion.distributionKind -ne "dotnet-tool") { throw "LocalDistributionKindMismatch" }

    Set-Content -LiteralPath (Join-Path $fixture "Fixture.cs") -Value "internal sealed class Fixture { }" -Encoding utf8NoBOM
    Invoke-Checked git @("init") $fixture
    Invoke-Checked git @("config", "user.email", "fixture@example.invalid") $fixture
    Invoke-Checked git @("config", "user.name", "TraceMap Fixture") $fixture
    Invoke-Checked git @("add", ".") $fixture
    Invoke-Checked git @("commit", "-m", "baseline") $fixture
    Invoke-Checked $tool @("local-review", "run", "--repo", $fixture, "--out", $review)
    foreach ($relative in @("local-review-result.json", "README.md", "scan/scan-manifest.json", "scan/facts.ndjson", "scan/index.sqlite", "scan/report.md", "scan/logs/analyzer.log")) {
        if (-not (Test-Path -LiteralPath (Join-Path $review $relative) -PathType Leaf)) {
            throw "LocalDistributionGuidedArtifactMissing"
        }
    }

    Invoke-Checked dotnet @("tool", "update", "TraceMap.Tool", "--tool-path", $toolPath,
        "--version", $versionTwo, "--configfile", $nugetConfig, "--no-cache")
    $secondVersion = Read-Version $tool
    if (-not $secondVersion.toolVersion.StartsWith($versionTwo, [StringComparison]::Ordinal)) {
        throw "LocalDistributionUpgradeMismatch"
    }
    Invoke-Checked dotnet @("tool", "uninstall", "TraceMap.Tool", "--tool-path", $toolPath)
    if (Test-Path -LiteralPath $tool) { throw "LocalDistributionUninstallFailed" }

    Invoke-Checked dotnet @("publish", $project, "--no-restore", "-c", $Configuration, "-o", $framework,
        "--self-contained", "false", "-p:TraceMapDistributionKind=framework-dependent-archive", "-p:Version=$versionTwo",
        "-p:DebugSymbols=false", "-p:DebugType=None")
    $frameworkVersion = Read-Version dotnet @((Join-Path $framework "tracemap.dll"))
    if ($frameworkVersion.distributionKind -ne "framework-dependent-archive") { throw "LocalDistributionFrameworkKindMismatch" }

    $rid = Get-RuntimeIdentifier
    Invoke-Checked dotnet @("publish", $project, "-c", $Configuration, "-o", $selfContained,
        "-r", $rid, "--self-contained", "true", "-p:TraceMapDistributionKind=self-contained-archive", "-p:Version=$versionTwo",
        "-p:DebugSymbols=false", "-p:DebugType=None")
    $selfTool = Join-Path $selfContained $(if ($IsWindows) { "tracemap.exe" } else { "tracemap" })
    $selfVersion = Read-Version $selfTool
    if ($selfVersion.distributionKind -ne "self-contained-archive") { throw "LocalDistributionSelfContainedKindMismatch" }

    $result = [ordered]@{
        schemaVersion = "local-distribution-smoke.v1"
        head = $head
        host = [ordered]@{
            operatingSystem = $firstVersion.host.operatingSystem
            architecture = $firstVersion.host.architecture
            runtimeVersion = $firstVersion.host.runtimeVersion
            runtimeIdentifier = $rid
        }
        selectedCandidate = "dotnet-tool"
        candidates = @(
            [ordered]@{ kind = "dotnet-tool"; status = "supported"; lifecycle = "install-upgrade-run-uninstall"; bytes = [long]$packages[-1].Length; sha256 = (Get-FileHash $packages[-1].FullName -Algorithm SHA256).Hash.ToLowerInvariant() },
            [ordered]@{ kind = "framework-dependent-archive"; status = "supported"; lifecycle = "extract-run-remove-directory"; bytes = Get-DirectorySize $framework },
            [ordered]@{ kind = "self-contained-archive"; status = "supported"; lifecycle = "extract-run-remove-directory"; bytes = Get-DirectorySize $selfContained },
            [ordered]@{ kind = "offline-container"; status = "not-selected"; lifecycle = "not-run"; limitation = "Container publication, base-image provenance, signing, and per-platform image validation remain separately gated." }
        )
        checks = [ordered]@{
            packageContent = "passed"
            packageSizeBudget = "passed"
            outsideCheckout = "passed"
            guidedWorkflow = "passed"
            upgrade = "passed"
            uninstall = "passed"
        }
        limitations = @(
            "Hashes establish byte integrity only, not publisher identity, authenticity, or trust.",
            "This synthetic package smoke does not prove application runtime behavior, complete scan coverage, or release approval.",
            "The NuGet container is assigned a per-build hash; deterministic payload evidence does not claim byte-identical outer packages."
        )
    }
    $resultPath = Join-Path $root "local-distribution-smoke.json"
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8NoBOM
    Write-Output "local-distribution-smoke=completed;head=$head;host=$($firstVersion.host.operatingSystem)-$($firstVersion.host.architecture);result=$resultPath"
} finally {
    if ($ownsRoot -and (Test-Path -LiteralPath $root)) {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}
