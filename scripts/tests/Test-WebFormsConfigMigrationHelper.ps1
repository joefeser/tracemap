$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wmigrate.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-migration-helper-test-' + [Guid]::NewGuid().ToString('N'))
$root = Join-Path $temp 'review'
[void][IO.Directory]::CreateDirectory((Join-Path $root 'config'))
[IO.File]::WriteAllText((Join-Path $root 'config/webforms-review.jsonc'), '{}')
$global:migrationHelperAnswers = [Collections.Generic.Queue[string]]::new()
$global:migrationHelperPrompts = 0
$global:migrationHelperCalls = 0
function Read-Host { param($Prompt) $global:migrationHelperPrompts++; return $global:migrationHelperAnswers.Dequeue() }
function dotnet { $global:migrationHelperCalls++; $global:LASTEXITCODE = 0 }
function Expect-Failure($Action, $Code) {
    try { & $Action; throw 'Expected failure was not raised' }
    catch { if (!$_.Exception.Message.StartsWith($Code)) { throw } }
}
try {
    # A no-argument invocation must prompt even if the caller has a same-named variable.
    $ReviewRoots = @('not-a-user-selection')
    $global:migrationHelperAnswers.Enqueue('"' + $root + '"')
    $global:migrationHelperAnswers.Enqueue('')
    & $helper
    if ($global:migrationHelperPrompts -ne 2 -or $global:migrationHelperCalls -ne 2) { throw 'No-argument prompt/build/conversion regression' }
    $global:migrationHelperCalls = 0
    $global:migrationHelperAnswers.Enqueue('')
    Expect-Failure { & $helper } 'WEBFORMS_CONFIG_MIGRATION_REVIEW_ROOT_REQUIRED'
    if ($global:migrationHelperCalls -ne 0) { throw 'Blank input launched dotnet' }
    Expect-Failure { & $helper -ReviewRoots @($temp) } 'WEBFORMS_CONFIG_MIGRATION_CONFIG_SELECTION'
    if ($global:migrationHelperCalls -ne 0) { throw 'Missing config launched dotnet' }
    [IO.File]::WriteAllText((Join-Path $root 'config/webforms-review.json'), '{}')
    Expect-Failure { & $helper -ReviewRoots @($root) } 'WEBFORMS_CONFIG_MIGRATION_CONFIG_SELECTION'
    if ($global:migrationHelperCalls -ne 0) { throw 'Ambiguous config launched dotnet' }
    if (Test-Path -LiteralPath (Join-Path $root 'native-config')) { throw 'Validation changed outputs' }
    Write-Output 'webFormsConfigMigrationHelperPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
