# Microsoft Access VBA Source Export

`Export-AccessVbaSource.ps1` is a separate Windows-only source-export boundary.
It is not used by `tracemap-access scan` or `tracemap-access scan-file`.

It accepts a disposable Access copy plus a compatible protected form/report
metadata bundle and creates:

```text
<output>/private-access-source/            # raw VBA plus form/report definitions
<output>/normalized-design-evidence/      # pass this directory to enrich-design
```

The exporter sets force-disabled automation and invisibility, verifies separate
canaries and original/copy hashes, uses a count-only loaded-module canary, and
fails closed on timeout, visible UI, fired canary, source mutation, or cleanup
failure. It never accesses VBE, recordsets, forms/reports, queries, macros, or
procedures.

The normalized directory is protected local input. `enrich-design` processes
source only in memory; normal facts, indexes, reports, and logs omit raw source
and event-expression text. Static same-module candidates do not prove runtime
behavior. Run the synthetic fixture first; a representative input needs a
separate owner authorization.

On the isolated Windows VM, validate only the synthetic fixture before any
owner-authorized representative discussion:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/access-validation/Invoke-AccessVbaSourceProducerSmoke.ps1 `
  -Generator scripts/access-validation/New-SyntheticAccessFixture.ps1 `
  -MetadataProducer scripts/access-validation/Export-AccessFormReportMetadata.ps1 `
  -VbaProducer scripts/access-validation/Export-AccessVbaSource.ps1 `
  -SmokeRoot C:\TraceMapDev\runs\access-vba-source-smoke
```

The smoke deletes its protected output. It is not an authorization to use a
customer database.
