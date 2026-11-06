Public Class Consumer
    Private ReadOnly connectionString As String
    Public Sub New(ByVal connectionString As String)
        Me.connectionString = connectionString
    End Sub

    ' BEGIN SELECTED METHOD
    Public Function ReadItems(ByVal first As String, ByVal second As String, ByVal month As String, ByVal year As Integer) As DataSet
        Dim parameters As New ArrayList()
        Dim firstValue As Object = first
        If String.IsNullOrEmpty(first) Then firstValue = DBNull.Value
        Dim secondValue As Object = second
        If String.IsNullOrEmpty(second) Then secondValue = DBNull.Value
        parameters.Add(New ParameterSpec("@first", SqlDbType.VarChar, firstValue))
        parameters.Add(New ParameterSpec("@second", SqlDbType.VarChar, secondValue))
        parameters.Add(New ParameterSpec("@month", SqlDbType.Int, CInt(month)))
        parameters.Add(New ParameterSpec("@year", SqlDbType.Int, year))
        Return DataAccess.Execute(connectionString, "dbo.ReadItems", parameters)
    End Function
    ' END SELECTED METHOD
End Class
