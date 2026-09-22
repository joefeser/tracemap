<%@ Page Language="C#" CodeBehind="Engines.aspx.cs" Inherits="Alpha.Pages.EnginesPage" %>
<html>
<body>
  <form runat="server">
    <asp:Button ID="DeepButton" runat="server" OnClick="DeepButton_Click" Text="Run deep report" />
    <asp:Button ID="LoopButton" runat="server" OnClick="LoopButton_Click" Text="Run loop report" />
    <asp:Button ID="EnginesButton" runat="server" OnClick="EnginesButton_Click" Text="Run all engines" />
  </form>
</body>
</html>
