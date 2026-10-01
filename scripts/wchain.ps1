param([string]$ReportFolder, [ValidateRange(1,100000)][int]$Chain = 7, [switch]$Open)
$ErrorActionPreference = 'Stop'
if (-not $ReportFolder) { $ReportFolder = Read-Host 'Handler requery folder (full path)' }
$inputPath = Join-Path $ReportFolder 'compiled-paths.local.html'
$outputPath = Join-Path $ReportFolder "chain-$Chain.diagnostic.local.html"
if (Test-Path -LiteralPath $outputPath) { throw 'WEBFORMS_CHAIN_OUTPUT_EXISTS;originals-preserved' }
# Read the rendered chain, not the large JSON inventory. No native validation.
$stream = [IO.File]::Open($inputPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    if ($stream.Length -gt 512MB) { throw 'WEBFORMS_CHAIN_INPUT_LIMIT' }
    $inputHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
    $stream.Position = 0
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 16384, $true)
    $body = [Text.StringBuilder]::new()
    $found = $false
    $complete = $false
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ($line.Length -gt 1MB) { throw 'WEBFORMS_CHAIN_LINE_LIMIT' }
            if (-not $found) {
                if (-not $line.StartsWith("<section id=`"chain-$Chain`"><h2>Chain $Chain ", [StringComparison]::Ordinal)) { continue }
                $found = $true
            }
            if ($line.Length -gt 8MB - $body.Length) { throw 'WEBFORMS_CHAIN_OUTPUT_LIMIT' }
            [void]$body.AppendLine($line)
            if ($line -eq '</ul></details></section>') { $complete = $true; break }
        }
    } finally { $reader.Dispose() }
    if (-not $found) { throw 'WEBFORMS_CHAIN_SELECTION_INVALID' }
    if (-not $complete) { throw 'WEBFORMS_CHAIN_SECTION_INCOMPLETE' }
} finally { $stream.Dispose() }
# Decode text and encode again: never execute copied HTML, even if tampered.
$text = [regex]::Replace($body.ToString(), '<br\s*/?>|</(?:p|li|td|tr|summary|h2)>', [Environment]::NewLine, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
$text = [Net.WebUtility]::HtmlDecode([regex]::Replace($text, '<[^>]*>', ''))
$encoded = [Net.WebUtility]::HtmlEncode($text)
$generatorHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$bounded = [Text.Encoding]::UTF8.GetBytes("webforms-chain-diagnostic.v2" + [char]10 + $inputHash + [char]10 + $Chain)
$boundedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bounded)).ToLowerInvariant()
$html = "<!doctype html><html lang='en'><meta charset='utf-8'><title>Chain $Chain local diagnostic</title><style>body{font:16px/1.5 system-ui;max-width:1100px;margin:2rem auto;padding:1rem}code,pre{overflow-wrap:anywhere;white-space:pre-wrap}</style><h1>Chain $Chain connecting evidence</h1><p>PRIVATE LOCAL DIAGNOSTIC — unadmitted rendered readback, not validated native evidence, graph correctness, parity or runtime execution. No scan or traversal. Exact identities may be private. Do not publish.</p><p>Rule workflow.webforms.chain-diagnostic.v2; tier Tier4Unknown</p><p>Generator SHA-256 <code>$generatorHash</code><br>Bounded input SHA-256 <code>$boundedHash</code><br>Input HTML SHA-256 <code>$inputHash</code>; chain $Chain</p><pre>$encoded</pre></html>"
$output = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $output.Write([Text.Encoding]::UTF8.GetBytes($html)) } finally { $output.Dispose() }
Write-Output "chainDiagnostic=written;chain=$Chain;unadmitted-local-only;no-json-load;no-scan;no-traversal;originals-preserved"
if ($Open) { Start-Process $outputPath }
