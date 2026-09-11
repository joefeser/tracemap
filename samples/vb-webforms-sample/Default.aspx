<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Default.aspx.vb" Inherits="VbWebFormsSample._Default" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Synthetic Order Desk</title>
</head>
<body>
    <form id="OrderDeskForm" runat="server">
        <div>
            <asp:Label ID="HeadingLabel" runat="server" Text="Synthetic order desk" />
        </div>
        <div>
            <asp:TextBox ID="OrderNumberBox" runat="server" />
            <asp:Button ID="SaveButton" runat="server" Text="Save" />
            <asp:Button ID="RefreshButton" runat="server" Text="Refresh" />
        </div>
        <div>
            <asp:Label ID="StatusLabel" runat="server" />
        </div>
    </form>
</body>
</html>
