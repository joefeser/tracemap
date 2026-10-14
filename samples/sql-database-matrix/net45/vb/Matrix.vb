Public Class SqlMatrix
Public Sub Case000(receiver As Global.System.Data.Common.DataAdapter, ByVal arg0 As Global.System.Data.DataSet)
Call receiver.Fill(arg0)
End Sub
Public Sub Case001(receiver As Global.System.Data.Common.DataAdapter, ByVal arg0 As Global.System.Data.DataSet, ByVal arg1 As Global.System.Data.SchemaType)
Call receiver.FillSchema(arg0, arg1)
End Sub
Public Sub Case002(receiver As Global.System.Data.Common.DataAdapter, ByVal arg0 As Global.System.Data.DataSet)
Call receiver.Update(arg0)
End Sub
Public Sub Case003(receiver As Global.System.Data.Common.DbCommand)
Call receiver.ExecuteNonQuery()
End Sub
Public Sub Case004(receiver As Global.System.Data.Common.DbCommand)
Call receiver.ExecuteNonQueryAsync()
End Sub
Public Sub Case005(receiver As Global.System.Data.Common.DbCommand, ByVal arg0 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteNonQueryAsync(arg0)
End Sub
Public Sub Case006(receiver As Global.System.Data.Common.DbCommand)
Call receiver.ExecuteReader()
End Sub
Public Sub Case007(receiver As Global.System.Data.Common.DbCommand, ByVal arg0 As Global.System.Data.CommandBehavior)
Call receiver.ExecuteReader(arg0)
End Sub
Public Sub Case008(receiver As Global.System.Data.Common.DbCommand)
Call receiver.ExecuteReaderAsync()
End Sub
Public Sub Case009(receiver As Global.System.Data.Common.DbCommand, ByVal arg0 As Global.System.Data.CommandBehavior)
Call receiver.ExecuteReaderAsync(arg0)
End Sub
Public Sub Case010(receiver As Global.System.Data.Common.DbCommand, ByVal arg0 As Global.System.Data.CommandBehavior, ByVal arg1 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteReaderAsync(arg0, arg1)
End Sub
Public Sub Case011(receiver As Global.System.Data.Common.DbCommand, ByVal arg0 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteReaderAsync(arg0)
End Sub
Public Sub Case012(receiver As Global.System.Data.Common.DbCommand)
Call receiver.ExecuteScalar()
End Sub
Public Sub Case013(receiver As Global.System.Data.Common.DbCommand)
Call receiver.ExecuteScalarAsync()
End Sub
Public Sub Case014(receiver As Global.System.Data.Common.DbCommand, ByVal arg0 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteScalarAsync(arg0)
End Sub
Public Sub Case015(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataSet)
Call receiver.Fill(arg0)
End Sub
Public Sub Case016(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataSet, ByVal arg1 As Global.System.Int32, ByVal arg2 As Global.System.Int32, ByVal arg3 As Global.System.String)
Call receiver.Fill(arg0, arg1, arg2, arg3)
End Sub
Public Sub Case017(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataSet, ByVal arg1 As Global.System.String)
Call receiver.Fill(arg0, arg1)
End Sub
Public Sub Case018(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataTable)
Call receiver.Fill(arg0)
End Sub
Public Sub Case019(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Int32, ByVal arg1 As Global.System.Int32, ByVal arg2 As Global.System.Data.DataTable())
Call receiver.Fill(arg0, arg1, arg2)
End Sub
Public Sub Case020(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataSet, ByVal arg1 As Global.System.Data.SchemaType)
Call receiver.FillSchema(arg0, arg1)
End Sub
Public Sub Case021(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataSet, ByVal arg1 As Global.System.Data.SchemaType, ByVal arg2 As Global.System.String)
Call receiver.FillSchema(arg0, arg1, arg2)
End Sub
Public Sub Case022(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataTable, ByVal arg1 As Global.System.Data.SchemaType)
Call receiver.FillSchema(arg0, arg1)
End Sub
Public Sub Case023(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataRow())
Call receiver.Update(arg0)
End Sub
Public Sub Case024(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataSet)
Call receiver.Update(arg0)
End Sub
Public Sub Case025(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataSet, ByVal arg1 As Global.System.String)
Call receiver.Update(arg0, arg1)
End Sub
Public Sub Case026(receiver As Global.System.Data.Common.DbDataAdapter, ByVal arg0 As Global.System.Data.DataTable)
Call receiver.Update(arg0)
End Sub
Public Sub Case027(receiver As Global.System.Data.IDataAdapter, ByVal arg0 As Global.System.Data.DataSet)
Call receiver.Fill(arg0)
End Sub
Public Sub Case028(receiver As Global.System.Data.IDataAdapter, ByVal arg0 As Global.System.Data.DataSet, ByVal arg1 As Global.System.Data.SchemaType)
Call receiver.FillSchema(arg0, arg1)
End Sub
Public Sub Case029(receiver As Global.System.Data.IDataAdapter, ByVal arg0 As Global.System.Data.DataSet)
Call receiver.Update(arg0)
End Sub
Public Sub Case030(receiver As Global.System.Data.IDbCommand)
Call receiver.ExecuteNonQuery()
End Sub
Public Sub Case031(receiver As Global.System.Data.IDbCommand)
Call receiver.ExecuteReader()
End Sub
Public Sub Case032(receiver As Global.System.Data.IDbCommand, ByVal arg0 As Global.System.Data.CommandBehavior)
Call receiver.ExecuteReader(arg0)
End Sub
Public Sub Case033(receiver As Global.System.Data.IDbCommand)
Call receiver.ExecuteScalar()
End Sub
Public Sub Case034(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataRow())
Call receiver.WriteToServer(arg0)
End Sub
Public Sub Case035(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataTable)
Call receiver.WriteToServer(arg0)
End Sub
Public Sub Case036(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataTable, ByVal arg1 As Global.System.Data.DataRowState)
Call receiver.WriteToServer(arg0, arg1)
End Sub
Public Sub Case037(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.IDataReader)
Call receiver.WriteToServer(arg0)
End Sub
Public Sub Case038(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataRow())
Call receiver.WriteToServerAsync(arg0)
End Sub
Public Sub Case039(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataRow(), ByVal arg1 As Global.System.Threading.CancellationToken)
Call receiver.WriteToServerAsync(arg0, arg1)
End Sub
Public Sub Case040(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataTable)
Call receiver.WriteToServerAsync(arg0)
End Sub
Public Sub Case041(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataTable, ByVal arg1 As Global.System.Data.DataRowState)
Call receiver.WriteToServerAsync(arg0, arg1)
End Sub
Public Sub Case042(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataTable, ByVal arg1 As Global.System.Data.DataRowState, ByVal arg2 As Global.System.Threading.CancellationToken)
Call receiver.WriteToServerAsync(arg0, arg1, arg2)
End Sub
Public Sub Case043(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.DataTable, ByVal arg1 As Global.System.Threading.CancellationToken)
Call receiver.WriteToServerAsync(arg0, arg1)
End Sub
Public Sub Case044(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.IDataReader)
Call receiver.WriteToServerAsync(arg0)
End Sub
Public Sub Case045(receiver As Global.System.Data.SqlClient.SqlBulkCopy, ByVal arg0 As Global.System.Data.IDataReader, ByVal arg1 As Global.System.Threading.CancellationToken)
Call receiver.WriteToServerAsync(arg0, arg1)
End Sub
Public Sub Case046(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.BeginExecuteNonQuery()
End Sub
Public Sub Case047(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.AsyncCallback, ByVal arg1 As Global.System.Object)
Call receiver.BeginExecuteNonQuery(arg0, arg1)
End Sub
Public Sub Case048(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.BeginExecuteReader()
End Sub
Public Sub Case049(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.AsyncCallback, ByVal arg1 As Global.System.Object)
Call receiver.BeginExecuteReader(arg0, arg1)
End Sub
Public Sub Case050(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.AsyncCallback, ByVal arg1 As Global.System.Object, ByVal arg2 As Global.System.Data.CommandBehavior)
Call receiver.BeginExecuteReader(arg0, arg1, arg2)
End Sub
Public Sub Case051(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Data.CommandBehavior)
Call receiver.BeginExecuteReader(arg0)
End Sub
Public Sub Case052(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.BeginExecuteXmlReader()
End Sub
Public Sub Case053(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.AsyncCallback, ByVal arg1 As Global.System.Object)
Call receiver.BeginExecuteXmlReader(arg0, arg1)
End Sub
Public Sub Case054(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.IAsyncResult)
Call receiver.EndExecuteNonQuery(arg0)
End Sub
Public Sub Case055(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.IAsyncResult)
Call receiver.EndExecuteReader(arg0)
End Sub
Public Sub Case056(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.IAsyncResult)
Call receiver.EndExecuteXmlReader(arg0)
End Sub
Public Sub Case057(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.ExecuteNonQuery()
End Sub
Public Sub Case058(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteNonQueryAsync(arg0)
End Sub
Public Sub Case059(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.ExecuteReader()
End Sub
Public Sub Case060(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Data.CommandBehavior)
Call receiver.ExecuteReader(arg0)
End Sub
Public Sub Case061(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.ExecuteReaderAsync()
End Sub
Public Sub Case062(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Data.CommandBehavior)
Call receiver.ExecuteReaderAsync(arg0)
End Sub
Public Sub Case063(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Data.CommandBehavior, ByVal arg1 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteReaderAsync(arg0, arg1)
End Sub
Public Sub Case064(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteReaderAsync(arg0)
End Sub
Public Sub Case065(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.ExecuteScalar()
End Sub
Public Sub Case066(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteScalarAsync(arg0)
End Sub
Public Sub Case067(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.ExecuteXmlReader()
End Sub
Public Sub Case068(receiver As Global.System.Data.SqlClient.SqlCommand)
Call receiver.ExecuteXmlReaderAsync()
End Sub
Public Sub Case069(receiver As Global.System.Data.SqlClient.SqlCommand, ByVal arg0 As Global.System.Threading.CancellationToken)
Call receiver.ExecuteXmlReaderAsync(arg0)
End Sub
End Class
