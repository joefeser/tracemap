Option Strict On
Option Explicit On

' Generated-looking VB application-framework designer file
' ("My Project\Application.Designer.vb"). Synthetic content in the classic shape.

Namespace My

    Partial Friend Class MyApplication
        Inherits Global.Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase

        <Global.System.Diagnostics.DebuggerStepThroughAttribute()> _
        Public Sub New()
            MyBase.New(Global.Microsoft.VisualBasic.ApplicationServices.AuthenticationMode.Windows)
            Me.IsSingleInstance = False
            Me.EnableVisualStyles = True
            Me.SaveMySettingsOnExit = True
            Me.ShutdownStyle = Global.Microsoft.VisualBasic.ApplicationServices.ShutdownMode.AfterMainFormCloses
        End Sub

        <Global.System.Diagnostics.DebuggerStepThroughAttribute()> _
        Protected Overrides Sub OnCreateMainForm()
            Me.MainForm = New Global.VbLegacyCatalog.CatalogHostForm()
        End Sub

    End Class

End Namespace
