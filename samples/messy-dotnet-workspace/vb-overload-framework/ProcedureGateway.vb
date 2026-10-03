Imports System
Imports System.Collections
Imports System.Data
Imports System.Data.SqlClient

Namespace Synthetic.Framework
    Public Class ProcedureGateway
        Public Function ExecuteProcedureDataSet(commandText As String) As DataSet
            Dim parameters As New ArrayList()
            Return ExecuteProcedureDataSet(commandText, parameters)
        End Function

        Public Function ExecuteProcedureDataSet(commandText As String, ByRef parameters As ArrayList) As DataSet
            Dim descriptors = DirectCast(parameters.ToArray(GetType(ParameterDescriptor)), ParameterDescriptor())
            Dim mappings As String() = Nothing
            Return ExecuteProcedureDataSet(commandText, descriptors, mappings)
        End Function

        Public Function ExecuteProcedureDataSet(commandText As String, ByRef descriptors As ParameterDescriptor(), ByRef mappings As String()) As DataSet
            Dim result As New DataSet()
            Dim adapter As New SqlDataAdapter()
            adapter.Fill(result)
            Return result
        End Function
    End Class

    Public Class ParameterDescriptor
        Public Sub New(name As String, value As Integer)
        End Sub
    End Class
End Namespace
