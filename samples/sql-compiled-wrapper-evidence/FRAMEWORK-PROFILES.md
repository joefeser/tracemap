# Public synthetic framework profiles

The C# and VB end-to-end InfoMessage fixtures are independently authored synthetic
examples. Their normal build references framework 4. The test helper
`IlSqlLifecycleExtractorTests.LegacyFrameworkBytes` copies their compiled bytes
and rewrites only the public framework assembly reference versions to the exact
legacy profile (mscorlib/System.Data/System.Xml 2.0.0.0, Microsoft.VisualBasic
8.0.0.0). Copies remain in temporary test directories and are never executed.

Both metadata readers decode the copied bodies and must agree. Extractor and
native compiled-only CLI tests cover the same command, custom-object ArrayList,
real parameter attachment, Fill, event subscription/removal and result naming
allocation identities. Wrong signing key, unsupported version, wrong constructor
parameter, mixed event profiles, sender access, list escape and reuse refuse.
This tests encoded static evidence only, not legacy runtime compatibility.

The public callbacks also exercise `XmlAttributeCollection.get_ItemOf(Int32)` and
`XmlNode.get_InnerXml()` through a statically typed XmlNode local. Named-attribute
access remains covered. Neither tests nor extractor execute the callbacks or
interpret the XML strings; normal/legacy native CLI controls preserve unbound
result names and allocation/execution identity.
