Option Strict On
Option Explicit On

' Synthetic Web Forms code-behind for future event-composition work (#738).
' TraceMap #736 makes no claim that Handles, AddHandler, RemoveHandler,
' RaiseEvent, or WithEvents relationships are composed into event edges yet.

Partial Public Class _Default
    Inherits System.Web.UI.Page

    ' WithEvents field with a custom event source handled through Handles.
    Private WithEvents _notifier As New StatusNotifier()

    ' Page lifecycle: Init.
    Protected Sub Page_Init(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Init
        ' Dynamic control-event wiring for the refresh path.
        AddHandler RefreshButton.Click, AddressOf RefreshButton_Click
    End Sub

    ' Page lifecycle: Load.
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If Not IsPostBack Then
            StatusLabel.Text = "Ready"
        End If
    End Sub

    ' Page lifecycle: PreRender.
    Protected Sub Page_PreRender(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.PreRender
        HeadingLabel.Text = "Synthetic order desk (" & StatusLabel.Text & ")"
    End Sub

    ' Declarative control event handled through Handles on a designer field.
    Protected Sub SaveButton_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles SaveButton.Click
        Dim orderNumber As String = OrderNumberBox.Text.Trim()
        _notifier.Announce("saved " & orderNumber)
    End Sub

    ' Dynamically attached handler target (AddHandler above).
    Private Sub RefreshButton_Click(ByVal sender As Object, ByVal e As System.EventArgs)
        _notifier.Announce("refreshed")
    End Sub

    ' Detach the dynamic handler when the page is unloaded.
    Protected Sub Page_Unload(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Unload
        RemoveHandler RefreshButton.Click, AddressOf RefreshButton_Click
    End Sub

    ' Custom event raised by the notifier and handled through Handles.
    Private Sub Notifier_StatusChanged(ByVal sender As Object, ByVal e As StatusChangedEventArgs) Handles _notifier.StatusChanged
        StatusLabel.Text = e.Message
    End Sub

End Class
