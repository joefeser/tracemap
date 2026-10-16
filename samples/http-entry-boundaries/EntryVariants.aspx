<%@ Page Language="VB" CodeFile="Host.aspx.vb" Inherits="HttpEntrySamples.HostPage" %>
<%@ Register TagPrefix="local" TagName="Profile" Src="Profile.ascx" %>
<local:Profile ID="first" runat="server" />
<local:Profile ID="second" runat="server" />
<%# first.Profile %>
<%# second.Profile %>
<%# missing.Profile %>
<%# first.Missing %>
<%# Eval("Profile") %>
<%# first.Profile.Name %>

<%# first.WriteOnlyValue %>
<%# first.HiddenValue %>
<%# first.RestrictedValue %>
