<%@ Page Language="VB" MasterPageFile="Layout.master" %>
<asp:Content runat="server" ContentPlaceHolderID="Main">
  <section ng-controller="Panel">
    <button ng-click="save()">First</button>
    <button ng-click="save()">Second</button>
  </section>
</asp:Content>
