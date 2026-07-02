# Swift HTTP Source Context Requirements

Status: implemented
Public claim level: shipped on dev after PR merge.

## Context

Swift v0 emits `HttpCallDetected` facts for static URLSession,
Alamofire-style, and Moya-style API-client evidence. For route/API flow
inspection, those facts are more useful when the scanner can identify the
source-local Swift declaration that contains the HTTP evidence.

## Requirements

1. Swift HTTP/API client facts SHALL keep `Tier3SyntaxOrTextual` evidence and
   SHALL NOT claim runtime network traffic, endpoint reachability, or semantic
   compiler call resolution.
2. When a `HttpCallDetected` evidence span is contained by a SwiftSyntax
   declaration span in the same file, the fact SHALL set `sourceSymbol` to that
   declaration symbol ID.
3. The fact SHALL include safe source-context metadata:
   `sourceContextStatus`, `containingDeclarationSymbolId`,
   `containingDeclarationDisplayName`, and `containingDeclarationKind`.
4. When no containing declaration is available, the fact SHALL emit
   `sourceContextStatus=unresolved` and SHALL NOT invent a source symbol.
5. Source-context selection SHALL be deterministic and prefer the innermost
   containing declaration.
6. Rule catalog limitations SHALL document that containing declaration context
   is static syntax evidence only.
7. Existing Swift smoke tests SHALL prove URLSession, Alamofire-style, and
   Moya-style HTTP facts carry source-local context when available.
