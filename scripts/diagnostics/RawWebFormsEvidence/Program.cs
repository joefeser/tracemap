using TraceMap.Reporting;

try
{
    if (args.Length != 2) throw new InvalidDataException();
    foreach (var line in WebFormsRawEvidenceAudit.Run(args[0], args[1])) Console.WriteLine(line);
    return 0;
}
catch (Exception error)
{
    // Do not echo native SQLite exceptions, private paths, symbols, or JSON values.
    string[] safeCodes = ["RawAuditInvalidLimit", "RawAuditReportLimit", "RawAuditSchemaMismatch",
        "RawAuditSourceMismatch", "RawAuditProvenanceMismatch", "RawAuditHandlerLimit",
        "RawAuditHandlerNotFound", "RawAuditHandlerSymbolMissing", "RawAuditInputLimit", "RawAuditTextLimit"];
    var code = error is InvalidDataException && safeCodes.Contains(error.Message, StringComparer.Ordinal)
        ? error.Message : "RawAuditInputOrRuntimeFailure";
    Console.Error.WriteLine($"raw-webforms-evidence=failed;code={code}");
    return 1;
}
