using Microsoft.CodeAnalysis;

namespace TraceMap.Core;

internal static class WebFormsFrameworkEntry
{
    internal static void AddWitness(IDictionary<string, string> properties, SemanticModel model,
        ISymbol target, string relationshipKind)
    {
        if (relationshipKind != "Overrides" || target is not IMethodSymbol method
            || method.IsStatic || !method.ReturnsVoid
            || method.ContainingType.ToDisplayString() is not ("System.Web.UI.Control" or "System.Web.UI.TemplateControl" or "System.Web.UI.UserControl" or "System.Web.UI.Page")
            || method.ContainingAssembly.Identity.Name != "System.Web"
            || Convert.ToHexStringLower(method.ContainingAssembly.Identity.PublicKeyToken.ToArray()) != "b03f5f7f11d50a3a"
            || model.Compilation.GetMetadataReference(method.ContainingAssembly) is not PortableExecutableReference)
            return;
        var supported = method.Name == "CreateChildControls" && method.Parameters.Length == 0
            || method.Name is "OnInit" or "OnLoad" or "OnPreRender" && method.Parameters.Length == 1
                && method.Parameters[0].RefKind == RefKind.None
                && method.Parameters[0].Type.ToDisplayString() == "System.EventArgs";
        if (supported)
        {
            properties["webFormsFrameworkEntry"] = method.Name;
            properties["webFormsFrameworkAssemblyIdentity"] = method.ContainingAssembly.Identity.ToString();
        }
    }
}
