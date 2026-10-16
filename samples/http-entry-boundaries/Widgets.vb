Imports System
Imports System.Net
Imports System.Web.UI
Imports System.Web.UI.WebControls

' Public synthetic compile-only fixture. Never host or execute this application.
Public Class ProfileWidget
    Inherits UserControl
    Protected WithEvents ApplyButton As New Button()

    Public Sub Apply(sender As Object, e As EventArgs) Handles ApplyButton.Click
        Dim profile As New ProfileRecord()
    End Sub

    Public ReadOnly Property Profile As ProfileRecord
        Get
            Return New ProfileRecord()
        End Get
    End Property
End Class

Public Class DispatchWidget
    Inherits UserControl
    Protected Overrides Sub CreateChildControls()
    End Sub
    Public Sub DirectCurrentClass()
        MyClass.CreateChildControls()
    End Sub
    Public Sub VirtualCandidate()
        Me.CreateChildControls()
    End Sub
End Class

Public Class DerivedDispatchWidget
    Inherits DispatchWidget
    Protected Overrides Sub CreateChildControls()
        RemoteService.Fetch()
    End Sub
End Class

Public Class ProfileRecord
    Public Sub New()
        RemoteService.Fetch()
    End Sub
End Class

Public Class RemoteService
    Public Shared Sub Fetch()
        Using client As New WebClient()
            client.UploadString("https://example.invalid/profile", "POST", "synthetic")
        End Using
    End Sub
End Class

Public Class LifecycleWidget
    Inherits UserControl
    Protected Overrides Sub CreateChildControls()
        MyBase.CreateChildControls()
        Prepare()
    End Sub
    Private Sub Prepare()
        RemoteService.Fetch()
    End Sub
End Class

Public Class InertWidget
    Inherits UserControl
    Protected Overrides Sub CreateChildControls()
        MyBase.CreateChildControls()
    End Sub
End Class

Public Class SameNameWidget
    Public Sub CreateChildControls()
    End Sub
End Class
