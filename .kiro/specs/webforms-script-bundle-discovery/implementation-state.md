# Implementation state

Branch `codex/webforms-sql-evidence` integrates the bundle and AngularJS rules
against dev. The script-bundle fixtures cover both C# and VB declarations,
ordered literal members and master-page references. Additional fixtures cover
VB ASAX locals, operator IIS mounts, AngularJS controller and service scope,
and independent POST-to-handler evidence. See docs/VALIDATION.md and each
fixture README for commands and expected outputs.

Registration, source member, POST, handler declaration and click selection
have separate fact identities. A truncated provider search preserves an
independent registration and its member evidence. It prevents click-selected
endpoints. Runtime injection, callback invocation, SQL execution and whole
application coverage remain outside these static results.
