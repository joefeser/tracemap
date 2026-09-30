$ErrorActionPreference = 'Stop'
$helper = Join-Path (Split-Path $PSScriptRoot -Parent) 'wchain.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-chain-test-' + [guid]::NewGuid())
[void][IO.Directory]::CreateDirectory($root)
try {
    $inputPath = Join-Path $root 'compiled-paths.local.html'
    $fixture = @'
<section id="chain-1"><h2>Chain 1 <small>(1 variants)</small></h2>
<p>Method&lt;Unsafe&gt; → Target()</p>
<details><summary>Retained transition evidence</summary><table><tr><td>synthetic.calls.v1<br>Tier2Structural</td><td>fact:one</td></tr></table></details>
<details><summary>Every retained path variant</summary><ul>
<li>variant:one</li>
</ul></details></section>
<section id="chain-2"><h2>Chain 2 <small>(1 variants)</small></h2>
<p>Must not include this other chain</p>
</ul></details></section>
'@
    [IO.File]::WriteAllText($inputPath, $fixture)
    $json = [IO.File]::Create((Join-Path $root 'compiled-paths.handoff.local.json'))
    try { $json.SetLength(65MB) } finally { $json.Dispose() }
    $before = (Get-FileHash $inputPath).Hash
    & $helper -ReportFolder $root -Chain 1 | Out-Null
    $html = [IO.File]::ReadAllText((Join-Path $root 'chain-1.diagnostic.local.html'))
    if ($html -notmatch 'Method&lt;Unsafe&gt;' -or $html -match 'Method<Unsafe>' -or $html -notmatch 'synthetic.calls.v1' -or $html -notmatch 'fact:one' -or $html -match 'Must not include') { throw 'Evidence, isolation or escaping missing' }
    if ($html -notmatch 'Generator SHA-256' -or $html -notmatch 'Bounded input SHA-256' -or $html -notmatch 'unadmitted') { throw 'Provenance missing' }
    if ($before -ne (Get-FileHash $inputPath).Hash) { throw 'Input changed' }
    try { & $helper -ReportFolder $root -Chain 1; throw 'Expected collision' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CHAIN_OUTPUT_EXISTS;originals-preserved') { throw } }
    try { & $helper -ReportFolder $root -Chain 3; throw 'Expected invalid selection' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CHAIN_SELECTION_INVALID') { throw } }
    [IO.File]::WriteAllText($inputPath, '<section id="chain-3"><h2>Chain 3 <small>incomplete</small></h2>')
    try { & $helper -ReportFolder $root -Chain 3; throw 'Expected incomplete section' }
    catch { if ($_.Exception.Message -ne 'WEBFORMS_CHAIN_SECTION_INCOMPLETE') { throw } }
    if (Test-Path (Join-Path $root 'chain-3.diagnostic.local.html')) { throw 'Failure wrote output' }
    'webFormsChainDiagnosticPublicTests=passed'
} finally { Remove-Item -LiteralPath $root -Recurse -Force }
