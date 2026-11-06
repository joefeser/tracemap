param(
    [Parameter(Mandatory=$true)][string]$OutRoot,
    [string]$FixtureRoot = (Join-Path $PSScriptRoot '../../samples/vb-compile-context'),
    [switch]$Worker
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7 -or -not $IsWindows) { throw 'Windows and PowerShell 7 required' }
function HashFile([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function HashText([string]$text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant() }
function WriteJson([string]$path, $value) { [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false)) }
function Roster([string]$root) {
    $files=@(Get-ChildItem -LiteralPath $root -Recurse -File | Select-Object -First 257)
    if($files.Count -gt 256){throw 'Output roster file limit'}
    @($files | Sort-Object FullName | ForEach-Object {
        if($_.Length -gt 16MB){throw 'Output roster file byte limit'}
        [ordered]@{ path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/'); sha256=(HashFile $_.FullName) }
    })
}
function FixtureRoster([string]$root) {
    $paths=@('Ambiguity.vb','AmbiguityControlled.vb','Consumer.vb','Dependency.vb','website-appcode/App_Code/Probe.vb','website-appcode/Default.aspx','website-appcode/Web.config','website-page/Default.aspx','website-page/Web.config')
    @($paths | ForEach-Object {
        $path=Join-Path $root $_
        $file=Get-Item -LiteralPath $path -Force
        if($file.PSIsContainer -or $file.Length -gt 65536 -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Fixture must be a bounded regular file'}
        for($dir=$file.Directory; $null -ne $dir; $dir=$dir.Parent){
            if($dir.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Fixture path must not traverse links'}
            if($dir.FullName -eq $root){break}
        }
        [ordered]@{path=$_;sha256=(HashFile $path)}
    })
}
$OutRoot = [IO.Path]::GetFullPath($OutRoot)
$generator = HashFile $PSCommandPath
if (-not $Worker) {
    if (Test-Path -LiteralPath $OutRoot) { throw 'Fresh output directory required; previous attempts are preserved' }
    $parent = [IO.Path]::GetDirectoryName($OutRoot)
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Output parent must exist' }
    for ($p=[IO.DirectoryInfo]::new($parent); $null -ne $p; $p=$p.Parent) {
        if (Test-Path -LiteralPath (Join-Path $p.FullName '.git')) { throw 'Output must be outside Git' }
    }
    $FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
    $before = FixtureRoster $FixtureRoot
    if ($before.Count -ne 9) { throw 'Public fixture roster must contain exactly nine files' }
    [void][IO.Directory]::CreateDirectory($OutRoot)
    foreach($item in $before){
        $destination=Join-Path (Join-Path $OutRoot 'inputs') $item.path
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
        Copy-Item -LiteralPath (Join-Path $FixtureRoot $item.path) -Destination $destination
    }
    $profiles = @()
    foreach ($entry in @(@('framework2','v2.0.50727'),@('framework4','v4.0.30319'))) {
        $path = Join-Path $env:WINDIR ('Microsoft.NET/Framework64/'+$entry[1]+'/vbc.exe')
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Required compiler unavailable: '+$entry[0]) }
        $profiles += [ordered]@{ id=$entry[0]; frameworkFolder=$entry[1]; fileVersion=([Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion); sha256=(HashFile $path) }
    }
    $aspnet=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/aspnet_compiler.exe'
    if (-not (Test-Path -LiteralPath $aspnet -PathType Leaf)) { throw 'ASP.NET 4 compiler unavailable' }
    $projection=[ordered]@{ fixtures=$before; compilers=$profiles; aspnetCompilerSha256=(HashFile $aspnet); timeoutSeconds=120; jobMemoryMiB=2048; activeProcessLimit=16 }
    $inputHash=HashText ($projection | ConvertTo-Json -Depth 12 -Compress)
    WriteJson (Join-Path $OutRoot 'input.json') ([ordered]@{ schema='vb-compile-context-input/1'; ruleId='validation.vb-compile-context.v1'; generatorSha256=$generator; boundedInputSha256=$inputHash; projection=$projection })
    $start=[Diagnostics.ProcessStartInfo]::new(); $start.FileName=(Get-Process -Id $PID).Path; $start.UseShellExecute=$false
    foreach ($arg in @('-NoProfile','-File',$PSCommandPath,'-OutRoot',$OutRoot,'-Worker')) { $start.ArgumentList.Add($arg) }
    $start.Environment['TRACEMAP_VB_POC_WORKER']='1'
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $clock=[Diagnostics.Stopwatch]::StartNew(); $process=[Diagnostics.Process]::Start($start)
    $finished=$process.WaitForExit(120000)
    if (-not $finished) { $process.Kill($true); $process.WaitForExit() }
    $stdout=$process.StandardOutput.ReadToEnd(); $stderr=$process.StandardError.ReadToEnd(); $code=$process.ExitCode; $process.Dispose()
    [IO.File]::WriteAllText((Join-Path $OutRoot 'worker.stdout.log'),$stdout)
    [IO.File]::WriteAllText((Join-Path $OutRoot 'worker.stderr.log'),$stderr)
    $after=FixtureRoster $FixtureRoot
    $preserved=(($before | ConvertTo-Json -Compress) -ceq ($after | ConvertTo-Json -Compress))
    WriteJson (Join-Path $OutRoot 'guard.json') ([ordered]@{ schema='vb-compile-context-guard/1'; ruleId='validation.vb-compile-context.v1'; generatorSha256=$generator; boundedInputSha256=$inputHash; timeoutSeconds=120; memoryLimitMiB=2048; elapsedMilliseconds=$clock.ElapsedMilliseconds; timedOut=(-not $finished); workerExit=$code; originalFixturesPreserved=$preserved; limitation='public synthetic compiler fixture only; no private conversion or runtime proof' })
    Write-Output $stdout
    if (-not $finished -or $code -ne 0 -or -not $preserved) { throw 'Public POC failed; retained output contains diagnostics' }
    Write-Output 'PASS bounded Windows VB compile-context POC; no application/database execution'
    exit 0
}
if ($env:TRACEMAP_VB_POC_WORKER -ne '1') { throw 'Internal worker requires parent invocation' }
[Environment]::SetEnvironmentVariable('TRACEMAP_VB_POC_WORKER',$null)
# Same Windows job limits as the scan guard, scoped to this public test worker.
# The worker joins BEFORE it starts any compiler. Parent timeout kills worker;
# closing its rooted job handle kills compiler descendants (including ASP.NET).
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class TraceMapVbPocJob {
    private static SafeFileHandle job;
    [StructLayout(LayoutKind.Sequential)] struct Basic { public long UserTime,JobTime; public uint Flags; public UIntPtr Min,Max; public uint Active; public UIntPtr Affinity; public uint Priority,Scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong A,B,C,D,E,F; }
    [StructLayout(LayoutKind.Sequential)] struct Extended { public Basic Basic; public Io Io; public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob; }
    [DllImport("kernel32.dll",SetLastError=true)] static extern SafeFileHandle CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(SafeFileHandle handle,int type,ref Extended info,uint length);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool QueryInformationJobObject(SafeFileHandle handle,int type,out Extended info,uint length,IntPtr returned);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(SafeFileHandle handle,IntPtr process);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    public static void AssignSelf() {
        if(IntPtr.Size!=8) throw new InvalidOperationException("64-bit worker required");
        job=CreateJobObject(IntPtr.Zero,null);
        var limits=new Extended { Basic=new Basic { Flags=0x2000|0x200|0x8,Active=16 },JobMemory=new UIntPtr(2147483648UL) };
        if(job.IsInvalid || !SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<Extended>()) || !AssignProcessToJobObject(job,GetCurrentProcess())) throw new InvalidOperationException("Job assignment failed; no compiler launched");
    }
    public static ulong Peak() { Extended info; if(!QueryInformationJobObject(job,9,out info,(uint)Marshal.SizeOf<Extended>(),IntPtr.Zero)) throw new InvalidOperationException("Job metric unavailable"); return info.PeakJob.ToUInt64(); }
}
'@
[TraceMapVbPocJob]::AssignSelf()
$inputRecord=Get-Content -Raw -LiteralPath (Join-Path $OutRoot 'input.json') | ConvertFrom-Json
if ($inputRecord.generatorSha256 -ne $generator) { throw 'Generator changed' }
$inputs=Join-Path $OutRoot 'inputs'
$roster=FixtureRoster $inputs
if ((HashText ($inputRecord.projection | ConvertTo-Json -Depth 12 -Compress)) -ne $inputRecord.boundedInputSha256) { throw 'Input projection changed' }
if (($roster | ConvertTo-Json -Depth 8 -Compress) -cne ($inputRecord.projection.fixtures | ConvertTo-Json -Depth 8 -Compress)) { throw 'Fixture copy changed' }
$cases=[Collections.Generic.List[object]]::new()
function InvokeCompiler([string]$name,[string]$exe,[string[]]$arguments,[string]$output,[string]$expect,[string]$diagnostic='') {
    if ($output -and (Test-Path -LiteralPath $output)) { throw ('Stale output: '+$name) }
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$exe;$start.UseShellExecute=$false;$start.WorkingDirectory=$OutRoot
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach ($arg in $arguments) { $start.ArgumentList.Add($arg) }
    $clock=[Diagnostics.Stopwatch]::StartNew();$p=[Diagnostics.Process]::Start($start)
    $stdoutTask=$p.StandardOutput.ReadToEndAsync();$stderrTask=$p.StandardError.ReadToEndAsync();$p.WaitForExit()
    $log=$stdoutTask.GetAwaiter().GetResult()+$stderrTask.GetAwaiter().GetResult();$code=$p.ExitCode;$p.Dispose()
    [IO.File]::WriteAllText((Join-Path $OutRoot ($name+'.log')),$log)
    $fresh=$false
    if($output) { $fresh=(Test-Path -LiteralPath $output -PathType Leaf) -or ((Test-Path -LiteralPath $output -PathType Container) -and @(Get-ChildItem -LiteralPath $output -Recurse -Filter '*.dll').Count -gt 0) }
    $ok=if($expect -eq 'pass') {$code -eq 0 -and $fresh} elseif($expect -eq 'fail') {$code -ne 0 -and -not $fresh -and $log.Contains($diagnostic)} else {($code -eq 0 -and $fresh) -or ($code -ne 0 -and -not $fresh -and $log.Contains($diagnostic))}
    $item=[ordered]@{ name=$name; expected=$expect; observed=$(if($code -eq 0 -and $fresh){'pass'}else{'fail'}); exitCode=$code; freshOutput=$fresh; outputSha256=$(if($fresh){if(Test-Path -LiteralPath $output -PathType Leaf){HashFile $output}else{HashText ((Roster $output) | ConvertTo-Json -Depth 8 -Compress)}}else{$null}); logSha256=(HashText $log); expectedDiagnosticPresent=$(if($diagnostic){$log.Contains($diagnostic)}else{$null}); elapsedMilliseconds=$clock.ElapsedMilliseconds; oraclePassed=[bool]$ok }
    $cases.Add($item); Write-Output ($name+': '+$item.observed+'; oracle='+$ok)
    if (-not $ok) { throw ('Unexpected compiler outcome: '+$name) }
}
function Vbc([string]$profile,[string]$name,[string[]]$extra,[string]$source,[string]$expect,[string]$diagnostic='') {
    $tool=$inputRecord.projection.compilers | Where-Object id -eq $profile
    $folder=Join-Path $env:WINDIR ('Microsoft.NET/Framework64/'+$tool.frameworkFolder)
    $exe=Join-Path $folder 'vbc.exe'
    if ((HashFile $exe) -ne $tool.sha256) { throw 'Compiler changed' }
    $output=Join-Path $OutRoot ($name+'.dll')
    $args=@('/nologo','/noconfig','/target:library',('/out:'+ $output),('/sdkpath:'+ $folder),('/reference:'+ (Join-Path $folder 'System.dll')),('/reference:'+ (Join-Path $folder 'System.Data.dll')),('/reference:'+ (Join-Path $folder 'System.Xml.dll')))+$extra+@($source)
    InvokeCompiler $name $exe $args $output $expect $diagnostic
}
$execHash=$null
try {
    Vbc 'framework2' 'dependency-net20' @() (Join-Path $inputs 'Dependency.vb') 'pass'
    $library=Join-Path $OutRoot 'dependency-net20.dll'
    $raw=[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($library))
    if(-not $raw.Contains('v2.0.50727')) { throw 'Dependency CLR2 metadata marker absent' }
    $original=Join-Path $inputs 'Consumer.vb'
    $text=[IO.File]::ReadAllText($original)
    $annotated=Join-Path $OutRoot 'AnnotatedConsumer.vb'
    $annotation=$text.Replace('    Public Function ReadItems(',"    ' Public synthetic annotation; executable statements unchanged.`n    Public Function ReadItems(")
    [IO.File]::WriteAllText($annotated,$annotation)
    $executable={ param($s) (($s -split '\r?\n' | Where-Object { -not $_.TrimStart().StartsWith("'") -and $_.Trim() }) -join "`n") }
    $execHash=HashText (& $executable $text)
    if ($execHash -ne (HashText (& $executable $annotation))) { throw 'Annotation changed executable text' }
    $reference='/reference:'+ $library
    $imports='/imports:System,System.Collections,System.Data,PublicFixture'
    Vbc 'framework2' 'missing-global-imports' @($reference) $annotated 'fail' 'BC30002'
    Vbc 'framework2' 'annotated-framework2' @($reference,$imports) $annotated 'pass'
    Vbc 'framework4' 'annotated-framework4-with-net20-dependency' @($reference,$imports) $annotated 'pass'
    $tripwire=Join-Path $OutRoot 'AnnotatedTripwire.vb'
    [IO.File]::WriteAllText($tripwire,$annotation.Replace('Return DataAccess.Execute(', 'Return MissingAnnotatedMethod.Execute('))
    Vbc 'framework4' 'annotated-input-tripwire' @($reference,$imports) $tripwire 'fail' 'BC30451'
    $controlled=Join-Path $inputs 'AmbiguityControlled.vb'
    Vbc 'framework4' 'controlled-namespace-collision' @($reference,'/imports:System,PublicFixture.Left,PublicFixture.Right') $controlled 'fail' 'BC30561'
    Vbc 'framework4' 'controlled-narrow-import-no-source-edit' @($reference,'/imports:System,PublicFixture.Left') $controlled 'pass'
    $ambiguity=Join-Path $inputs 'Ambiguity.vb'
    foreach($profile in @('framework2','framework4')) {
        $version=if($profile -eq 'framework2'){'v2.0.50727'}else{'v4.0.30319'}
        $framework=Join-Path $env:WINDIR ('Microsoft.NET/Framework64/'+$version)
        $webref='/reference:'+ (Join-Path $framework 'System.Web.dll')
        Vbc $profile ('framework-broad-import-'+$profile) @($webref,'/imports:System.Text,System.Web,System.Web.UI') $ambiguity 'observe' 'BC30561'
        Vbc $profile ('framework-narrow-import-'+$profile) @($webref,'/imports:System.Text,System.Web.UI') $ambiguity 'pass'
    }
    $framework4=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
    $aspnet=Join-Path $framework4 'aspnet_compiler.exe'
    if ((HashFile $aspnet) -ne $inputRecord.projection.aspnetCompilerSha256) { throw 'ASP.NET compiler changed' }
    foreach($name in @('page','appcode','appcode-no-page-import','appcode-explicit')) {
        $site=Join-Path $OutRoot ('host-'+$name)
        $fixture=if($name -eq 'page'){'website-page'}else{'website-appcode'}
        Copy-Item -LiteralPath (Join-Path $inputs $fixture) -Destination $site -Recurse
        [void][IO.Directory]::CreateDirectory((Join-Path $site 'bin'))
        Copy-Item -LiteralPath $library -Destination (Join-Path $site 'bin/dependency-net20.dll')
        $config=Join-Path $site 'Web.config'
        [xml]$xml=[IO.File]::ReadAllText($config)
        if($name -in @('appcode-no-page-import','appcode-explicit')) {
            [void]$xml.configuration.'system.web'.pages.namespaces.RemoveChild($xml.configuration.'system.web'.pages.namespaces.add)
            $xml.Save($config)
        }
        if($name -eq 'appcode-explicit') {
            $node=$xml.CreateElement('compilers');$compiler=$xml.CreateElement('compiler')
            $compiler.SetAttribute('language','vb;vbs;visualbasic;vbscript');$compiler.SetAttribute('extension','.vb')
            $compiler.SetAttribute('type','Microsoft.VisualBasic.VBCodeProvider, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089')
            $compiler.SetAttribute('compilerOptions','/imports:PublicFixture')
            [void]$node.AppendChild($compiler);[void]$xml.configuration.'system.web'.compilation.AppendChild($node);$xml.Save($config)
        }
        $destination=Join-Path $OutRoot ('compiled-'+$name)
        $expected=if($name -eq 'appcode'){'observe'}elseif($name -eq 'appcode-no-page-import'){'fail'}else{'pass'}
        # ASP.NET emits unpredictable assembly filenames. A dedicated directory
        # has to be newly created by this invocation; its DLL is checked below.
        InvokeCompiler ('aspnet-'+$name) $aspnet @('-nologo','-v',('/TraceMapPublic'+$name),'-p',$site,$destination) $destination $expected 'BC30002'
        $dlls=@(Get-ChildItem -LiteralPath $destination -Recurse -Filter '*.dll' -ErrorAction SilentlyContinue)
        $last=$cases[$cases.Count-1]
        if($last.exitCode -eq 0 -and $dlls.Count -eq 0) { throw 'ASP.NET zero exit without fresh assembly' }
    }
    if($cases.Count -ne 15){throw 'Case inventory incomplete'}
    Write-Output 'All public fixture oracles passed; App_Code page-import applicability recorded as observation.'
} finally {
    $same=(($roster | ConvertTo-Json -Depth 8 -Compress) -ceq ((FixtureRoster $inputs) | ConvertTo-Json -Depth 8 -Compress))
    WriteJson (Join-Path $OutRoot 'report.json') ([ordered]@{ schema='vb-compile-context-report/1'; ruleId='validation.vb-compile-context.v1'; jobApplied=$true; generatorSha256=$generator; boundedInputSha256=$inputRecord.boundedInputSha256; compilerProfiles=$inputRecord.projection.compilers; inputFixturesPreserved=$same; annotationExecutableSha256=$execHash; cases=@($cases.ToArray()); jobPeakCommittedBytes=[TraceMapVbPocJob]::Peak(); limitation='public synthetic compilation only; emitted application/SQL code never executed; private conversion and runtime behavior not verified' })
    if(-not $same){throw 'Isolated input fixture changed'}
}
