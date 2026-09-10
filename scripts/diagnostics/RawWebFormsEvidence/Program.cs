using TraceMap.Reporting;

try
{
    if (args.Length is 5 or 6 or 7 or 8 && args[0] == "--code-path-review")
    {
        if (args.Length >= 6 && !int.TryParse(args[5], out _)) throw new InvalidDataException("CodePathReviewInvalidLimit");
        var triggerContextLines = args.Length == 5 ? 12 : int.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);
        var returnHref = args.Length >= 7 && args[6] != "--include-raw-source" ? args[6] : null;
        var includeRawSource = args.Length >= 7 && args[^1] == "--include-raw-source";
        if (args.Length == 8 && args[7] != "--include-raw-source") throw new InvalidDataException("CodePathReviewRawSourceOptionInvalid");
        foreach (var line in WebFormsCodePathReview.Run(args[1], args[2], args[3], args[4], triggerContextLines: triggerContextLines, returnHref: returnHref, includeRawSource: includeRawSource)) Console.WriteLine(line);
        return 0;
    }
    if (args.Length == 4 && args[0] == "--batch-inspection")
    {
        foreach (var line in WebFormsRawEvidenceAudit.Run(args[1], args[2], inspectionPath: args[3], inspectAllHandlers: true)) Console.WriteLine(line);
        return 0;
    }
    if (args.Length == 3 && args[0] == "--database-evidence")
    {
        foreach (var line in WebFormsDatabaseEvidenceAudit.Run(args[1], args[2], Console.WriteLine)) Console.WriteLine(line);
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
        "RawAuditMethodHintInvalid", "RawAuditMethodAmbiguous", "RawAuditMethodNotFound", "RawAuditFillCallerUnavailable",
        "RawAuditNoRecognizedFillHop", "RawAuditFillCallerIdentityMissing", "RawAuditMultipleFillCallers", "RawAuditFillIndexWitnessMissing"];
    safeCodes = [.. safeCodes, "CodePathReviewInvalidLimit", "CodePathReviewCaseInvalid", "CodePathReviewInspectionUnavailable",
        "CodePathReviewSourceRootUnavailable", "CodePathReviewSchemaMismatch", "CodePathReviewCaseUnavailable",
        "CodePathReviewSourcePathInvalid", "CodePathReviewSourceUnavailable", "CodePathReviewExcerptLimit", "CodePathReviewSourceSpanInvalid",
        "CodePathReviewAnonymousLeak", "CodePathReviewReturnLinkInvalid", "CodePathReviewRawSourceOptionInvalid", "CodePathReviewCandidateWorkLimit"];
    var code = error is InvalidDataException && safeCodes.Contains(error.Message, StringComparer.Ordinal)
        ? error.Message : "RawAuditInputOrRuntimeFailure";
    Console.Error.WriteLine($"raw-webforms-evidence=failed;code={code}");
    return 1;
}
