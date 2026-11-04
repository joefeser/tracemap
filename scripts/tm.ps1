Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'TraceMap launcher requires PowerShell 7.' }
# Keep this a simple script: advanced-script common parameters would consume
# TraceMap's --out as the ambiguous -OutVariable/-OutBuffer before forwarding it.
$Build = $false
$BuildConfiguration = 'Release'
$CommandArgs = @($args)
while ($CommandArgs.Count -gt 0) {
    if ($CommandArgs[0] -eq '-Build') {
        $Build = $true
        $CommandArgs = @($CommandArgs | Select-Object -Skip 1)
    } elseif ($CommandArgs[0] -eq '-BuildConfiguration') {
        if ($CommandArgs.Count -lt 2 -or $CommandArgs[1] -notin @('Debug', 'Release')) {
            throw '-BuildConfiguration requires Debug or Release.'
        }
        $BuildConfiguration = if ($CommandArgs[1] -ieq 'Debug') { 'Debug' } else { 'Release' }
        $CommandArgs = @($CommandArgs | Select-Object -Skip 2)
    } else { break }
}
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
$toolRoot = Join-Path $repo "artifacts/workspace-cli/$BuildConfiguration"
$cli = Join-Path $toolRoot 'tracemap.dll'
if ($Build) {
    # Publish chooses the project's target framework. Do not derive it from the installed SDK.
    # This builds TraceMap itself, never the repositories selected for collection.
    dotnet publish $project --configuration $BuildConfiguration --output $toolRoot --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (!(Test-Path -LiteralPath $cli -PathType Leaf)) {
    throw "TraceMap tool is not prepared. Run: pwsh -File `"$PSCommandPath`" -Build -BuildConfiguration $BuildConfiguration version"
}
dotnet $cli @CommandArgs
exit $LASTEXITCODE
