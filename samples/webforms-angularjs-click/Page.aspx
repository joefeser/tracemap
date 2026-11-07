<%@ Page Language="VB" MasterPageFile="~/Site.master" %>
<asp:Content runat="server" ContentPlaceHolderID="Main">
  <section ng-controller="EditorController">
    <button ng-click="save(item)">Save</button>
  </section>
</asp:Content>
