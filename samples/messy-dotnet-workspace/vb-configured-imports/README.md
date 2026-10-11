# Explicit ASP.NET VB compilation imports

Public synthetic source only. Never execute the application or SQL.

`App_Code/Entry.vb` deliberately has no file-level Imports. Its closed generic
parameter depends on the root Web.config namespace collection. `Events.vb`
depends on explicit compiler-option imports for EventArgs and Control. The
configuration explicitly selects the framework VBCodeProvider in System.

The reporting tests generate matching CLR2/CLR4 PE metadata with Cecil, run the
real scanner and bounded combined-index reporter, and require a source-rooted
route to the synthetic provider's database API. An alternate imported namespace
with a compatible overload must remain ambiguous. This is not authentic
aspnet_compiler compilation, runtime execution, or private application proof.

The framework behavior is documented by Microsoft's
[AssemblyBuilder implementation](https://github.com/microsoft/referencesource/blob/main/System.Web/Compilation/AssemblyBuilder.cs):
`FixUpCompilerParameters` invokes `AddVBGlobalNamespaceImports` for the framework
VB provider, using application-level pages/namespaces. This is not permission to
apply directory-local page imports to App_Code or to invent inherited defaults.
