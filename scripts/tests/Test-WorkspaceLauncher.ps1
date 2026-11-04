[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$OutputRoot, [switch]$WithCompiled)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $root) {
    throw "Existing replay preserved. Inspect '$root/workspace/runs/first/START-HERE.md', or choose a fresh OutputRoot to rerun."
}
$launcher = Join-Path (Split-Path $PSScriptRoot -Parent) 'tm.ps1'
[void][IO.Directory]::CreateDirectory($root)

function Invoke-Tool([string[]]$Command) {
    & pwsh -NoProfile -File $launcher @Command
    if ($LASTEXITCODE -ne 0) { throw "Workspace launcher replay failed: $($Command[0..1] -join ' ')" }
}

$targetFramework = $null
if ($WithCompiled) {
    $toolProject = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
    $targetFramework = (dotnet msbuild $toolProject -nologo -getProperty:TargetFramework | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $targetFramework -notmatch '^net[0-9]+\.[0-9]+$') { throw 'Could not resolve the tool project target framework for the synthetic fixture.' }
}

foreach ($name in @('web', 'framework')) {
    $source = Join-Path $root $name
    [void][IO.Directory]::CreateDirectory($source)
    [IO.File]::WriteAllText((Join-Path $source 'Sample.cs'), 'namespace Example; public class Sample { public void Save() {} }')
    if ($WithCompiled) {
        [IO.File]::WriteAllText((Join-Path $source '.gitignore'), "bin/`nobj/`n")
        if ($name -eq 'framework') {
            [IO.File]::WriteAllText((Join-Path $source 'GenericFramework.csproj'), "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>$targetFramework</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>")
            [IO.File]::WriteAllText((Join-Path $source 'FrameworkService.cs'), 'namespace Example; public sealed class FrameworkService { public int Save() => 1; }')
        } else {
            [IO.File]::WriteAllText((Join-Path $source 'SyntheticWeb.csproj'), "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>$targetFramework</TargetFramework><Nullable>enable</Nullable><DebugType>portable</DebugType></PropertyGroup><ItemGroup><ProjectReference Include=`"../framework/GenericFramework.csproj`" /></ItemGroup></Project>")
        }
    }
    if ($name -eq 'web') {
        [IO.File]::WriteAllText((Join-Path $source 'Web.config'), '<configuration />')
        [IO.File]::WriteAllText((Join-Path $source 'ExamplePage.aspx'), '<%@ Page Language="C#" CodeBehind="ExamplePage.aspx.cs" Inherits="Example.ExamplePage" %><asp:Button ID="Save" runat="server" OnClick="Save_Click" />')
        [IO.File]::WriteAllText((Join-Path $source 'ExamplePage.aspx.cs'), 'namespace Example; public class ExamplePage { protected void Save_Click(object sender, System.EventArgs e) {} }')
        if ($WithCompiled) {
            [IO.File]::WriteAllText((Join-Path $source 'ExamplePage.aspx.cs'), 'namespace Example; public class ExamplePage { public int Result { get; private set; } protected void Save_Click(object sender, System.EventArgs e) { Result = new FrameworkService().Save(); } }')
        }
    }
    git -C $source init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic Git initialization failed.' }
    git -C $source config core.autocrlf false
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic Git line-ending setup failed.' }
    if ($WithCompiled) {
        git -C $source remote add origin "https://example.invalid/synthetic-$name.git"
        if ($LASTEXITCODE -ne 0) { throw 'Synthetic repository identity setup failed.' }
    }
    git -C $source add .
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic Git staging failed.' }
    git -C $source -c user.name='Synthetic Fixture' -c user.email=fixture@example.invalid -c commit.gpgsign=false commit --quiet -m 'synthetic source collection fixture'
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic Git commit failed.' }
}

$workspace = Join-Path $root 'workspace'
Invoke-Tool @('workspace', 'init', '--root', $workspace)
$configuration = [ordered]@{
    schemaVersion = 'tracemap-workspace.v1'
    repositories = @(
        [ordered]@{ id = 'web'; path = (Join-Path $root 'web'); projects = @() },
        [ordered]@{ id = 'framework'; path = (Join-Path $root 'framework'); projects = @() }
    )
    scopeFile = $null
    webFormsReport = $true
}
if ($WithCompiled) {
    $web = Join-Path $root 'web'
    $published = Join-Path $root 'published'
    [void][IO.Directory]::CreateDirectory($published)
    # Explicitly build only the synthetic sources authored above. This is not IIS/ASP.NET publication acceptance.
    dotnet publish (Join-Path $web 'SyntheticWeb.csproj') --configuration Release --output (Join-Path $published 'bin') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic managed publication failed.' }
    Copy-Item -LiteralPath (Join-Path $web 'ExamplePage.aspx') -Destination $published
    Copy-Item -LiteralPath (Join-Path $web 'Web.config') -Destination $published
    [IO.File]::WriteAllText((Join-Path $published 'ExamplePage.aspx.compiled'), '<preserve virtualPath="/ExamplePage.aspx" assembly="SyntheticWeb" type="Example.ExamplePage" />')
    $sourceCommit = (git -C $web rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic source commit unavailable.' }
    $native = [ordered]@{
        schemaVersion = 'webforms-compiled-review-config.v1'; operation = 'fresh'
        sourceRoot = $web; sourceCommitSha = $sourceCommit
        projectMode = 'projects'; solutionRelativePath = $null; projectRelativePaths = @('SyntheticWeb.csproj')
        sourceFolders = @('.'); pageMode = 'selected'; pageRelativePaths = @('ExamplePage.aspx')
        publishedRoot = $published; primaryAssemblies = @('bin/SyntheticWeb.dll'); dependencyAssemblies = @('bin/GenericFramework.dll')
        bindingReceipts = @(); pdbInputs = @('bin/SyntheticWeb.pdb'); pageMaps = @('ExamplePage.aspx.compiled')
        parentScanRoot = $null; budgets = @{}
        publishSourceRelativePaths = @('ExamplePage.aspx.cs', 'SyntheticWeb.csproj', 'Web.config')
    }
    $nativePath = Join-Path $workspace 'native-input.local.json'
    [IO.File]::WriteAllText($nativePath, ($native | ConvertTo-Json -Depth 8))
    $evidence = Join-Path $workspace 'native-evidence'
    # Explicit correspondence declaration applies only to this freshly built synthetic fixture.
    Invoke-Tool @('webforms-review', 'prepare', '--config', $nativePath, '--out', $evidence, '--attest-exact-source-commit', $sourceCommit)
    $configuration.schemaVersion = 'tracemap-workspace.v2'
    $configuration['compiledReview'] = [ordered]@{ repositoryId = 'web'; nativeConfigPath = (Join-Path $evidence 'review-config.local.json') }
    $configuration.repositories[1].projects = @('GenericFramework.csproj')
}
[IO.File]::WriteAllText((Join-Path $workspace 'workspace.local.json'), ($configuration | ConvertTo-Json -Depth 8))
$run = Join-Path $workspace 'runs/first'
Invoke-Tool @('workspace', 'verify', '--root', $workspace)
Invoke-Tool @('workspace', 'collect', '--root', $workspace, '--out', $run)
$record = Get-Content -LiteralPath (Join-Path $run 'workspace-run.local.json') -Raw | ConvertFrom-Json
if ($record.outcome -ne 'partial' -or @($record.sources).Count -ne 2) { throw 'Expected two source collections with explicit partial coverage.' }
if ($WithCompiled) {
    if ($record.schemaVersion -ne 'tracemap-workspace-run.v2' -or $record.compiledReview.repositoryId -ne 'web') { throw 'Compiled review record missing.' }
    if (!(Test-Path -LiteralPath (Join-Path $run ($record.compiledReview.rawScan + '/index.sqlite')) -PathType Leaf)) { throw 'Native raw scan missing.' }
    if (!(Test-Path -LiteralPath (Join-Path $run ($record.compiledReview.reportBundle + '/index.html')) -PathType Leaf)) { throw 'Native workbench missing.' }
}
foreach ($path in @('START-HERE.md', 'report.md', 'combined.sqlite', 'webforms/webforms-modernization.md')) {
    if (!(Test-Path -LiteralPath (Join-Path $run $path) -PathType Leaf)) { throw "Missing replay output: $path" }
}
$scope = Join-Path $workspace 'repositories.txt'
[IO.File]::WriteAllText($scope, "# Restrict collection to web`nweb`n")
$filtered = Join-Path $workspace 'runs/web-only'
Invoke-Tool @('workspace', 'verify', '--root', $workspace, '--scope', $scope)
Invoke-Tool @('workspace', 'collect', '--root', $workspace, '--scope', $scope, '--out', $filtered)
$selected = Get-Content -LiteralPath (Join-Path $filtered 'workspace-run.local.json') -Raw | ConvertFrom-Json
if (@($selected.sources).Count -ne 1 -or $selected.sources[0].id -ne 'web') { throw 'Repository filter did not restrict collection.' }
Write-Host "Workspace launcher replay passed. Open: $(Join-Path $run 'START-HERE.md')"
