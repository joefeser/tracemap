# Inherited VB imports

Public synthetic reproduction of the missing evidence shape: App_Code uses an
unqualified generic type, the site Web.config does not declare a compiler, and
its local namespace collection does not contain System.Collections.Generic.

`FrameworkSignatureCandidateTests` supplies a hash-bound synthetic observer
context and matching Cecil metadata. It proves source-to-compiled/database
candidate recovery, missing/stale context refusal, and the current-environment
warning. The fake provider bytes used by those unit fixtures are intentionally
not native provider proof. The separate Windows capture tests exercise real
framework configuration APIs against synthetic sites. Neither executes the
application or SQL.
