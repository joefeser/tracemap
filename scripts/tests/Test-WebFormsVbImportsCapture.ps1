Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$capture = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Capture-WebFormsVbImports.ps1'))
$desktop = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
if (!(Test-Path -LiteralPath $desktop)) { throw 'VB_IMPORT_CAPTURE_TEST_REQUIRES_WINDOWS_FRAMEWORK_POWERSHELL' }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('vb-import-capture-test-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporary) | Out-Null
function Hash-Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Assert-True([bool]$Condition, [string]$Code) { if (!$Condition) { throw $Code } }
function Set-PublicConfig([string]$Xml) { [IO.File]::WriteAllText((Join-Path $script:site 'Web.config'), $Xml) }
function Invoke-Capture([string]$Name, [bool]$Success, [string]$ExpectedError = '') {
    $output = Join-Path $temporary ($Name + '.json')
    $lines = @(& $desktop -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $capture -SourceRoot $script:site -Out $output 2>&1)
    $exitCode = $LASTEXITCODE
    if ($Success) {
        Assert-True ($exitCode -eq 0) ('CAPTURE_FAILED_' + $Name + ': ' + ($lines -join ' '))
        $receipt = Get-Content -Raw -LiteralPath $output | ConvertFrom-Json
        Assert-True ($receipt.schemaVersion -ceq 'webforms-vb-import-context.v1') 'SCHEMA'
        Assert-True ($receipt.visibility -ceq 'local-only') 'VISIBILITY'
        Assert-True ($receipt.generatorSha256 -ceq (Hash-Bytes ([IO.File]::ReadAllBytes($capture)))) 'GENERATOR_HASH'
        Assert-True ($receipt.boundedInputSha256 -ceq (Hash-Bytes ([Text.Encoding]::UTF8.GetBytes($receipt.payloadJson)))) 'INPUT_HASH'
        $payload = $receipt.payloadJson | ConvertFrom-Json
        Assert-True ($payload.evidenceKind -ceq 'current-environment-not-build-proof') 'CLAIM_BOUNDARY'
        Assert-True ($payload.mappingKind -ceq 'standalone-root') 'MAPPING_SCOPE'
        Assert-True ($payload.providerType -ceq 'Microsoft.VisualBasic.VBCodeProvider') 'PROVIDER'
        Assert-True ($payload.runtimeVersion.StartsWith('4.')) 'RUNTIME'
        Assert-True ($payload.processArchitecture -cin @('x86', 'x64')) 'ARCHITECTURE'
        Assert-True ($payload.sourceFiles.Count -eq 1 -and $payload.sourceFiles[0].path -ceq 'App_Code/PublicHelper.vb') 'SOURCE_SCOPE'
        Assert-True ($payload.sourceRootPathSha256 -ceq (Hash-Bytes ([Text.Encoding]::UTF8.GetBytes($script:site.TrimEnd([char[]]'\/').Replace('\', '/').ToUpperInvariant())))) 'ROOT_HASH'
        foreach ($row in $payload.configurationFiles) {
            Assert-True ([IO.Path]::IsPathRooted($row.path) -and $row.sha256 -ceq (Hash-Bytes ([IO.File]::ReadAllBytes($row.path)))) 'CONFIG_HASH'
        }
        Assert-True ($payload.providerAssemblySha256 -ceq (Hash-Bytes ([IO.File]::ReadAllBytes($payload.providerAssemblyPath)))) 'PROVIDER_HASH'
        return $payload
    }
    Assert-True ($exitCode -ne 0 -and !(Test-Path -LiteralPath $output)) ('EXPECTED_REFUSAL_' + $Name)
    if ($ExpectedError) { Assert-True (($lines -join ' ') -match [regex]::Escape($ExpectedError)) ('WRONG_REFUSAL_' + $Name) }
    Assert-True (($lines -join ' ') -notmatch 'Public\.Unavailable|Public\.Secret|Public\.CustomProvider') 'ERROR_DISCLOSURE'
}
try {
    $script:site = Join-Path $temporary 'site'
    [IO.Directory]::CreateDirectory((Join-Path $site 'App_Code')) | Out-Null
    [IO.File]::WriteAllText((Join-Path $site 'App_Code/PublicHelper.vb'), 'Public Class PublicHelper' + "`n" + 'End Class')
    & git -C $site init --quiet
    & git -C $site -c user.name=Public -c user.email=public@example.invalid add App_Code
    & git -C $site -c user.name=Public -c user.email=public@example.invalid commit --quiet -m 'Public synthetic source'
    if ($LASTEXITCODE -ne 0) { throw 'SYNTHETIC_COMMIT_FAILED' }
    $originalSourceHash = Hash-Bytes ([IO.File]::ReadAllBytes((Join-Path $site 'App_Code/PublicHelper.vb')))
    Set-PublicConfig '<configuration />'
    $inherited = Invoke-Capture 'inherited' $true
    foreach ($name in @('System', 'System.Web.UI', 'System.Collections.Generic', 'Microsoft.VisualBasic')) { Assert-True ($name -cin $inherited.namespaces) 'INHERITED_IMPORT_MISSING' }
    $inheritedAgain = Invoke-Capture 'inherited-again' $true
    Assert-True ((($inherited | ConvertTo-Json -Depth 12 -Compress)) -ceq ($inheritedAgain | ConvertTo-Json -Depth 12 -Compress)) 'NONDETERMINISTIC'
    $configHash = Hash-Bytes ([IO.File]::ReadAllBytes((Join-Path $site 'Web.config')))
    Assert-True ($inherited.configurationFiles.Count -eq 3) 'CONFIGURATION_CHAIN'
    Assert-True ((Hash-Bytes ([IO.File]::ReadAllBytes((Join-Path $site 'Web.config')))) -ceq $configHash) 'CONFIG_CHANGED'
    Remove-Item -LiteralPath (Join-Path $site 'Web.config')
    $withoutLocal = Invoke-Capture 'without-local-config' $true
    Assert-True ($withoutLocal.configurationFiles.Count -eq 2 -and 'System.Web.UI' -cin $withoutLocal.namespaces) 'NO_LOCAL_CONFIG_INHERITANCE'
    Set-PublicConfig '<configuration><system.web><pages><namespaces autoImportVBNamespace="false"><clear/><add namespace="Public.Captured"/></namespaces></pages></system.web></configuration>'
    $cleared = Invoke-Capture 'clear' $true
    Assert-True ($cleared.namespaces.Count -eq 1 -and $cleared.namespaces[0] -ceq 'Public.Captured') 'CLEAR_OR_AUTO_IMPORT'
    Set-PublicConfig '<configuration><system.web><pages><namespaces><remove namespace="System.Web.UI"/><add namespace="Public.Captured"/></namespaces></pages></system.web></configuration>'
    $removed = Invoke-Capture 'remove' $true
    Assert-True ('System.Web.UI' -cnotin $removed.namespaces -and 'Public.Captured' -cin $removed.namespaces) 'REMOVE'
    Set-PublicConfig '<configuration><system.codedom><compilers><compiler language="vb;vbs;visualbasic;vbscript" extension=".vb" type="Microsoft.VisualBasic.VBCodeProvider, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" compilerOptions="/imports:Public.Compiler,System.Data"/></compilers></system.codedom></configuration>'
    $compiler = Invoke-Capture 'compiler-imports' $true
    Assert-True ('Public.Compiler' -cin $compiler.namespaces -and 'System.Data' -cin $compiler.namespaces) 'COMPILER_IMPORTS'
    Set-PublicConfig '<configuration><system.codedom><compilers><compiler language="vb;vbs;visualbasic;vbscript" extension=".vb" type="Public.CustomProvider, Public.Unavailable"/></compilers></system.codedom></configuration>'
    Invoke-Capture 'custom-provider' $false 'VB_IMPORT_CAPTURE_CUSTOM_PROVIDER_REFUSED'
    Set-PublicConfig '<configuration><system.codedom><compilers><compiler language="vb" extension=".vb" type="Microsoft.VisualBasic.VBCodeProvider, System"/></compilers></system.codedom></configuration>'
    Invoke-Capture 'ambiguous-aliases' $false 'VB_IMPORT_CAPTURE_PROVIDER_AMBIGUOUS'
    Set-PublicConfig '<configuration><configSections><sectionGroup name="system.web"><section name="pages" type="Public.CustomSection, Public.Unavailable"/></sectionGroup></configSections></configuration>'
    Invoke-Capture 'section-handler' $false 'VB_IMPORT_CAPTURE_SECTION_REDEFINITION_REFUSED'
    Set-PublicConfig '<configuration><location path="Public"><system.web><pages><namespaces><add namespace="Public.Secret"/></namespaces></pages></system.web></location></configuration>'
    Invoke-Capture 'location' $false 'VB_IMPORT_CAPTURE_LOCATION_REFUSED'
    Set-PublicConfig '<configuration><system.web><pages configSource="Public.Unavailable"/></system.web></configuration>'
    Invoke-Capture 'external' $false 'VB_IMPORT_CAPTURE_EXTERNAL_CONFIGURATION_REFUSED'
    Set-PublicConfig '<configuration><system.codedom><compilers><compiler language="vb;vbs;visualbasic;vbscript" extension=".vb" type="Microsoft.VisualBasic.VBCodeProvider, System" compilerOptions="/imports:Alias=Public.Secret"/></compilers></system.codedom></configuration>'
    Invoke-Capture 'compiler-alias' $false 'VB_IMPORT_CAPTURE_COMPILER_OPTIONS_REFUSED'
    Set-PublicConfig '<configuration><system.codedom><compilers><compiler language="vb;vbs;visualbasic;vbscript" extension=".vb" type="Microsoft.VisualBasic.VBCodeProvider, System" compilerOptions="@Public.Secret"/></compilers></system.codedom></configuration>'
    Invoke-Capture 'compiler-response' $false 'VB_IMPORT_CAPTURE_COMPILER_OPTIONS_REFUSED'
    Set-PublicConfig '<configuration><system.codedom><compilers><compiler language="vb;vbs;visualbasic;vbscript" extension=".vb" type="Microsoft.VisualBasic.VBCodeProvider, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0000000000000000"/></compilers></system.codedom></configuration>'
    Invoke-Capture 'provider-token' $false 'VB_IMPORT_CAPTURE_CUSTOM_PROVIDER_REFUSED'
    Set-PublicConfig '<configuration><system.web><compilation><compilers><compiler language="vb" extension=".vb" type="Public.CustomProvider, Public.Unavailable"/></compilers></compilation></system.web></configuration>'
    Invoke-Capture 'legacy-provider' $false 'VB_IMPORT_CAPTURE_LEGACY_COMPILER_OVERRIDE_REFUSED'
    Set-PublicConfig ('<configuration>' + (' ' * (2MB)) + '</configuration>')
    Invoke-Capture 'configuration-bound' $false 'VB_IMPORT_CAPTURE_FILE_BOUND'
    $many = (0..128 | ForEach-Object { '<add namespace="Public.N' + $_ + '"/>' }) -join ''
    Set-PublicConfig ('<configuration><system.web><pages><namespaces><clear/>' + $many + '</namespaces></pages></system.web></configuration>')
    Invoke-Capture 'namespace-bound' $false 'VB_IMPORT_CAPTURE_NAMESPACE_BOUND'
    Set-PublicConfig '<configuration />'
    $nested = Join-Path $site 'App_Code/Web.config'
    [IO.File]::WriteAllText($nested, '<configuration/>')
    Invoke-Capture 'nested-config' $false 'VB_IMPORT_CAPTURE_NESTED_CONFIGURATION_REFUSED'
    Remove-Item -LiteralPath $nested
    $extraSource = Join-Path $site 'App_Code/NotSelected.vb'
    [IO.File]::WriteAllText($extraSource, 'Public Class NotSelected' + "`n" + 'End Class')
    $subsetOutput = Join-Path $temporary 'subset.json'
    $null = & $desktop -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $capture -SourceRoot $site -Out $subsetOutput -SourceFile 'App_Code/PublicHelper.vb' 2>&1
    Assert-True ($LASTEXITCODE -eq 0) 'SUBSET_CAPTURE_FAILED'
    $subsetReceipt = Get-Content -Raw -LiteralPath $subsetOutput | ConvertFrom-Json
    $subsetPayload = $subsetReceipt.payloadJson | ConvertFrom-Json
    Assert-True ($subsetPayload.sourceFiles.Count -eq 1 -and $subsetPayload.sourceFiles[0].path -ceq 'App_Code/PublicHelper.vb') 'SUBSET_SCOPE'
    $escapeOutput = Join-Path $temporary 'escape.json'
    $null = & $desktop -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $capture -SourceRoot $site -Out $escapeOutput -SourceFile 'App_Code/../PublicHelper.vb' 2>&1
    Assert-True ($LASTEXITCODE -ne 0 -and !(Test-Path -LiteralPath $escapeOutput)) 'SOURCE_ESCAPE_ADMITTED'
    Remove-Item -LiteralPath $extraSource
    $linkedTarget = Join-Path $temporary 'public-linked-target'
    [IO.Directory]::CreateDirectory($linkedTarget) | Out-Null
    $linkedDirectory = Join-Path $site 'App_Code/Linked'
    New-Item -ItemType Junction -Path $linkedDirectory -Target $linkedTarget | Out-Null
    Invoke-Capture 'junction' $false 'VB_IMPORT_CAPTURE_REPARSE_REFUSED'
    Remove-Item -LiteralPath $linkedDirectory -Force
    $tooMany = @()
    foreach ($number in 0..255) {
        $path = Join-Path $site ('App_Code/Extra' + $number + '.vb')
        [IO.File]::WriteAllText($path, '')
        $tooMany += $path
    }
    Invoke-Capture 'source-count' $false 'VB_IMPORT_CAPTURE_SOURCE_BOUND'
    foreach ($path in $tooMany) { Remove-Item -LiteralPath $path }
    $large = Join-Path $site 'App_Code/Large.vb'
    $largeStream = [IO.File]::Create($large)
    try { $largeStream.SetLength(64MB) } finally { $largeStream.Dispose() }
    Invoke-Capture 'aggregate-source-bytes' $false 'VB_IMPORT_CAPTURE_SOURCE_BOUND'
    Remove-Item -LiteralPath $large
    $existing = Join-Path $temporary 'existing.json'
    [IO.File]::WriteAllText($existing, 'preserve')
    $null = & $desktop -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $capture -SourceRoot $site -Out $existing 2>&1
    Assert-True ($LASTEXITCODE -ne 0 -and [IO.File]::ReadAllText($existing) -ceq 'preserve') 'EXISTING_OUTPUT_CHANGED'
    Assert-True ((Hash-Bytes ([IO.File]::ReadAllBytes((Join-Path $site 'App_Code/PublicHelper.vb')))) -ceq $originalSourceHash) 'SOURCE_CHANGED'
    Write-Output 'Web Forms VB imports capture tests passed (mapped configuration; no compilation or application execution).'
} finally {
    $resolved = [IO.Path]::GetFullPath($temporary)
    if (!$resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolved -Leaf).StartsWith('vb-import-capture-test-')) { throw 'TEST_CLEANUP_REFUSED' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
