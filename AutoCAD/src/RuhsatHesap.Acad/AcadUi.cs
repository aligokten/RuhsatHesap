using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace RuhsatHesap.Acad
{
    /// <summary>Command-line helpers shared by every RH command.</summary>
    public static class AcadUi
    {
        public static Document ActiveDocument => AcadApp.DocumentManager.MdiActiveDocument;

        public static void Write (Editor editor, string message) => editor.WriteMessage ("\n" + message);

        public static void WriteHeader (Editor editor, string message)
        {
            editor.WriteMessage ("\n");
            editor.WriteMessage ("\n" + message);
            editor.WriteMessage ("\n" + new string ('-', Math.Min (60, Math.Max (10, message.Length))));
        }

        /// <summary>Asks for a text value. Returns null when the user cancels.</summary>
        public static string AskString (Editor editor, string message, string defaultValue)
        {
            defaultValue = defaultValue ?? string.Empty;
            var options = new PromptStringOptions ("\n" + message + ": ") {
                AllowSpaces = true,
                DefaultValue = defaultValue,
                UseDefaultValue = defaultValue.Length > 0
            };
            PromptResult result = editor.GetString (options);
            if (result.Status != PromptStatus.OK) return null;
            string value = (result.StringResult ?? string.Empty).Trim ();
            // "." clears a value, the same convention AutoCAD uses elsewhere.
            if (value == ".") return string.Empty;
            return value.Length == 0 ? defaultValue : value;
        }

        /// <summary>Asks for a number. Returns null when the user cancels.</summary>
        public static double? AskDouble (Editor editor, string message, double defaultValue, bool allowNegative = false)
        {
            var options = new PromptDoubleOptions ("\n" + message + ": ") {
                AllowNegative = allowNegative,
                AllowNone = true,
                DefaultValue = defaultValue,
                UseDefaultValue = true
            };
            PromptDoubleResult result = editor.GetDouble (options);
            if (result.Status == PromptStatus.None) return defaultValue;
            if (result.Status != PromptStatus.OK) return null;
            return result.Value;
        }

        public static int? AskInteger (Editor editor, string message, int defaultValue)
        {
            var options = new PromptIntegerOptions ("\n" + message + ": ") {
                AllowNegative = false,
                AllowNone = true,
                DefaultValue = defaultValue,
                UseDefaultValue = true
            };
            PromptIntegerResult result = editor.GetInteger (options);
            if (result.Status == PromptStatus.None) return defaultValue;
            if (result.Status != PromptStatus.OK) return null;
            return result.Value;
        }

        /// <summary>Asks the user to choose one keyword. Returns null on cancel.</summary>
        public static string AskKeyword (Editor editor, string message, IEnumerable<string> keywords, string defaultKeyword)
        {
            var options = new PromptKeywordOptions ("\n" + message) { AllowNone = true };
            foreach (string keyword in keywords) options.Keywords.Add (keyword);
            if (!string.IsNullOrEmpty (defaultKeyword)) options.Keywords.Default = defaultKeyword;
            PromptResult result = editor.GetKeywords (options);
            if (result.Status == PromptStatus.None) return defaultKeyword;
            if (result.Status != PromptStatus.OK) return null;
            return result.StringResult;
        }

        public static bool AskYesNo (Editor editor, string message, bool defaultYes)
        {
            string answer = AskKeyword (editor, message, new[] { "Evet", "Hayir" }, defaultYes ? "Evet" : "Hayir");
            return string.Equals (answer, "Evet", StringComparison.OrdinalIgnoreCase);
        }

        public static Point3d? PickPoint (Editor editor, string message)
        {
            var options = new PromptPointOptions ("\n" + message + ": ") { AllowNone = false };
            PromptPointResult result = editor.GetPoint (options);
            if (result.Status != PromptStatus.OK) return null;
            return result.Value;
        }

        /// <summary>
        /// Asks for the area objects. Returns null when nothing was selected.
        /// </summary>
        public static ObjectId[] SelectAreaObjects (Editor editor, string message)
        {
            var filterValues = new List<TypedValue> {
                new TypedValue ((int) DxfCode.Start, string.Join (",", GeometryUtil.SupportedDxfNames))
            };
            var options = new PromptSelectionOptions {
                MessageForAdding = "\n" + message,
                MessageForRemoval = "\nSeçimden çıkarılacak nesneler"
            };
            PromptSelectionResult result = editor.GetSelection (options, new SelectionFilter (filterValues.ToArray ()));
            if (result.Status != PromptStatus.OK || result.Value == null || result.Value.Count == 0) return null;
            return result.Value.GetObjectIds ();
        }

        public static ObjectId? PickEntity (Editor editor, string message)
        {
            var options = new PromptEntityOptions ("\n" + message + ": ");
            PromptEntityResult result = editor.GetEntity (options);
            if (result.Status != PromptStatus.OK) return null;
            return result.ObjectId;
        }

        /// <summary>Ensures a layer exists and returns its name.</summary>
        public static string EnsureLayer (Database database, Transaction transaction, string layerName, short colorIndex)
        {
            var layerTable = (LayerTable) transaction.GetObject (database.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has (layerName)) return layerName;
            layerTable.UpgradeOpen ();
            var record = new LayerTableRecord {
                Name = layerName,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex (Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
            };
            layerTable.Add (record);
            transaction.AddNewlyCreatedDBObject (record, true);
            return layerName;
        }

        public static BlockTableRecord CurrentSpace (Database database, Transaction transaction) =>
            (BlockTableRecord) transaction.GetObject (database.CurrentSpaceId, OpenMode.ForWrite);
    }
}
