Imports System.Web.Optimization

Public Module BundleConfig
    Public Sub RegisterBundles(bundles As BundleCollection)
        bundles.Add(New ScriptBundle("~/bundles/app").Include(
            "~/Scripts/global.js",
            "~/Scripts/main.js"))
    End Sub
End Module
