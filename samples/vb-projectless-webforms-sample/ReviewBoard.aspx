<%@ Page Language="VB" CodeFile="ReviewBoard.aspx.vb" Inherits="ReviewBoard" %>
<form id="ReviewForm" runat="server">
  <asp:TextBox ID="FilterText" runat="server" />
  <asp:Button ID="SearchButton" runat="server" Text="Search" OnClick="SearchButton_Click" />
  <asp:Button ID="ToggleButton" runat="server" Text="Toggle" OnClick="ToggleButton_Click" />
  <asp:Button ID="ApproveButton" runat="server" Text="Approve" OnClick="ApproveButton_Click" />
  <asp:GridView ID="ResultsGrid" runat="server" />
</form>
<script>
$("[id*=FilterText]").on("keyup", function () {
  $("[id*=SearchButton]").prop("disabled", $(this).val().length === 0);
});
</script>
