Imports System.Web.UI

Public Class ShadowedBase
    Inherits Page
    Protected profileWidget As New InertWidget()
End Class

Public Class ShadowedHost
    Inherits ShadowedBase
End Class
