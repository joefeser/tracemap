<%@ Page Language="C#" CodeBehind="Engines.aspx.cs" Inherits="Alpha.Pages.EnginesPage" %>
<html>
<body>
  <form runat="server">
    <asp:Button ID="DeepButton" runat="server" OnClick="DeepButton_Click" Text="Run deep report" />
    <asp:Button ID="LoopButton" runat="server" OnClick="LoopButton_Click" Text="Run loop report" />
    <asp:Button ID="SelfButton" runat="server" OnClick="SelfButton_Click" Text="Run self report" />
    <asp:Button ID="EnginesButton" runat="server" OnClick="EnginesButton_Click" Text="Run all engines" />
    <asp:Button ID="AmbiguityButton" runat="server" OnClick="AmbiguityButton_Click" Text="Exercise overloads and uncertain receiver" />
  </form>
</body>
</html>
