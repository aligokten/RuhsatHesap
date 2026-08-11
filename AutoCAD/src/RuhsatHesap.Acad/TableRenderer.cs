using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using RuhsatHesap.Core.Reporting;

namespace RuhsatHesap.Acad
{
    /// <summary>
    /// Draws a <see cref="ReportTable"/> into the drawing as a native AutoCAD
    /// TABLE entity, so the result can be edited, restyled and plotted like any
    /// other table in the sheet.
    /// </summary>
    public static class TableRenderer
    {
        public const string TableLayer = "RH-TABLO";

        private static readonly Color HeaderColor = Color.FromRgb (217, 225, 242);
        private static readonly Color TotalColor = Color.FromRgb (226, 239, 218);
        private static readonly Color SectionColor = Color.FromRgb (255, 242, 204);

        /// <summary>
        /// Inserts one table with its upper-left corner at <paramref name="position"/>
        /// and returns the height it consumed, so several tables can be stacked.
        /// </summary>
        public static double Insert (Database database, Transaction transaction, BlockTableRecord space,
            ReportTable table, Point3d position, DrawingSettings settings)
        {
            double textHeight = settings.EffectiveTextHeight;
            double rowHeight = textHeight * 2.0;
            double charWidth = textHeight * 0.75;

            var acadTable = new Table ();
            acadTable.SetDatabaseDefaults (database);
            if (!database.Tablestyle.IsNull) acadTable.TableStyle = database.Tablestyle;
            acadTable.Layer = AcadUi.EnsureLayer (database, transaction, TableLayer, 7);

            int columnCount = Math.Max (1, table.ColumnCount);
            int rowCount = 2 + table.Rows.Count; // başlık + sütun başlıkları + satırlar
            acadTable.SetSize (rowCount, columnCount);
            acadTable.Position = position;

            for (int column = 0; column < columnCount; column++)
                acadTable.Columns[column].Width = Math.Max (textHeight * 4.0, table.Columns[column].Width * charWidth);
            for (int row = 0; row < rowCount; row++)
                acadTable.Rows[row].Height = rowHeight;

            SetCell (acadTable, 0, 0, table.Title, textHeight * 1.25, CellAlignment.MiddleCenter, null, true);
            if (columnCount > 1) acadTable.MergeCells (CellRange.Create (acadTable, 0, 0, 0, columnCount - 1));

            for (int column = 0; column < columnCount; column++)
                SetCell (acadTable, 1, column, table.Columns[column].Header, textHeight,
                    CellAlignment.MiddleCenter, HeaderColor, true);

            int tableRow = 2;
            foreach (ReportRow row in table.Rows) {
                bool bold = row.Kind == RowKind.BlockTotal || row.Kind == RowKind.GrandTotal || row.Kind == RowKind.Section;
                Color background =
                    row.Kind == RowKind.Section ? SectionColor :
                    row.Kind == RowKind.BlockTotal || row.Kind == RowKind.GrandTotal ? TotalColor : null;

                int column = 0;
                foreach (ReportCell cell in row.Cells) {
                    if (column >= columnCount) break;
                    int span = Math.Max (1, cell.Span);
                    int lastColumn = Math.Min (columnCount, column + span) - 1;
                    CellAlignment alignment = cell.Value.HasValue ? CellAlignment.MiddleRight : CellAlignment.MiddleLeft;
                    if (span > 1) alignment = CellAlignment.MiddleLeft;
                    SetCell (acadTable, tableRow, column, cell.Text, textHeight, alignment, background, bold);
                    for (int filler = column + 1; filler <= lastColumn; filler++)
                        SetCell (acadTable, tableRow, filler, string.Empty, textHeight, alignment, background, bold);
                    if (lastColumn > column)
                        acadTable.MergeCells (CellRange.Create (acadTable, tableRow, column, tableRow, lastColumn));
                    column += span;
                }
                // Empty trailing cells still need the row's background.
                for (; column < columnCount; column++)
                    SetCell (acadTable, tableRow, column, string.Empty, textHeight, CellAlignment.MiddleLeft, background, bold);
                tableRow++;
            }

            acadTable.GenerateLayout ();
            space.AppendEntity (acadTable);
            transaction.AddNewlyCreatedDBObject (acadTable, true);
            return rowCount * rowHeight;
        }

        /// <summary>Stacks several tables under each other, top-down.</summary>
        public static void InsertStack (Database database, Transaction transaction, BlockTableRecord space,
            IEnumerable<ReportTable> tables, Point3d position, DrawingSettings settings)
        {
            double gap = settings.EffectiveTextHeight * 4.0;
            double offset = 0.0;
            foreach (ReportTable table in tables) {
                double height = Insert (database, transaction, space, table,
                    new Point3d (position.X, position.Y - offset, position.Z), settings);
                offset += height + gap;
            }
        }

        private static void SetCell (Table table, int row, int column, string text, double textHeight,
            CellAlignment alignment, Color background, bool bold)
        {
            Cell cell = table.Cells[row, column];
            cell.TextHeight = textHeight;
            cell.Alignment = alignment;
            cell.TextString = text ?? string.Empty;
            if (background != null) cell.BackgroundColor = background;
            if (bold) {
                // The table style controls the font; emphasising a total row
                // with a slightly taller text keeps the output readable even
                // when the style has no bold variant.
                cell.TextHeight = textHeight * 1.05;
            }
        }
    }
}
