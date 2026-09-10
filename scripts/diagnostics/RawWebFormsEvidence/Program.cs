using TraceMap.Reporting;

try
{
    if (args.Length == 3 && args[0] == "--database-evidence")
    {
        foreach (var line in WebFormsDatabaseEvidenceAudit.Run(args[1], args[2])) Console.WriteLine(line);
        return 0;
    }
    if (args.Length is not (2 or 3 or 4)) throw new InvalidDataException();
    foreach (var line in WebFormsRawEvidenceAudit.Run(args[0], args[1], inspectionPath: args.Length >= 3 ? args[2] : null, startingMethodName: args.Length == 4 ? args[3] : null)) Console.WriteLine(line);
    return 0;
}
catch (Exception error)
{
    // Do not echo native SQLite exceptions, private paths, symbols, or JSON values.
    string[] safeCodes = ["RawAuditInvalidLimit", "RawAuditReportLimit", "RawAuditSchemaMismatch",
        "RawAuditSourceMismatch", "RawAuditProvenanceMismatch", "RawAuditHandlerLimit",
        "RawAuditHandlerNotFound", "RawAuditHandlerSymbolMissing", "RawAuditInputLimit", "RawAuditTextLimit", "RawAuditInspectionUnavailable",
        "RawAuditMethodHintInvalid", "RawAuditMethodAmbiguous", "RawAuditMethodNotFound", "RawAuditFillCallerUnavailable"];
    var code = error is InvalidDataException && safeCodes.Contains(error.Message, StringComparer.Ordinal)
        ? error.Message : "RawAuditInputOrRuntimeFailure";
    Console.Error.WriteLine($"raw-webforms-evidence=failed;code={code}");
    return 1;
}
