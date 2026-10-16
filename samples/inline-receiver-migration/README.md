# Inline receiver migration regression

Independent public source-only VB fixture: an observed Handles event calls a
freshly constructed receiver with one omitted trailing Optional argument, then
reaches HTTP, SQL-command and bulk-write operation candidates. Expected integrated
report: three groups, three candidate entry paths. No runtime, hosting, overload
binding, SQL contract or framework ancestry proof is claimed; no method executes.

InlineReceiverMigrationTests covers supplied/omitted optional arguments, required
omissions, named omissions, ParamArray expansion, competing overloads and old facts
without optional metadata. Reversing fact order must preserve report output.
