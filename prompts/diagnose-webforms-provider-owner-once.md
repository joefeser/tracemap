# Identify the provider blocking module resolution

> Identify the specific provider producing AngularServiceModuleUnavailable and the failed ownership check. Return its synthetic shape. Keep provider ordering and scanner code unchanged.

Use implementation `0e53bd94741d00f75dbdc304e9bedf64b9037ad5` or a docs-only descendant. The retained gap points to the click's service call. Use existing diagnostic evidence if available; otherwise, when authorized, use a local debugger on one fresh static scan with the same source selection and mount. Break at the AngularServiceModuleUnavailable throw in AngularJsScript.ResolveService for the affected token.

1. Is this the intended service or another provider? Identify the registration method and whether its first argument is the exact target literal, a computed value, or absent. Other literal tokens are filtered earlier.
2. Inspect the receiver before ServiceReceiver, the factory count after unwinding, and the remaining root expression type. Check whether the registration is a top-level expression statement.
3. Inspect ResolveModule in that frame. Identify the first failed condition: global angular binding or mutation, unsupported root, alias mutation or conflicting declaration, alias declaration count across included scripts, or unsupported module initializer.
4. Return only the provider category, token classification, factory count, root shape and first failed check. Keep private source, identifiers, paths and detailed debugger output local. Stop at the diagnosis.
