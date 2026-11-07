<%@ Page Language="VB" MasterPageFile="~/Site.master" %>
<asp:Content runat="server" ContentPlaceHolderID="Main">
  <section ng-controller="EditorController">
    <button ng-click="save(item)">Save</button>
  </section>
  <script type="text/javascript">
    var showHints = <%= Preferences.ShowHints %>;
    var pageSize = <%= Settings.Paging.Size %>;
    var mode = <%= 1 %>;
  </script>
</asp:Content>
