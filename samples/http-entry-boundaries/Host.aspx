<%@ Page Language="VB" CodeFile="Host.aspx.vb" Inherits="HttpEntrySamples.HostPage" %>
<%@ Register TagPrefix="demo" Namespace="HttpEntrySamples" Assembly="HttpEntryBoundaries" %>
<demo:ProfileWidget ID="profileWidget" runat="server" />
<demo:LifecycleWidget ID="lifecycleWidget" runat="server" />
<%# profileWidget.Profile %>
