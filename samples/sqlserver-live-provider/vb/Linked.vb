Imports System.Data
Imports Microsoft.Data.SqlClient
Namespace LinkedVisualBasic
Public Module Cases
    Public Function ReaderName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using reader = command.ExecuteReader()
                If reader.Read() Then Return reader("Name")
            End Using
        End Using
        Return Nothing
    End Function
    Public Function TableName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0)("Name")
            End Using
        End Using
    End Function
    Public Function SetName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim results As New DataSet()
                adapter.Fill(results)
                Return results.Tables(0).Rows(0)("Name")
            End Using
        End Using
    End Function
    Public Function MissingName(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0)("Missing")
            End Using
        End Using
    End Function
    Public Function CaseOnly(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table.Rows(0)("name")
            End Using
        End Using
    End Function
    Public Function LaterTable(connection As SqlConnection) As Object
        Using command As New SqlCommand("dbo.ReadItems")
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim results As New DataSet()
                adapter.Fill(results)
                Return results.Tables(1).Rows(0)("Value")
            End Using
        End Using
    End Function
End Module
End Namespace
