# Access Form/Report Metadata Requirements

## Goal

Produce and compose bounded Microsoft Access form/report definition metadata.
The slice explains declared data bindings; it does not reconstruct layout or
claim runtime behavior.

## Requirements

1. A Windows-only producer may serialize saved form/report definitions from an
   explicitly selected disposable database copy using `Application.SaveAsText`.
   It must force-disable automation security, remain invisible, observe loaded
   form/report state before and after each export, and fail closed on a visible
   window, canary activation, source mutation, timeout, or cleanup failure.
2. The producer may enumerate form/report identities and serialize definitions.
   It must not read rows or recordsets, execute queries, render/open forms or
   reports, invoke events, read VBA source, or export macro bodies.
3. Raw serialized definitions are protected source-neutral input. They must
   never enter standard facts, indexes, reports, logs, flow output, vault,
   combine, release publication, or public artifacts.
4. The parser shall project only functional metadata:
   - surface `RecordSource`, `Filter`, `OrderBy`, module presence;
   - control `ControlSource`, `RowSource`, `RowSourceType`,
     `ValidationRule`, `BoundColumn`, bounded column count;
   - subform/subreport `SourceObject`, `LinkMasterFields`,
     `LinkChildFields`;
   - report group/sort field or expression metadata.
5. Visual coordinates, dimensions, colors, fonts, borders, images, captions,
   themes, formatting, tab order, accessibility, screenshots, and OCR are
   explicitly excluded.
6. Query output metadata shall connect direct output identifiers to source
   table fields where static SQL shape proves a unique candidate. Aliases,
   expressions, wildcards, dynamic SQL, ambiguous columns, and unsupported
   query shapes shall remain partial with rule-backed gaps.
7. Standard artifacts remain hash-only. An explicitly requested identity
   projection may retain owner-local form/report/control and direct binding
   identifiers only in an independently deletable output with claim level
   `hidden`; it shall not be accepted by combine, vault, public-site, or
   release-publication paths.
8. Every fact/path must preserve rule ID, tier, coverage, limitation,
   commit/snapshot identity, extractor version, coordinate or container span,
   and supporting fact identity.
9. Mac tests use synthetic text/structured evidence only. Windows validation
   uses only the established isolated Parallels synthetic fixture, never a
   representative or private database.
10. The implementation shall not depend on unmerged PR #564.

## Non-claims

The evidence does not prove rendering, event firing, navigation, query
execution, row access, runtime reachability, data state, business intent,
correctness, completeness, production use, or release approval.
