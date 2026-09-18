# Historical `dotnetperf` Validation-Corpus Runway

## Purpose

The historical `dotnetperf` repository is a candidate public validation corpus
for TraceMap's .NET source, metadata, PDB, and IL identity model. It is a strong
legacy Cecil/.NET Framework rewriting corpus, not a comprehensive modern
ECMA-335 or cross-language corpus.

Tracking issue: [#759](https://github.com/joefeser/tracemap/issues/759).

Pin all investigation to commit:

```text
db8c3359badfec620ccdc6df062b1756ef9607f8
```

The inspected repository contains approximately 34,606 declared MSTest methods.
Most are generated control-flow matrices, so the method count must not be
reported as independent semantic coverage.

## Useful Coverage

- branch legality across exception-handling regions;
- `switch`, `leave`, and protected-block control flow;
- branch retargeting after instrumentation;
- nested and generic runtime identities;
- duplicate assembly identity cases;
- raw IL fixtures, netmodules, and checked-in assembly/PDB inputs; and
- old MSBuild, aliases, reflection-heavy code, WPF/XAML, properties, and events.

## Known Blind Spots

- no Visual Basic or F# source corpus;
- incomplete metadata-table, token, blob, and evaluation-stack coverage;
- little or no modern signature, function-pointer, `calli`, custom-modifier,
  portable-PDB, SourceLink, type-forwarding, or modern-runtime coverage;
- no general hostile-PE corpus;
- historical strong-name and credential material must be treated as
  compromised; and
- the full legacy build is not expected to run safely or reproducibly on the
  current macOS development environment.

## Identity Requirements

TraceMap must keep these as separate identities connected by evidence-backed
edges:

- source symbol;
- compiled metadata member;
- PDB method/document occurrence; and
- rewritten method body or member.

Do not collapse them into a shared display-string identity. Regression cases
must include:

- operand-insensitive method-body hashes;
- nested-type `/` versus `+` naming;
- same-name and same-arity overloads;
- properties and events versus generated accessors;
- constructed generic identities versus generic definitions; and
- duplicate assemblies containing similar-looking types.

## Companion Public Fixtures

`dotnetperf` cannot prove language equivalence. Maintain a purpose-built
C#/Visual Basic/F# matrix that compiles equivalent CLR shapes and tests exact
metadata signatures. Add sanitized Web Forms fixtures for:

- project/default imports such as `Odbc.OdbcCommand`;
- inherited receiver fields and failed or unavailable base projects;
- `ArrayList` and derived parameter collections;
- overload identity and resolution; and
- page handler to business layer to inherited data access to database terminal
  traversal.

Golden assertions should cover rule IDs, evidence tiers, identity provenance,
receiver-bridge and surface-evidence edges, terminal kind/count, and explicit
gap or truncation classifications. Artifact byte size is diagnostic context,
not an acceptance contract.

## Staged Windows Validation

Use an isolated Windows x64 environment and begin with the smallest proof:

```bat
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe" /NOLOGO /DLL /DEBUG /OPTIMIZE /OUTPUT:"%TEMP%\dotnetperf-ret.dll" "src\NetPerf.Tests.Unit\Test Data\instr_ret.il.txt"
```

Then proceed in bounded stages:

1. Record Windows, Visual Studio, MSBuild, .NET Framework, ILAsm, and test-runner
   versions.
2. Run the ILAsm fixture smoke without altering project files.
3. Build the smallest viable project/test slice.
4. Run representative branch, switch, exception-region, `leave`, instrumentation,
   nested/generic, and duplicate-identity tests.
5. Capture exact commands, exit codes, logs, selected tests, output paths, and
   blockers.
6. Add public TraceMap regression fixtures only after the expected identity and
   evidence contract is documented.
7. Run the complete historical corpus only after the bounded slices are stable.

Do not push modernization changes from the validation environment. Do not use
or disclose historical keys, credentials, or signing material.
