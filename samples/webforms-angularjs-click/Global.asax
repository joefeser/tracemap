<%@ Application Language="VB" %>
<script runat="server">
Sub Application_Start(sender As Object, e As EventArgs)
    Dim scripts As New ScriptBundle("~/bundles/client")
    scripts.Include("~/Scripts/module.js", "~/Scripts/editor.js", "~/Scripts/other.js")
    BundleTable.Bundles.Add(scripts)
End Sub
</script>
