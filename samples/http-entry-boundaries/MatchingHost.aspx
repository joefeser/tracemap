<%@ Page Language="VB" CodeFile="MatchingHost.aspx.vb" Inherits="HttpEntrySamples.MatchingHost" %>
<%@ Register TagPrefix="demo" Namespace="HttpEntrySamples" Assembly="HttpEntryBoundaries" %>
<demo:ProfileWidget ID="profileWidget" runat="server" />
<%# profileWidget.Profile %>
