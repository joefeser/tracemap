# TraceMap Web Forms extraction readiness summary

## Bottom line

Nine .NET scopes were reviewed. None contained positive Web Forms inventory,
event-binding, resolved-handler, event-flow, or classic ASP.NET facts. One
scope reported full semantic analysis and a successful build; the other eight
were reduced or partial. Reduced compilation therefore cannot, by itself,
explain the complete absence of Web Forms evidence.

Four scopes contained 11 WinForms event-binding facts in total. None also
contained positive Web Forms inventory, so the retained evidence does not prove
that TraceMap misclassified a Web Forms event as WinForms.

## What remains unknown

The aggregate evidence cannot distinguish among these cases:

- the intended Web Forms source was not configured;
- the configured repository root did not contain the Web Forms application;
- include or exclude scope omitted the markup;
- Web Forms candidate files were inventoried but the extractor emitted no
  corresponding facts.

The next pass should not rescan. It should identify the intended source locally
and compare its existing `FileInventoried` categories with its configured scan
scope. Private source labels, paths, filenames, and glob values must remain in
the restricted environment.

## Limitations

This summary was reconstructed from owner-supplied photos of a sanitized local
result. It is planning evidence, not proof of application completeness,
runtime behavior, source identity, or an extractor defect.
