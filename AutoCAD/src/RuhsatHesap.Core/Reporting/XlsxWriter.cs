using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace RuhsatHesap.Core.Reporting
{
    /// <summary>
    /// Writes the report tables into an .xlsx workbook. The file is assembled
    /// directly as Office Open XML inside a zip container, so the plug-in needs
    /// neither Excel nor a third-party spreadsheet library on the machine.
    /// </summary>
    public static class XlsxWriter
    {
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // Style indices, matching the order written in StylesXml ().
        private const int StyleDefault = 0;
        private const int StyleTitle = 1;
        private const int StyleHeader = 2;
        private const int StyleText = 3;
        private const int StyleNumber = 4;
        private const int StyleInteger = 5;
        private const int StyleTotalText = 6;
        private const int StyleTotalNumber = 7;
        private const int StyleSection = 8;

        public static void Write (string path, IEnumerable<ReportTable> tables)
        {
            List<ReportTable> sheets = tables.ToList ();
            if (sheets.Count == 0) throw new InvalidOperationException ("Yazılacak tablo yok.");

            var usedNames = new List<string> ();
            var sheetNames = sheets.Select (table => UniqueSheetName (table, usedNames)).ToList ();

            using (var stream = new FileStream (path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive (stream, ZipArchiveMode.Create)) {
                AddEntry (archive, "[Content_Types].xml", ContentTypesXml (sheets.Count));
                AddEntry (archive, "_rels/.rels", RootRelationshipsXml ());
                AddEntry (archive, "xl/workbook.xml", WorkbookXml (sheetNames));
                AddEntry (archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml (sheets.Count));
                AddEntry (archive, "xl/styles.xml", StylesXml ());
                for (int index = 0; index < sheets.Count; index++)
                    AddEntry (archive, "xl/worksheets/sheet" + (index + 1).ToString (CultureInfo.InvariantCulture) + ".xml",
                        WorksheetXml (sheets[index]));
            }
        }

        private static void AddEntry (ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry (name, CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open ())
            using (var writer = new StreamWriter (entryStream, new UTF8Encoding (false)))
                writer.Write (content);
        }

        /// <summary>Excel rejects []:*?/\ and anything longer than 31 characters.</summary>
        private static string UniqueSheetName (ReportTable table, List<string> used)
        {
            string name = string.IsNullOrWhiteSpace (table.SheetName) ? "Tablo" : table.SheetName;
            foreach (char forbidden in new[] { '[', ']', ':', '*', '?', '/', '\\' })
                name = name.Replace (forbidden, ' ');
            name = name.Trim ();
            if (name.Length > 31) name = name.Substring (0, 31);
            if (name.Length == 0) name = "Tablo";

            string candidate = name;
            int suffix = 2;
            while (used.Contains (candidate, StringComparer.OrdinalIgnoreCase)) {
                string tail = " " + suffix.ToString (CultureInfo.InvariantCulture);
                candidate = (name.Length + tail.Length > 31 ? name.Substring (0, 31 - tail.Length) : name) + tail;
                suffix++;
            }
            used.Add (candidate);
            return candidate;
        }

        public static string ColumnName (int columnIndex)
        {
            var builder = new StringBuilder ();
            int value = columnIndex + 1;
            while (value > 0) {
                int remainder = (value - 1) % 26;
                builder.Insert (0, (char) ('A' + remainder));
                value = (value - 1) / 26;
            }
            return builder.ToString ();
        }

        private static string CellReference (int rowIndex, int columnIndex) =>
            ColumnName (columnIndex) + (rowIndex + 1).ToString (CultureInfo.InvariantCulture);

        private static string WorksheetXml (ReportTable table)
        {
            var builder = new StringBuilder ();
            var merges = new List<string> ();
            builder.Append ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append ("<worksheet xmlns=\"").Append (SpreadsheetNamespace).Append ("\">");

            builder.Append ("<cols>");
            for (int index = 0; index < table.ColumnCount; index++) {
                builder.Append ("<col min=\"").Append (index + 1).Append ("\" max=\"").Append (index + 1)
                       .Append ("\" width=\"").Append (table.Columns[index].Width.ToString ("0.##", CultureInfo.InvariantCulture))
                       .Append ("\" customWidth=\"1\"/>");
            }
            builder.Append ("</cols><sheetData>");

            // Row 0: title, row 1: blank, row 2: header, then the data rows.
            builder.Append ("<row r=\"1\" ht=\"22\" customHeight=\"1\">");
            AppendInlineString (builder, CellReference (0, 0), StyleTitle, table.Title);
            builder.Append ("</row>");
            if (table.ColumnCount > 1)
                merges.Add (CellReference (0, 0) + ":" + CellReference (0, table.ColumnCount - 1));

            builder.Append ("<row r=\"3\">");
            for (int index = 0; index < table.ColumnCount; index++)
                AppendInlineString (builder, CellReference (2, index), StyleHeader, table.Columns[index].Header);
            builder.Append ("</row>");

            int rowIndex = 3;
            foreach (ReportRow row in table.Rows) {
                builder.Append ("<row r=\"").Append (rowIndex + 1).Append ("\">");
                int columnIndex = 0;
                foreach (ReportCell cell in row.Cells) {
                    if (columnIndex >= table.ColumnCount) break;
                    int span = Math.Max (1, cell.Span);
                    bool numeric = cell.Value.HasValue;
                    int style = StyleFor (row.Kind, numeric, table.Columns[columnIndex].Decimals);
                    if (numeric) AppendNumber (builder, CellReference (rowIndex, columnIndex), style, cell.Value.Value);
                    else AppendInlineString (builder, CellReference (rowIndex, columnIndex), style, cell.Text);
                    if (span > 1) {
                        int lastColumn = Math.Min (table.ColumnCount, columnIndex + span) - 1;
                        // The merged range still needs its trailing cells to
                        // carry the style, otherwise Excel drops the fill.
                        for (int filler = columnIndex + 1; filler <= lastColumn; filler++)
                            AppendInlineString (builder, CellReference (rowIndex, filler), style, string.Empty);
                        if (lastColumn > columnIndex)
                            merges.Add (CellReference (rowIndex, columnIndex) + ":" + CellReference (rowIndex, lastColumn));
                    }
                    columnIndex += span;
                }
                builder.Append ("</row>");
                rowIndex++;
            }

            builder.Append ("</sheetData>");
            if (merges.Count > 0) {
                builder.Append ("<mergeCells count=\"").Append (merges.Count).Append ("\">");
                foreach (string merge in merges) builder.Append ("<mergeCell ref=\"").Append (merge).Append ("\"/>");
                builder.Append ("</mergeCells>");
            }
            builder.Append ("</worksheet>");
            return builder.ToString ();
        }

        private static int StyleFor (RowKind kind, bool numeric, int decimals)
        {
            switch (kind) {
                case RowKind.Section:
                    return StyleSection;
                case RowKind.BlockTotal:
                case RowKind.GrandTotal:
                    return numeric ? StyleTotalNumber : StyleTotalText;
                default:
                    if (!numeric) return StyleText;
                    return decimals == 0 ? StyleInteger : StyleNumber;
            }
        }

        private static void AppendInlineString (StringBuilder builder, string reference, int style, string text)
        {
            builder.Append ("<c r=\"").Append (reference).Append ("\" s=\"").Append (style).Append ("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                   .Append (XmlEscape (text))
                   .Append ("</t></is></c>");
        }

        private static void AppendNumber (StringBuilder builder, string reference, int style, double value)
        {
            if (double.IsNaN (value) || double.IsInfinity (value)) value = 0.0;
            builder.Append ("<c r=\"").Append (reference).Append ("\" s=\"").Append (style).Append ("\"><v>")
                   .Append (value.ToString ("0.############", CultureInfo.InvariantCulture))
                   .Append ("</v></c>");
        }

        public static string XmlEscape (string value)
        {
            if (string.IsNullOrEmpty (value)) return string.Empty;
            var builder = new StringBuilder (value.Length);
            foreach (char character in value) {
                switch (character) {
                    case '&': builder.Append ("&amp;"); break;
                    case '<': builder.Append ("&lt;"); break;
                    case '>': builder.Append ("&gt;"); break;
                    case '"': builder.Append ("&quot;"); break;
                    case '\'': builder.Append ("&apos;"); break;
                    default:
                        // Control characters are not legal in XML 1.0 content.
                        if (character < 0x20 && character != '\t' && character != '\n' && character != '\r') break;
                        builder.Append (character);
                        break;
                }
            }
            return builder.ToString ();
        }

        private static string ContentTypesXml (int sheetCount)
        {
            var builder = new StringBuilder ();
            builder.Append ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append ("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            builder.Append ("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            builder.Append ("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            builder.Append ("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (int index = 1; index <= sheetCount; index++)
                builder.Append ("<Override PartName=\"/xl/worksheets/sheet").Append (index)
                       .Append (".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            builder.Append ("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            builder.Append ("</Types>");
            return builder.ToString ();
        }

        private static string RootRelationshipsXml () =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"" + RelationshipNamespace + "/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private static string WorkbookXml (IReadOnlyList<string> sheetNames)
        {
            var builder = new StringBuilder ();
            builder.Append ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append ("<workbook xmlns=\"").Append (SpreadsheetNamespace)
                   .Append ("\" xmlns:r=\"").Append (RelationshipNamespace).Append ("\"><sheets>");
            for (int index = 0; index < sheetNames.Count; index++) {
                builder.Append ("<sheet name=\"").Append (XmlEscape (sheetNames[index]))
                       .Append ("\" sheetId=\"").Append (index + 1)
                       .Append ("\" r:id=\"rId").Append (index + 1).Append ("\"/>");
            }
            builder.Append ("</sheets></workbook>");
            return builder.ToString ();
        }

        private static string WorkbookRelationshipsXml (int sheetCount)
        {
            var builder = new StringBuilder ();
            builder.Append ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append ("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int index = 1; index <= sheetCount; index++)
                builder.Append ("<Relationship Id=\"rId").Append (index).Append ("\" Type=\"").Append (RelationshipNamespace)
                       .Append ("/worksheet\" Target=\"worksheets/sheet").Append (index).Append (".xml\"/>");
            builder.Append ("<Relationship Id=\"rId").Append (sheetCount + 1).Append ("\" Type=\"").Append (RelationshipNamespace)
                   .Append ("/styles\" Target=\"styles.xml\"/>");
            builder.Append ("</Relationships>");
            return builder.ToString ();
        }

        private static string StylesXml ()
        {
            var builder = new StringBuilder ();
            builder.Append ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append ("<styleSheet xmlns=\"").Append (SpreadsheetNamespace).Append ("\">");
            builder.Append ("<numFmts count=\"2\">");
            builder.Append ("<numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/>");
            builder.Append ("<numFmt numFmtId=\"165\" formatCode=\"#,##0\"/>");
            builder.Append ("</numFmts>");

            builder.Append ("<fonts count=\"3\">");
            builder.Append ("<font><sz val=\"11\"/><name val=\"Calibri\"/></font>");
            builder.Append ("<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>");
            builder.Append ("<font><b/><sz val=\"14\"/><name val=\"Calibri\"/></font>");
            builder.Append ("</fonts>");

            builder.Append ("<fills count=\"5\">");
            builder.Append ("<fill><patternFill patternType=\"none\"/></fill>");
            builder.Append ("<fill><patternFill patternType=\"gray125\"/></fill>");
            builder.Append ("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9E1F2\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            builder.Append ("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE2EFDA\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            builder.Append ("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            builder.Append ("</fills>");

            builder.Append ("<borders count=\"2\">");
            builder.Append ("<border><left/><right/><top/><bottom/><diagonal/></border>");
            builder.Append ("<border><left style=\"thin\"><color rgb=\"FF808080\"/></left><right style=\"thin\"><color rgb=\"FF808080\"/></right>");
            builder.Append ("<top style=\"thin\"><color rgb=\"FF808080\"/></top><bottom style=\"thin\"><color rgb=\"FF808080\"/></bottom><diagonal/></border>");
            builder.Append ("</borders>");

            builder.Append ("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
            builder.Append ("<cellXfs count=\"9\">");
            // 0 default
            builder.Append ("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
            // 1 title
            builder.Append ("<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>");
            // 2 header
            builder.Append ("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>");
            // 3 text
            builder.Append ("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>");
            // 4 number
            builder.Append ("<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>");
            // 5 integer
            builder.Append ("<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>");
            // 6 total text
            builder.Append ("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>");
            // 7 total number
            builder.Append ("<xf numFmtId=\"164\" fontId=\"1\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>");
            // 8 section
            builder.Append ("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>");
            builder.Append ("</cellXfs>");
            builder.Append ("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            builder.Append ("</styleSheet>");
            return builder.ToString ();
        }
    }
}
