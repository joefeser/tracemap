<%@ Application Language="VB" %>
<%@ Import Namespace="System.Web.Optimization" %>
<script runat="server">
Sub Application_Start(sender As Object, e As EventArgs)
    Dim first As New ScriptBundle("~/bundles/first")
    Dim second = New ScriptBundle("~/bundles/second")
    first.Include("~/Scripts/a.js")
    second.Include("~/Scripts/b.js")
    FIRST.Include("~/Scripts/c.js")
    BundleTable.Bundles.Add(first)
    BundleTable.Bundles.Add(second)
End Sub
</script>
