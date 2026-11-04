[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$OutputRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $root) { throw 'Choose a fresh synthetic replay folder; existing outputs are preserved.' }
$launcher = Join-Path (Split-Path $PSScriptRoot -Parent) 'tm.ps1'
[void][IO.Directory]::CreateDirectory($root)

function Invoke-Tool([string[]]$Command) {
    & pwsh -NoProfile -File $launcher @Command
    if ($LASTEXITCODE -ne 0) { throw "Workspace launcher replay failed: $($Command[0..1] -join ' ')" }
}

foreach ($name in @('web', 'framework')) {
    $source = Join-Path $root $name
    [void][IO.Directory]::CreateDirectory($source)
    [IO.File]::WriteAllText((Join-Path $source 'Sample.cs'), 'namespace Example; public class Sample { public void Save() {} }')
    if ($name -eq 'web') {
        [IO.File]::WriteAllText((Join-Path $source 'Web.config'), '<configuration />')
        [IO.File]::WriteAllText((Join-Path $source 'ExamplePage.aspx'), '<%@ Page Language="C#" CodeBehind="ExamplePage.aspx.cs" Inherits="Example.ExamplePage" %><asp:Button ID="Save" runat="server" OnClick="Save_Click" />')
        [IO.File]::WriteAllText((Join-Path $source 'ExamplePage.aspx.cs'), 'namespace Example; public class ExamplePage { protected void Save_Click(object sender, System.EventArgs e) {} }')
    }
    git -C $source init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic Git initialization failed.' }
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
[IO.File]::WriteAllText((Join-Path $workspace 'workspace.local.json'), ($configuration | ConvertTo-Json -Depth 8))
$run = Join-Path $workspace 'runs/first'
Invoke-Tool @('workspace', 'verify', '--root', $workspace)
Invoke-Tool @('workspace', 'collect', '--root', $workspace, '--out', $run)
$record = Get-Content -LiteralPath (Join-Path $run 'workspace-run.local.json') -Raw | ConvertFrom-Json
if ($record.outcome -ne 'partial' -or @($record.sources).Count -ne 2) { throw 'Expected two source-only scans with partial syntax coverage.' }
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
