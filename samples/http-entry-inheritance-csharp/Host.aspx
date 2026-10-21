<%@ Page Language="C#" CodeFile="Widgets.cs" Inherits="HttpInheritance.Host" %>
<%@ Register TagPrefix="demo" Namespace="HttpInheritance" Assembly="HttpEntryInheritance" %>
<demo:BaseWidget ID="direct" runat="server" />
<demo:DerivedWidget ID="inherited" runat="server" />
<%# direct.Profile %>
<%# inherited.Profile %>
