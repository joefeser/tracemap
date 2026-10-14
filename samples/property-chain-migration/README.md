# Explicit property-chain regression

Public, source-only cross-file event -> shared property -> instance property ->
method -> SQL/bulk fixture. Expected report: 2/2 candidate entry paths, including
property declaration evidence. No getter or operation is executed; property values,
nullability, virtual dispatch and runtime reachability remain unproved.

PropertyChainMigrationTests also covers field chains, missing types, nonshared type
roots, indexed properties, arrays, duplicate members, local shadows, factory calls
and indexed receiver expressions. Inherited/partial member merging is not supported
by this bounded extension; missing declarations remain unresolved.
