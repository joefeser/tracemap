# Windows VB compile-context POC

This independently authored public fixture qualifies an isolated compiler harness.
It does not validate an employer conversion, infer source/DLL correspondence or
resolve an opaque SQL wrapper. No application methods or SQL are executed. Only
compilers and the standard ASP.NET precompilation pipeline run; no IIS site,
application startup, custom build provider or production configuration is used.

Run on 64-bit Windows with PowerShell 7 and the existing .NET Framework 2 and 4
compiler/ASP.NET installations. The fixture deliberately tests both installed
compiler profiles; it does not install tools or select a private project's target.

```powershell
pwsh -NoProfile -File ./scripts/tests/Test-VbCompileContext.ps1 -OutRoot "$env:TEMP/tracemap-vb-context-new-attempt"
```

Fixture intake reads only nine known files, at most 64 KiB each; unknown files
are not copied. Output hashing is capped at 256 files and 16 MiB per file.
The output must be fresh and outside Git; previous runs refuse overwrite. Source
fixtures remain unchanged. All compiler children run inside a Windows Job Object
with 2048 MiB aggregate committed-memory and 16-process limits. The parent stops
the whole worker after 120 seconds. No process unrelated to this owned worker is
stopped. Prerequisite failures can stop before compilation/report creation.

## What it checks

| Case | Required result |
| --- | --- |
| Compile complete public ADO wrapper with VB 8 / framework 2 references | Fresh DLL; CLR2 metadata marker present |
| Annotated four-argument consumer without global imports | BC30002, no output DLL |
| Same annotated consumer with explicit fixture imports, VB 8 | Fresh DLL |
| Same annotated consumer, framework 4 compiler with framework 2 dependency | Fresh DLL |
| Corrupt only the annotated input with an undefined method owner | BC30451, no output DLL |
| Two public namespaces deliberately exporting the same nested name | BC30561, no output DLL |
| Narrow imports, same source and same compiler | Fresh DLL |
| Broad/narrow framework namespace probes under each compiler | Record broad outcome; narrow must compile |
| ASP.NET page with explicit fixture page namespace | Fresh compiled assembly |
| ASP.NET App_Code with that page namespace | Record observed host behavior |
| Same App_Code without the fixture page namespace | BC30002, no output assembly |
| Same App_Code with explicit compiler import and no fixture page namespace | Fresh compiled assembly |

There are 15 compiler/precompiler entry-point invocations. ASP.NET can invoke
additional compiler children internally; the runner does not claim an exact
child-compiler invocation census. No help probes or searches across candidate
compiler versions are needed. Compiler full paths remain local; file versions and
SHA-256 identities are recorded separately from framework folders/fixture inputs.

The public dependency has an inspected implementation: it constructs real
SqlParameter instances, attaches them to a SqlCommand and contains an adapter Fill
path. Compilation never calls it. This is not a stub standing in for an employer
wrapper. The public method uses four custom parameter objects in an ArrayList,
null/empty normalization and integer conversion. Its annotated copy differs only
in comments; a normalized executable-text hash and failing annotated-input
tripwire bind which source actually entered compilation. Text normalization is a
fixture assertion, not a general semantic-equivalence algorithm.

## Results and limits

Windows validation on 2026-11-05 observed VB compiler file versions
8.0.50727.9177 and 14.8.9221.0. All 15 case oracles passed. The final run took 6688 ms and peaked at
168521728 bytes (about 161 MiB) aggregate Job committed memory,
under the 120-second / 2048 MiB caps. Existing-output refusal also passed. Both compilers accepted
the public consumer with imports; the newer compiler accepted the CLR2 dependency.
The actual framework broad-import probes compiled on both profiles, so they did
NOT reproduce the private framework ambiguity. The controlled public collision
DID reproduce BC30561 and was fixed through imports without source edits.

On this Windows host, ASP.NET's page namespace DID apply to the public App_Code
compilation. Removing that single fixture namespace caused BC30002; explicit
compiler imports restored success. This is a causal public fixture result, not
proof of the effective options of a different website, inherited configuration,
CodeDom provider, framework installation or retained private compiler invocation.
Do not copy a namespace list or switch compilers/reference assemblies merely to
obtain a green private build. Recover that site's actual applicable context.

Earlier POC failures were retained: one over-specific expectation that the actual
framework probe would fail, and a fixture assembly-name mismatch caused by copying
a DLL under a different filename. The final fixture preserves the assembly name.
Those were POC development failures, not employer conversion findings.

## Local artifacts

- `input.json`: public fixture file hashes, compiler identities and declared caps.
- `report.json`: case outcomes, compiler exits, fresh output/log hashes, annotation
  executable-text hash and measured Windows Job peak committed bytes.
- `guard.json`: timeout/worker exit, elapsed time and original-fixture preservation.
- Named logs, isolated annotated/tripwire inputs, host copies and compiled outputs.

Each JSON artifact carries rule `validation.vb-compile-context.v1`, the exact runner
SHA-256 and the SHA-256 of its bounded public input projection. Raw compiler logs
contain local paths; retain them locally, outside Git. A missing report, nonzero
worker exit, timeout, changed input or failed case oracle is not a qualified run.
Success does not mean that the private conversion compiles or runs successfully.
