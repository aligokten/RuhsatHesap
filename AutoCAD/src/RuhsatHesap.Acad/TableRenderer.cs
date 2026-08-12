using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Geometry;
using RuhsatHesap.Core.Reporting;

namespace RuhsatHesap.Acad
{
    /// <summary>
    /// Draws a <see cref="ReportTable"/> into the drawing as a native AutoCAD
    /// TABLE entity, so the result can be edited, restyled and plotted like any
    /// other table in the sheet. Cells are left unfilled and every cell is
    /// centred, so the drawing's table style keeps full control over borders
    /// and colours.
    /// </summary>
    public static class TableRenderer
    {
        public const string TableLayer = "RH-TABLO";

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
            ObjectId textStyleId = EnsureTextStyle (database, transaction, settings.TableTextStyle);

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

            SetCell (acadTable, 0, 0, table.Title, textHeight * 1.25, CellAlignment.MiddleCenter, textStyleId);
            if (columnCount > 1) acadTable.MergeCells (CellRange.Create (acadTable, 0, 0, 0, columnCount - 1));

            for (int column = 0; column < columnCount; column++)
                SetCell (acadTable, 1, column, table.Columns[column].Header, textHeight,
                    CellAlignment.MiddleCenter, textStyleId);

            int tableRow = 2;
            foreach (ReportRow row in table.Rows) {
                int column = 0;
                foreach (ReportCell cell in row.Cells) {
                    if (column >= columnCount) break;
                    int span = Math.Max (1, cell.Span);
                    int lastColumn = Math.Min (columnCount, column + span) - 1;
                    SetCell (acadTable, tableRow, column, cell.Text, textHeight, CellAlignment.MiddleCenter, textStyleId);
                    for (int filler = column + 1; filler <= lastColumn; filler++)
                        SetCell (acadTable, tableRow, filler, string.Empty, textHeight, CellAlignment.MiddleCenter, textStyleId);
                    if (lastColumn > column)
                        acadTable.MergeCells (CellRange.Create (acadTable, tableRow, column, tableRow, lastColumn));
                    column += span;
                }
                for (; column < columnCount; column++)
                    SetCell (acadTable, tableRow, column, string.Empty, textHeight, CellAlignment.MiddleCenter, textStyleId);
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
            CellAlignment alignment, ObjectId textStyleId)
        {
            Cell cell = table.Cells[row, column];
            if (!textStyleId.IsNull) cell.TextStyleId = textStyleId;
            cell.TextHeight = textHeight;
            cell.Alignment = alignment;
            cell.TextString = text ?? string.Empty;
        }

        /// <summary>
        /// Returns the text style the tables are written with, creating it from
        /// the matching font when the drawing has no such style yet. ISOCPEUR
        /// ships with AutoCAD, so the default costs nothing; an unavailable font
        /// simply falls back to the table style's own text style.
        /// </summary>
        public static ObjectId EnsureTextStyle (Database database, Transaction transaction, string styleName)
        {
            if (string.IsNullOrWhiteSpace (styleName)) return ObjectId.Null;
            styleName = styleName.Trim ();

            var styles = (TextStyleTable) transaction.GetObject (database.TextStyleTableId, OpenMode.ForRead);
            if (styles.Has (styleName)) return styles[styleName];

            try {
                styles.UpgradeOpen ();
                var record = new TextStyleTableRecord {
                    Name = styleName,
                    // TrueType styles are defined by their type face; the .ttf
                    // name keeps the style usable if the face cannot be resolved.
                    FileName = styleName.ToLowerInvariant () + ".ttf",
                    TextSize = 0.0
                };
                record.Font = new FontDescriptor (styleName, false, false, 0, 0);
                styles.Add (record);
                transaction.AddNewlyCreatedDBObject (record, true);
                return record.ObjectId;
            } catch (System.Exception) {
                // A font the machine does not have must not stop the table from
                // being drawn; the table style's own text style is used instead.
                return ObjectId.Null;
            }
        }
    }
}
