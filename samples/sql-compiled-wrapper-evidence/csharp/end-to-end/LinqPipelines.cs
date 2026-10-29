using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
namespace PublicSql.CSharp
{
    public sealed class GroupItem { public string Label { get; set; } }
    public sealed class RowProjection { public int Identifier { get; set; } public string Caption { get; set; } }
    public static class LinqPipelines
    {
        public static object GroupObjects(IEnumerable<GroupItem> values, int threshold)
            => values.GroupBy(value => value)
                .Select(group => new { Item = group.Key, Total = group.Count() })
                .Where(item => item.Total == threshold).ToList();
        public static List<RowProjection> ProjectRows(DataTable table)
            => table.AsEnumerable().Select(row => new RowProjection {
                Identifier = int.Parse(row["public_identifier"].ToString().Trim()),
                Caption = row["public_caption"].ToString() }).ToList();
        public static List<RowProjection> ProjectDataSet(DataSet data)
            => data.Tables[0].AsEnumerable().Select(row => new RowProjection {
                Identifier = (int)row["public_identifier"], Caption = row["public_caption"].ToString() }).ToList();
        public static List<RowProjection> DynamicColumnRefusal(DataTable table, string column)
            => table.AsEnumerable().Select(row => new RowProjection { Caption = row[column].ToString() }).ToList();
        public static List<RowProjection> HelperRefusal(DataTable table)
            => table.AsEnumerable().Select(row => new RowProjection { Caption = Build(row["public_caption"]) }).ToList();
        private static string Build(object value) => value.ToString();
        public static object ComparerRefusal(IEnumerable<GroupItem> values, int threshold)
            => values.GroupBy(value => value, EqualityComparer<GroupItem>.Default)
                .Select(group => new { Item = group.Key, Total = group.Count() }).Where(item => item.Total == threshold).ToList();
        public static List<RowProjection> IndexedSelectorRefusal(DataTable table)
            => table.AsEnumerable().Select((row, index) => new RowProjection { Identifier = index }).ToList();
    }
}
