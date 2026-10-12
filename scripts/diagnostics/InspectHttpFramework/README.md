# Offline work-machine framework inspection

Rule `diagnostic.http-framework-inspection.v1` records Tier2Structural literal XML
and encoded metadata evidence; missing/unreadable/unresolved inputs are Tier4Unknown.
This is a local diagnostic, not a TraceMap scan/index or semantic API classifier.
Application-specific compatibility is **pending work-machine validation**. Public
net20/net35/net40/net45/net48 fixtures are representative targets, not confirmed
application targets. Completing this private inspection does not block the public branch.

Build this **public tool** using .NET 10, then run the built DLL on the work machine.
The inspection invocation has no network/upload facility. It never builds/evaluates
the application, follows project imports/external config, loads application assemblies,
resolves dependencies, reads symbols, or executes fixture/application methods.
Only explicitly selected project/config/binary files are read. It writes one new
local Markdown file and refuses to overwrite an existing file. Keep that file on
the work machine; return only a manually reviewed sanitized summary.

PowerShell example (replace the placeholder paths locally; never send them back):

```powershell
# Build only the public collector, before offline inspection.
dotnet build .\scripts\diagnostics\InspectHttpFramework\InspectHttpFramework.csproj -c Release -warnaserror

# Create a local output directory if needed; output filename must be new.
New-Item -ItemType Directory -Force C:\LOCAL\EVIDENCE | Out-Null

dotnet .\scripts\diagnostics\InspectHttpFramework\bin\Release\net10.0\InspectHttpFramework.dll `
  --repo-label local-application --commit-sha unknown `
  --project C:\LOCAL\APP\Application.csproj `
  --config C:\LOCAL\APP\Web.config `
  --binary C:\LOCAL\APP\bin\Application.dll `
  --out C:\LOCAL\EVIDENCE\framework-inspection-001.md
```

Repeat `--project`, `--config`, `--binary` for each explicitly admitted selection,
including VB projects and both root/nested Web.config files where relevant. Include
only binaries admitted for this inspection. Do not recursively discover private
inputs. Use a generic public-safe `--repo-label`; supply the admitted source's
40-character lowercase commit if known, or `unknown`. The supplied commit is not
verified binary/source provenance or historical build authenticity. The tool must
not silently substitute its own checkout SHA for the application's source commit.

If the work machine has no .NET 10 runtime, prepare a public self-contained tool
on an authorized build machine, then transfer **only the public tool** inward:

```powershell
dotnet publish .\scripts\diagnostics\InspectHttpFramework\InspectHttpFramework.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false `
  -o C:\LOCAL\PUBLIC-TOOL
# On the work machine, invoke PUBLIC-TOOL\InspectHttpFramework.exe with the same options.
```

Choose the runtime identifier for the work machine's architecture. Build/restore
of this public tool may need package downloads; the collector invocation does not.
Do not transfer application binaries/source/configuration outward to run it here.
Self-contained deployment is an alternative, not a claim of work-machine validation.

The local report keeps these sources separate:

- Project TargetFramework, TargetFrameworks and TargetFrameworkVersion, with input
  alias, XML line and conditional flag. Conditions/property expressions/imports are
  not evaluated. Invalid/arbitrary values are suppressed. Multiple literal values
  stay visible; multi-targeting is not automatically treated as a conflict.
- Each selected Web.config's system.web compilation/httpRuntime targetFramework.
  Missing settings remain explicit. location/inheritance/server configuration and
  external configSource are not resolved; no effective configuration is asserted.
- Binary TargetFrameworkAttribute, including missing/multiple/unrecognized states.
  No attribute means unknown, even if System or mscorlib version is 2.0.0.0.
- Allowlisted framework AssemblyRef names, versions, culture and public-key tokens,
  and selected HTTP MemberRef declaring types, method names, generic arity,
  calling convention, parameter/return types and encoded assembly scope.
  App/private types are replaced by `<unresolved-or-private-type>`; non-framework
  scopes are redacted. These are encoded references, not resolved declarations,
  trusted API support, an IL call census, runtime use or execution evidence.

Selection is bounded: 64 inputs, 2 MiB per XML file, 64 MiB per binary, 256 MiB
aggregate inspected bytes and 10,000 MemberRefs per binary. Refused/missing/unreadable
inputs and overflow remain explicit. XML DTDs/external entities are prohibited;
selected symbolic-link files are refused. No input paths, filenames, source/config
snippets, arbitrary TFM expressions, credentials, payloads or app type names are
retained. Do not share the local report anyway: its private input hashes and selected
reference/signature inventory remain local evidence. Its bounded-input hash covers
inspected input byte hashes and gap outcomes, not a whole tree or authentic build.
Generator/dependency hashes identify the inspector that produced it.

Missing, conditional, differing or conflicting values require human reconciliation
on the work machine. Do not choose the newest version, majority value, or assembly
version as application TFM. Source declarations and a binary can come from different
builds. Use [SANITIZED-SUMMARY.md](SANITIZED-SUMMARY.md) for the return handoff.
