Public Class SamplePage
    Private WithEvents Widget As PublicControls.Widget
    Sub Page_Load()
        If Not Page.IsPostBack Then
            With Widget
                AddHandler .Click, AddressOf Clicked
            End With
        End If
    End Sub
    Sub Clicked() Handles Widget.Click
    End Sub
End Class
