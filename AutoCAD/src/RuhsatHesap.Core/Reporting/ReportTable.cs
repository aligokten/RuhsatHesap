using System;
using System.Collections.Generic;
using System.Linq;

namespace RuhsatHesap.Core.Reporting
{
    public enum RowKind
    {
        Header,
        Data,
        Section,
        BlockTotal,
        GrandTotal
    }

    public sealed class ReportColumn
    {
        public string Header = string.Empty;
        /// <summary>Relative width hint, in characters.</summary>
        public double Width = 14.0;
        public bool Numeric;
        /// <summary>Decimals used when rendering a numeric cell.</summary>
        public int Decimals = 2;

        public ReportColumn () { }

        public ReportColumn (string header, double width, bool numeric = false, int decimals = 2)
        {
            Header = header;
            Width = width;
            Numeric = numeric;
            Decimals = decimals;
        }
    }

    public sealed class ReportCell
    {
        public string Text = string.Empty;
        /// <summary>Set for numeric cells; the renderers format it themselves.</summary>
        public double? Value;
        /// <summary>Horizontal cell merge, in columns.</summary>
        public int Span = 1;

        public static ReportCell OfText (string text, int span = 1) =>
            new ReportCell { Text = text ?? string.Empty, Span = span };

        public static ReportCell OfNumber (double value, int decimals = 2, int span = 1) =>
            new ReportCell {
                Value = value,
                Text = TextUtil.FormatArea (value, decimals),
                Span = span
            };

        public static ReportCell OfInteger (double value, int span = 1) =>
            new ReportCell {
                Value = value,
                Text = TextUtil.FormatArea (value, 0),
                Span = span
            };

        public static readonly ReportCell Empty = OfText (string.Empty);
    }

    public sealed class ReportRow
    {
        public RowKind Kind = RowKind.Data;
        public List<ReportCell> Cells = new List<ReportCell> ();

        public static ReportRow Of (RowKind kind, params ReportCell[] cells) =>
            new ReportRow { Kind = kind, Cells = cells.ToList () };
    }

    /// <summary>
    /// Renderer-independent table. The same instance is drawn as an AutoCAD
    /// TABLE entity, written into the .xlsx workbook and exported to .csv, so
    /// the three outputs can never drift apart.
    /// </summary>
    public sealed class ReportTable
    {
        public string Title = string.Empty;
        /// <summary>Sheet name used by the Excel export; defaults to the title.</summary>
        public string SheetName = string.Empty;
        public List<ReportColumn> Columns = new List<ReportColumn> ();
        public List<ReportRow> Rows = new List<ReportRow> ();

        public int ColumnCount => Columns.Count;

        public ReportTable (string title, string sheetName = null)
        {
            Title = title;
            SheetName = string.IsNullOrEmpty (sheetName) ? title : sheetName;
        }

        public ReportTable Column (string header, double width, bool numeric = false, int decimals = 2)
        {
            Columns.Add (new ReportColumn (header, width, numeric, decimals));
            return this;
        }

        public ReportRow AddRow (RowKind kind, params ReportCell[] cells)
        {
            ReportRow row = ReportRow.Of (kind, cells);
            Rows.Add (row);
            return row;
        }

        /// <summary>Adds a full-width caption row.</summary>
        public ReportRow AddSection (string text)
        {
            return AddRow (RowKind.Section, ReportCell.OfText (text, Math.Max (1, ColumnCount)));
        }

        /// <summary>Label in the first cells, value in the last column.</summary>
        public ReportRow AddLabelValue (RowKind kind, string label, ReportCell value)
        {
            int span = Math.Max (1, ColumnCount - 1);
            return AddRow (kind, ReportCell.OfText (label, span), value);
        }

        public double ColumnSum (int columnIndex)
        {
            double total = 0.0;
            foreach (ReportRow row in Rows) {
                if (row.Kind != RowKind.Data) continue;
                ReportCell cell = CellAt (row, columnIndex);
                if (cell?.Value != null) total += cell.Value.Value;
            }
            return total;
        }

        /// <summary>
        /// Resolves the cell covering a column index, honouring spans so a
        /// merged label does not shift the columns after it.
        /// </summary>
        public static ReportCell CellAt (ReportRow row, int columnIndex)
        {
            int position = 0;
            foreach (ReportCell cell in row.Cells) {
                int span = Math.Max (1, cell.Span);
                if (columnIndex >= position && columnIndex < position + span) return cell;
                position += span;
            }
            return null;
        }
    }
}
