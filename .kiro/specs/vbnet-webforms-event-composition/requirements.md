# Requirements

Issue #738 extends the Visual Basic adapter with deterministic event and ASP.NET
Web Forms composition. The scanner must retain static evidence for VB `Handles`,
`AddHandler`, `RemoveHandler`, `RaiseEvent`, and `WithEvents` constructs and join
supported Web Forms markup/control declarations to VB code-behind only when the
checked-in syntax and compiler evidence establish the relationship.

The implementation must:

- emit rule-identified, tiered, source-spanned event evidence;
- reuse the shared Web Forms page, control, handler, flow, packet, review, and
  anonymous-report contracts;
- recognize classic non-SDK VB Web Application project and designer partial
  conventions;
- continue under failed or partial compilation with bounded syntax evidence and
  explicit gaps for late binding, ambiguous handlers/partials, unsupported
  delegate shapes, and unavailable framework metadata;
- remain deterministic and privacy-safe by default, retaining no raw source,
  markup values, local absolute paths, SQL, URLs, or diagnostic messages.

It must not claim runtime event ordering, event firing, postback behavior,
reachability, business intent, BRD inference, or modernization generation.
