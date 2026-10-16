<%@ Page Language="VB" CodeFile="ShadowedHost.aspx.vb" Inherits="HttpEntrySamples.ShadowedHost" %>
<%@ Register TagPrefix="demo" Namespace="HttpEntrySamples" Assembly="HttpEntryBoundaries" %>
<demo:ProfileWidget ID="profileWidget" runat="server" />
<%# profileWidget.Profile %>
