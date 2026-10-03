# Projectless VB Web Forms sample

This synthetic fixture models an ASP.NET Web Site: it intentionally has no
`.vbproj` or solution file. `CodeFile` links the page markup to its VB
code-behind, while `App_Code` supplies a shared helper.

The fixture keeps three bounded shapes separate:

- an inline jQuery binding beside a server event;
- a handler with UI-only state changes and no method call;
- handlers with direct framework-shaped and `App_Code` calls.

It exists to validate projectless discovery and evidence correlation. It does
not prove that an arbitrary Web Site compiles, that an event fires at runtime,
or that a retained call reaches a supported terminal.
