Option Strict On
Option Explicit On

' Generated-looking strongly-typed resource module
' ("My Project\Resources.Designer.vb"). Synthetic content in the classic shape.

Namespace My.Resources

    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "2.0.0.0"), _
     Global.System.Diagnostics.DebuggerNonUserCodeAttribute(), _
     Global.System.Runtime.CompilerServices.CompilerGeneratedAttribute(), _
     Global.Microsoft.VisualBasic.HideModuleNameAttribute()> _
    Friend Module Resource1

        Private _resourceManager As Global.System.Resources.ResourceManager

        Friend ReadOnly Property ResourceManager() As Global.System.Resources.ResourceManager
            Get
                If Object.ReferenceEquals(_resourceManager, Nothing) Then
                    Dim temp As Global.System.Resources.ResourceManager = _
                        New Global.System.Resources.ResourceManager("VbLegacyCatalog.Resource1", _
                            GetType(Resource1).Assembly)
                    _resourceManager = temp
                End If
                Return _resourceManager
            End Get
        End Property

        Friend ReadOnly Property CatalogWindowTitle() As String
            Get
                Return ResourceManager.GetString("CatalogWindowTitle", Nothing)
            End Get
        End Property

    End Module

End Namespace
