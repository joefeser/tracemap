# Mixed desktop framework signature boundary

Public synthetic source root for `FrameworkSignatureCandidateTests`. The test
creates independent website/provider PE files with Cecil, scans them with both
metadata/IL readers, enters through a synthetic publish receipt, combines the
index and generates normal paths, retained method graphs and HTML reports.
No proprietary code, work-created patch, production identifiers or binaries are
included. No database connection or application method is executed.

The essential case is a website caller using desktop framework 4.0 type scopes
and a provider built against 2.0 type scopes. The provider assembly's own identity
is identical in the caller reference and target definition; only embedded
parameter/return framework assembly versions differ. Tests also cover the reverse
direction without claiming runtime compatibility.

These are synthetic PE fixtures, **not output from aspnet_compiler**. The source
root is an indexing input, not a standalone runnable website. Synthetic receipt
markers exercise report admission; they are not verified publication receipts.
The bound-provenance case explicitly mutates a fixture fact to test that branch;
it is not a producer test for authentic source/build provenance.

Run from the repository root:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~FrameworkSignatureCandidateTests
```

See [the matrix](../../../docs/validation/framework-signature-candidates.md)
for coverage, evidence boundaries and remaining Windows validation.
