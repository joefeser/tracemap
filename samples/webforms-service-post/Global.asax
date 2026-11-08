<%@ Application Language="VB" %>
<script runat="server">
Sub Application_Start(sender As Object, e As EventArgs)
    Dim scripts As New ScriptBundle("~/bundles/client")
    scripts.Include("~/Scripts/adapter.js", "~/Scripts/module.js", "~/Scripts/panel.js", "~/Scripts/services.js")
    BundleTable.Bundles.Add(scripts)
End Sub
</script>
