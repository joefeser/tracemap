# Capture current mapped-root application configuration, never historical build proof.
# Run with Windows PowerShell 5.1 in the framework/architecture to be recorded.
# No provider construction, application assembly loading, compilation, or SQL.
# Rule: webforms.vb-import-context.current-environment.v1.
# Scope is an explicitly mapped '/' application, not IIS/site-parent discovery.
# External config, relevant location overrides, section redeclarations, custom
# providers, uncertain aliases, and unsupported options are refused. Framework
# handler internals are version-sensitive and fail closed. Capture and rechecks
# are current-environment observations, not an atomic snapshot or build receipt.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceRoot,
    [Parameter(Mandatory = $true)][string]$Out,
    [string[]]$SourceFile = @()
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Stop-Capture([string]$Code) { throw $Code }
function Assert-RegularPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        $item = Get-Item -LiteralPath $current
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { Stop-Capture 'VB_IMPORT_CAPTURE_REPARSE_REFUSED' }
        $parent = [IO.Directory]::GetParent($current)
        $current = $(if ($null -eq $parent) { $null } else { $parent.FullName })
    }
}
function Hash-Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Read-Bounded([string]$Path, [long]$Limit) {
    Assert-RegularPath $Path
    $file = Get-Item -LiteralPath $Path
    if (!$file.PSIsContainer -and ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 -and $file.Length -le $Limit) {
        $readStream = New-Object IO.FileStream($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        $bounded = New-Object IO.MemoryStream
        $buffer = New-Object byte[] 65536
        try {
            while ($true) {
                $remaining = $Limit - $bounded.Length
                $read = $readStream.Read($buffer, 0, [int][Math]::Min($buffer.Length, $remaining + 1))
                if ($read -eq 0) { return ,$bounded.ToArray() }
                if ($read -gt $remaining) { Stop-Capture 'VB_IMPORT_CAPTURE_FILE_BOUND' }
                $bounded.Write($buffer, 0, $read)
            }
        } finally { $bounded.Dispose(); $readStream.Dispose() }
    }
    Stop-Capture 'VB_IMPORT_CAPTURE_FILE_BOUND'
}
try {
    if ($PSVersionTable.PSEdition -ne 'Desktop') { Stop-Capture 'VB_IMPORT_CAPTURE_REQUIRES_WINDOWS_FRAMEWORK_POWERSHELL' }
    Add-Type -AssemblyName System.Web
    Add-Type -AssemblyName System.Configuration
    $root = [IO.Path]::GetFullPath($SourceRoot).TrimEnd([char[]]'\/')
    $rootItem = Get-Item -LiteralPath $root
    if (!$rootItem.PSIsContainer -or ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) { Stop-Capture 'VB_IMPORT_CAPTURE_ROOT_INVALID' }
    Assert-RegularPath $root
    $subset = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($relative in $SourceFile) {
        if ($relative -notmatch '^App_Code/[^\\]+\.vb$' -or @($relative.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0 -or !$subset.Add($relative)) {
            Stop-Capture 'VB_IMPORT_CAPTURE_SOURCE_SCOPE_INVALID'
        }
    }
    if ($subset.Count -gt 256) { Stop-Capture 'VB_IMPORT_CAPTURE_SOURCE_BOUND' }
    $commit = @(& git -C $root rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $commit.Count -ne 1 -or $commit[0] -cnotmatch '^[0-9a-f]{40,64}$') { Stop-Capture 'VB_IMPORT_CAPTURE_COMMIT_UNAVAILABLE' }
    $appCode = Join-Path $root 'App_Code'
    if (!(Test-Path -LiteralPath $appCode -PathType Container)) { Stop-Capture 'VB_IMPORT_CAPTURE_APPCODE_UNAVAILABLE' }
    # Enumerate explicitly so reparse directories are refused before descent.
    $queue = New-Object 'Collections.Generic.Queue[string]'
    $queue.Enqueue($appCode)
    $sourcePaths = New-Object 'Collections.Generic.List[string]'
    $entries = 0
    while ($queue.Count -gt 0) {
        $directory = $queue.Dequeue()
        $directoryItem = Get-Item -LiteralPath $directory
        if ($directoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { Stop-Capture 'VB_IMPORT_CAPTURE_REPARSE_REFUSED' }
        $iterator = [IO.Directory]::EnumerateFileSystemEntries($directory).GetEnumerator()
        try { while ($iterator.MoveNext()) {
            if (++$entries -gt 4096) { Stop-Capture 'VB_IMPORT_CAPTURE_SCOPE_BOUND' }
            $entry = Get-Item -LiteralPath $iterator.Current -Force
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { Stop-Capture 'VB_IMPORT_CAPTURE_REPARSE_REFUSED' }
            if ($entry.PSIsContainer) { $queue.Enqueue($entry.FullName) }
            elseif ($entry.Extension -ieq '.vb') {
                $relative = $entry.FullName.Substring($root.Length + 1).Replace('\', '/')
                if ($subset.Count -gt 0 -and !$subset.Contains($relative)) { continue }
                $sourcePaths.Add($entry.FullName)
                if ($sourcePaths.Count -gt 256) { Stop-Capture 'VB_IMPORT_CAPTURE_SOURCE_BOUND' }
            }
            elseif ($entry.Name -ieq 'web.config') { Stop-Capture 'VB_IMPORT_CAPTURE_NESTED_CONFIGURATION_REFUSED' }
        } } finally { ([IDisposable]$iterator).Dispose() }
    }
    if ($subset.Count -gt 0 -and $sourcePaths.Count -ne $subset.Count) { Stop-Capture 'VB_IMPORT_CAPTURE_SOURCE_SCOPE_INVALID' }
    if ($sourcePaths.Count -eq 0) { Stop-Capture 'VB_IMPORT_CAPTURE_SOURCE_UNAVAILABLE' }
    $totalSourceBytes = 0L
    foreach ($path in $sourcePaths) { $totalSourceBytes += (Get-Item -LiteralPath $path).Length }
    if ($totalSourceBytes -gt 64MB) { Stop-Capture 'VB_IMPORT_CAPTURE_SOURCE_BOUND' }
    $sources = @($sourcePaths | Sort-Object -CaseSensitive | ForEach-Object {
        [ordered]@{ path = $_.Substring($root.Length + 1).Replace('\', '/'); sha256 = Hash-Bytes (Read-Bounded $_ (64MB)) }
    })
    $runtimeDirectory = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
    $machine = [Runtime.InteropServices.RuntimeEnvironment]::SystemConfigurationFile
    $rootConfig = Join-Path $runtimeDirectory 'Config/Web.config'
    $siteConfig = Join-Path $root 'Web.config'
    $siteConfigPresent = Test-Path -LiteralPath $siteConfig -PathType Leaf
    $configPaths = @($machine, $rootConfig)
    if (Test-Path -LiteralPath $siteConfig -PathType Leaf) { $configPaths += $siteConfig }
    $configurations = @()
    $documents = @()
    foreach ($path in $configPaths) {
        $bytes = Read-Bounded $path (2MB)
        $configurations += [ordered]@{ path = [IO.Path]::GetFullPath($path); sha256 = Hash-Bytes $bytes }
        $settings = New-Object Xml.XmlReaderSettings
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $stream = New-Object IO.MemoryStream(,$bytes)
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        $document = New-Object Xml.XmlDocument
        $document.XmlResolver = $null
        try { $document.Load($reader) } finally { $reader.Dispose(); $stream.Dispose() }
        # The application cannot supply handlers for sections we evaluate.
        if ($path -eq $siteConfig -and $document.SelectNodes('/configuration/configSections/section[@name="system.codedom"] | /configuration/configSections/sectionGroup[@name="system.web"]/section[@name="pages" or @name="compilation"] | /configuration/configSections/sectionGroup[@name="system.web"][@type]').Count -gt 0) {
            Stop-Capture 'VB_IMPORT_CAPTURE_SECTION_REDEFINITION_REFUSED'
        }
        foreach ($declaration in $document.SelectNodes('/configuration/configSections/section[@name="system.codedom"] | /configuration/configSections/sectionGroup[@name="system.web"]/section[@name="pages" or @name="compilation"]')) {
            $expectedType = switch ($declaration.GetAttribute('name')) {
                'system.codedom' { 'System.CodeDom.Compiler.CodeDomConfigurationHandler' }
                'pages' { 'System.Web.Configuration.PagesSection' }
                'compilation' { 'System.Web.Configuration.CompilationSection' }
            }
            $assembly = $(if ($declaration.GetAttribute('name') -eq 'system.codedom') { 'System' } else { 'System.Web' })
            $token = $(if ($assembly -eq 'System') { 'b77a5c561934e089' } else { 'b03f5f7f11d50a3a' })
            $expected = $expectedType + ', ' + $assembly + ', Version=4.0.0.0, Culture=neutral, PublicKeyToken=' + $token
            if ($declaration.GetAttribute('type') -cne $expected) { Stop-Capture 'VB_IMPORT_CAPTURE_SECTION_REDEFINITION_REFUSED' }
        }
        if ($document.SelectNodes('/configuration/location/system.codedom | /configuration/location/system.web/pages | /configuration/location/system.web/compilation').Count -gt 0) {
            Stop-Capture 'VB_IMPORT_CAPTURE_LOCATION_REFUSED'
        }
        foreach ($node in $document.SelectNodes('/configuration/system.codedom | /configuration/system.web/pages | /configuration/system.web/compilation')) {
            if ($node.HasAttribute('configSource') -or $node.HasAttribute('configProtectionProvider')) { Stop-Capture 'VB_IMPORT_CAPTURE_EXTERNAL_CONFIGURATION_REFUSED' }
        }
        $documents += $document
    }
    $map = New-Object System.Web.Configuration.WebConfigurationFileMap
    $map.MachineConfigFilename = $machine
    $mapping = New-Object System.Web.Configuration.VirtualDirectoryMapping($root, $true)
    $map.VirtualDirectories.Add('/', $mapping)
    $configuration = [System.Web.Configuration.WebConfigurationManager]::OpenMappedWebConfiguration($map, '/')
    $pages = $configuration.GetSection('system.web/pages')
    $compilation = $configuration.GetSection('system.web/compilation')
    foreach ($compiler in $compilation.Compilers) {
        if (@($compiler.Language.Split(';') | Where-Object { $_.Trim() -imatch '^(vb|vbs|visualbasic|vbscript)$' }).Count -gt 0 -or $compiler.Extension -ieq '.vb') {
            Stop-Capture 'VB_IMPORT_CAPTURE_LEGACY_COMPILER_OVERRIDE_REFUSED'
        }
    }
    # OpenMappedWebConfiguration exposes system.codedom as DefaultSection.
    # Host CodeDomProvider.GetCompilerInfo is NOT the mapped site configuration.
    # Replay only the trusted framework handler, without reading ProviderType or
    # constructing providers. Internals are pinned to the loaded framework binary;
    # an unavailable shape is a refusal, never a host-config fallback.
    $systemAssembly = [System.CodeDom.Compiler.CodeDomProvider].Assembly
    $handler = $systemAssembly.GetType('System.CodeDom.Compiler.CodeDomCompilationConfiguration+SectionHandler', $true)
    $create = $handler.GetMethod('CreateStatic', [Reflection.BindingFlags]'NonPublic,Static')
    if ($null -eq $create) { Stop-Capture 'VB_IMPORT_CAPTURE_FRAMEWORK_API_UNAVAILABLE' }
    $effective = $null
    foreach ($document in $documents) {
        $node = $document.SelectSingleNode('/configuration/system.codedom')
        if ($null -ne $node) { $effective = $create.Invoke($null, @($effective, $node.CloneNode($true))) }
    }
    if ($null -eq $effective) {
        $defaultType = $systemAssembly.GetType('System.CodeDom.Compiler.CodeDomCompilationConfiguration', $true)
        $effective = [Activator]::CreateInstance($defaultType, $true)
    }
    $flags = [Reflection.BindingFlags]'NonPublic,Instance'
    $languages = $effective.GetType().GetField('_compilerLanguages', $flags).GetValue($effective)
    $extensions = $effective.GetType().GetField('_compilerExtensions', $flags).GetValue($effective)
    $info = $extensions['.vb']
    if ($null -eq $info -or ![object]::ReferenceEquals($info, $languages['vb'])) { Stop-Capture 'VB_IMPORT_CAPTURE_PROVIDER_AMBIGUOUS' }
    foreach ($alias in @('vbs', 'visualbasic', 'vbscript')) {
        if (![object]::ReferenceEquals($info, $languages[$alias])) { Stop-Capture 'VB_IMPORT_CAPTURE_PROVIDER_AMBIGUOUS' }
    }
    $typeName = $info.GetType().GetField('_codeDomProviderTypeName', $flags).GetValue($info)
    $typeParts = $typeName.Split(',', 2)
    if ($typeParts.Count -ne 2 -or $typeParts[0].Trim() -cne 'Microsoft.VisualBasic.VBCodeProvider') { Stop-Capture 'VB_IMPORT_CAPTURE_CUSTOM_PROVIDER_REFUSED' }
    $configuredAssembly = New-Object Reflection.AssemblyName($typeParts[1].Trim())
    $actualAssembly = $systemAssembly.GetName()
    $configuredToken = $configuredAssembly.GetPublicKeyToken()
    if ($configuredAssembly.Name -cne 'System' -or
        ($null -ne $configuredAssembly.Version -and $configuredAssembly.Version -ne $actualAssembly.Version) -or
        ($null -ne $configuredToken -and $configuredToken.Length -gt 0 -and [BitConverter]::ToString($configuredToken) -ne [BitConverter]::ToString($actualAssembly.GetPublicKeyToken())) -or
        ($configuredAssembly.CultureName -and $configuredAssembly.CultureName -ne 'neutral')) { Stop-Capture 'VB_IMPORT_CAPTURE_CUSTOM_PROVIDER_REFUSED' }
    $providerOptions = $info.GetType().GetField('_providerOptions', $flags).GetValue($info)
    foreach ($key in $providerOptions.Keys) {
        if ($key -cnotin @('CompilerVersion', 'OptionInfer', 'WarnAsError')) { Stop-Capture 'VB_IMPORT_CAPTURE_PROVIDER_OPTIONS_REFUSED' }
        if ($key -ceq 'CompilerVersion' -and $providerOptions[$key] -cne 'v4.0') { Stop-Capture 'VB_IMPORT_CAPTURE_PROVIDER_OPTIONS_REFUSED' }
    }
    $compilerParameters = $info.GetType().GetField('_compilerParams', $flags)
    if ($null -eq $compilerParameters) { Stop-Capture 'VB_IMPORT_CAPTURE_FRAMEWORK_API_UNAVAILABLE' }
    $options = $compilerParameters.GetValue($info).CompilerOptions
    $namespaces = New-Object 'Collections.Generic.SortedSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $pages.Namespaces) { [void]$namespaces.Add($entry.Namespace) }
    if ($pages.Namespaces.AutoImportVBNamespace) { [void]$namespaces.Add('Microsoft.VisualBasic') }
    if ($options) {
        if ($options.Length -gt 16384 -or $options.Contains('@')) { Stop-Capture 'VB_IMPORT_CAPTURE_COMPILER_OPTIONS_REFUSED' }
        # Refuse quoting/aliases and unsupported switches rather than guessing.
        foreach ($option in $options.Trim().Split([char[]]' ', [StringSplitOptions]::RemoveEmptyEntries)) {
            if ($option -match '^[-/]imports:(.+)$') {
                foreach ($name in $Matches[1].Split(',')) {
                    if ($name -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$') { Stop-Capture 'VB_IMPORT_CAPTURE_COMPILER_OPTIONS_REFUSED' }
                    [void]$namespaces.Add($name)
                }
            }
            elseif ($option -notmatch '^[-/](optionstrict|optionexplicit|optioninfer|warnaserror|nowarn|langversion|optimize|debug)([+\-]|:[A-Za-z0-9_,.]+)?$') { Stop-Capture 'VB_IMPORT_CAPTURE_COMPILER_OPTIONS_REFUSED' }
        }
    }
    if ($namespaces.Count -eq 0 -or $namespaces.Count -gt 128) { Stop-Capture 'VB_IMPORT_CAPTURE_NAMESPACE_BOUND' }
    foreach ($name in $namespaces) {
        if ($name.Length -gt 512 -or $name -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$') { Stop-Capture 'VB_IMPORT_CAPTURE_NAMESPACE_INVALID' }
    }
    $providerPath = $systemAssembly.Location
    $payload = [ordered]@{
        sourceCommitSha = $commit[0]
        sourceRootPathSha256 = Hash-Bytes ([Text.Encoding]::UTF8.GetBytes($root.Replace('\', '/').ToUpperInvariant()))
        providerType = 'Microsoft.VisualBasic.VBCodeProvider'
        providerAssemblyPath = $providerPath
        providerAssemblySha256 = Hash-Bytes (Read-Bounded $providerPath (64MB))
        runtimeVersion = [Runtime.InteropServices.RuntimeEnvironment]::GetSystemVersion().TrimStart('v')
        processArchitecture = $(if ([Environment]::Is64BitProcess) { 'x64' } else { 'x86' })
        namespaces = @($namespaces)
        sourceFiles = $sources
        configurationFiles = $configurations
        evidenceKind = 'current-environment-not-build-proof'
        mappingKind = 'standalone-root'
    }
    foreach ($entry in $configurations) {
        if ((Hash-Bytes (Read-Bounded $entry.path (2MB))) -cne $entry.sha256) { Stop-Capture 'VB_IMPORT_CAPTURE_INPUT_CHANGED' }
    }
    if ((Test-Path -LiteralPath $siteConfig -PathType Leaf) -ne $siteConfigPresent) { Stop-Capture 'VB_IMPORT_CAPTURE_INPUT_CHANGED' }
    foreach ($entry in $sources) {
        if ((Hash-Bytes (Read-Bounded (Join-Path $root $entry.path) (64MB))) -cne $entry.sha256) { Stop-Capture 'VB_IMPORT_CAPTURE_INPUT_CHANGED' }
    }
    $payloadJson = $payload | ConvertTo-Json -Depth 12 -Compress
    $receipt = [ordered]@{
        schemaVersion = 'webforms-vb-import-context.v1'
        ruleId = 'webforms.vb-import-context.current-environment.v1'
        visibility = 'local-only'
        generatorSha256 = Hash-Bytes (Read-Bounded $PSCommandPath (2MB))
        boundedInputSha256 = Hash-Bytes ([Text.Encoding]::UTF8.GetBytes($payloadJson))
        payloadJson = $payloadJson
    }
    $outputBytes = [Text.Encoding]::UTF8.GetBytes(($receipt | ConvertTo-Json -Depth 4 -Compress))
    if ($outputBytes.LongLength -gt 1MB) { Stop-Capture 'VB_IMPORT_CAPTURE_RECEIPT_BOUND' }
    $destination = [IO.Path]::GetFullPath($Out)
    if ($destination.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { Stop-Capture 'VB_IMPORT_CAPTURE_OUTPUT_INSIDE_SOURCE_REFUSED' }
    Assert-RegularPath ([IO.Path]::GetDirectoryName($destination))
    $outputStream = New-Object IO.FileStream($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $outputStream.Write($outputBytes, 0, $outputBytes.Length) } finally { $outputStream.Dispose() }
    Write-Output 'VB import context captured (local-only; current environment, not historical build proof).'
} catch {
    # Never expose config contents, paths, provider names or exception stacks.
    $code = $_.Exception.Message
    if ($code -cnotmatch '^VB_IMPORT_CAPTURE_[A-Z_]+$') { $code = 'VB_IMPORT_CAPTURE_CONFIGURATION_REFUSED' }
    [Console]::Error.WriteLine($code)
    exit 1
}
