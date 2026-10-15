<%@ Page Language="VB" CodeFile="Page.aspx.vb" Inherits="SamplePage" %>
<sample:Widget ID="Widget" runat="server" />
<script>__doPostBack('Widget', ''); __doPostBack(dynamicTarget, '');</script>
