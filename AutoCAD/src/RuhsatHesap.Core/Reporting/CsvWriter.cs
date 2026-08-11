using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RuhsatHesap.Core.Reporting
{
    /// <summary>
    /// Writes report tables as CSV. Turkish Excel expects ';' as the list
    /// separator and ',' as the decimal mark, which is what the cell text
    /// already uses, so the file opens with a double click.
    /// </summary>
    public static class CsvWriter
    {
        public static void Write (string path, IEnumerable<ReportTable> tables)
        {
            // UTF-8 with BOM: without it Excel shows Turkish characters wrong.
            using (var writer = new StreamWriter (path, false, new UTF8Encoding (true)))
                writer.Write (Build (tables));
        }

        public static string Build (IEnumerable<ReportTable> tables)
        {
            var builder = new StringBuilder ();
            bool first = true;
            foreach (ReportTable table in tables) {
                if (!first) builder.Append ('\n');
                first = false;
                builder.Append (Escape (table.Title)).Append ('\n');

                var headers = new List<string> ();
                foreach (ReportColumn column in table.Columns) headers.Add (column.Header);
                builder.Append (string.Join (";", headers.ConvertAll (Escape))).Append ('\n');

                foreach (ReportRow row in table.Rows) {
                    var cells = new List<string> ();
                    int columnIndex = 0;
                    foreach (ReportCell cell in row.Cells) {
                        int span = Math.Max (1, cell.Span);
                        cells.Add (Escape (cell.Text));
                        for (int filler = 1; filler < span && columnIndex + filler < table.ColumnCount; filler++)
                            cells.Add (string.Empty);
                        columnIndex += span;
                    }
                    builder.Append (string.Join (";", cells)).Append ('\n');
                }
            }
            return builder.ToString ();
        }

        private static string Escape (string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOf (';') < 0 && value.IndexOf ('"') < 0 && value.IndexOf ('\n') < 0) return value;
            return "\"" + value.Replace ("\"", "\"\"") + "\"";
        }
    }
}
