Imports System.Data
Imports System.Data.SqlClient
Namespace PublicSql.VisualBasic
    Public NotInheritable Class FieldParameter
        Public ReadOnly Name As String
        Public ReadOnly Type As SqlDbType
        Public ReadOnly Size As Integer
        Public Sub New(name As String, type As SqlDbType, size As Integer)
            Me.Name = name : Me.Type = type : Me.Size = size
        End Sub
    End Class
    Public NotInheritable Class PropertyParameter
        Public ReadOnly Property Name As String
        Public ReadOnly Property Type As SqlDbType
        Public ReadOnly Property Size As Integer
        Public Sub New(name As String, type As SqlDbType, size As Integer)
            Me.Name = name : Me.Type = type : Me.Size = size
        End Sub
    End Class
    Public NotInheritable Class MutableParameter
        Public Property Size As Integer
        Public Sub New(size As Integer)
            Me.Size = size
        End Sub
    End Class
    Public NotInheritable Class InputParameter
        Public ReadOnly Name As String
        Public ReadOnly Type As SqlDbType
        Public ReadOnly Size As Integer
        Public ReadOnly Value As Object
        Public Sub New(name As String, type As SqlDbType, size As Integer, value As Object)
            Me.Name = name : Me.Type = type : Me.Size = size : Me.Value = value
        End Sub
    End Class
    Public Module Wrapper
        Public Function PublicScalarEcho(value As String) As String
            Return value
        End Function
        Public Function PublicScalarTransform(value As String) As String
            Dim result As String = value
            Try
                Try
                    Try
                        If value Is Nothing Then
                            result = "public-fallback"
                        Else
                            result = value.Trim()
                        End If
                    Finally
                        System.GC.KeepAlive(value)
                    End Try
                Finally
                    System.GC.KeepAlive(result)
                End Try
            Catch ex As System.ArgumentException
                result = "public-fallback"
            Finally
                System.GC.KeepAlive(value)
            End Try
            Return result
        End Function
        Private scalarState As String
        Public Function PublicScalarWrites(value As String) As String
            scalarState = value
            Return scalarState
        End Function
        Public Function PublicScalarThrows(value As String) As String
            If value Is Nothing Then Throw New System.ArgumentException()
            Return value
        End Function

        Public Function ArrayInputs(procedure As String, parameters As System.Collections.ArrayList) As DataSet
            Dim command = New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            For Each value As InputParameter In parameters
                Dim parameter = New SqlParameter(value.Name, value.Type, value.Size)
                parameter.Value = value.Value
                command.Parameters.Add(parameter)
            Next
            Dim result = New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
        Public Function ArrayParameters(procedure As String, parameters As System.Collections.ArrayList) As DataSet
            Dim command = New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            For Each value As FieldParameter In parameters
                command.Parameters.Add(New SqlParameter(value.Name, value.Type, value.Size))
            Next
            Dim adapter = New SqlDataAdapter(command)
            Dim result = New DataSet()
            adapter.Fill(result)
            Return result
        End Function
        Public Function ArrayProperties(procedure As String, parameters As System.Collections.ArrayList) As DataSet
            Dim command = New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            For Each value As PropertyParameter In parameters
                command.Parameters.Add(New SqlParameter(value.Name, value.Type, value.Size))
            Next
            Dim adapter = New SqlDataAdapter(command)
            Dim result = New DataSet()
            adapter.Fill(result)
            Return result
        End Function
        Public Function ArrayIncomplete(procedure As String, parameters As System.Collections.ArrayList) As DataSet
            For Each value As FieldParameter In parameters
                Return Forwarded(procedure, New SqlParameter(value.Name, value.Type, value.Size))
            Next
            Return Direct(procedure)
        End Function
        Public Function Direct(procedure As String) As DataSet
            Dim command = New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            Dim parameter = New SqlParameter("@Name", SqlDbType.NVarChar, 40)
            command.Parameters.Add(parameter)
            Dim adapter = New SqlDataAdapter(command)
            Dim result = New DataSet()
            adapter.Fill(result)
            Return result
        End Function
        Public Function Forwarded(procedure As String, parameter As SqlParameter) As DataSet
            Dim command = New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.Add(parameter)
            Dim adapter = New SqlDataAdapter(command)
            Dim result = New DataSet()
            adapter.Fill(result)
            Return result
        End Function
        Public Function FieldObject(procedure As String, value As FieldParameter) As DataSet
            Return Forwarded(procedure, New SqlParameter(value.Name, value.Type, value.Size))
        End Function
        Public Function PropertyObject(procedure As String, value As PropertyParameter) As DataSet
            Return Forwarded(procedure, New SqlParameter(value.Name, value.Type, value.Size))
        End Function
        Public Function Mutated(procedure As String) As DataSet
            Dim parameter = New SqlParameter("@Name", SqlDbType.NVarChar, 40)
            parameter.Size = 80
            Return Forwarded(procedure, parameter)
        End Function
        Public Function Escaped(procedure As String) As DataSet
            Dim parameter = New SqlParameter("@Name", SqlDbType.NVarChar, 40)
            UnknownEffect(parameter)
            Return Forwarded(procedure, parameter)
        End Function
        Public Function Branch(procedure As String, condition As Boolean) As DataSet
            Dim parameter = New SqlParameter("@Name", SqlDbType.NVarChar, If(condition, 40, 80))
            Return Forwarded(procedure, parameter)
        End Function
        Private Sub UnknownEffect(parameter As SqlParameter)
            parameter.Size = 120
        End Sub
    End Module
End Namespace
