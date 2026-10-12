Namespace PublicGenericFixture
    ' Hand-written public analogue of a nested generic provider, not VB My code.
    Public Class Container
        Public Class Slot(Of T)
            Public ReadOnly Property Current As T
                Get
                    Return Nothing
                End Get
            End Property

            Public Function Copy(ByRef value As T, values As T()) As T
                Return value
            End Function
        End Class
    End Class

    ' Same member spelling must not cause cross-type resolution.
    Public Class OtherContainer
        Public Class Slot(Of T)
            Public ReadOnly Property Current As T
                Get
                    Return Nothing
                End Get
            End Property
        End Class
    End Class

    Public Class PlainProvider
        Public ReadOnly Property Current As String
            Get
                Return "public synthetic value"
            End Get
        End Property
    End Class
End Namespace
