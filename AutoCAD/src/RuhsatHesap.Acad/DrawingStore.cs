using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using RuhsatHesap.Core.Model;

namespace RuhsatHesap.Acad
{
    /// <summary>
    /// Keeps the Ruhsat Hesap project data inside the DWG itself, in an Xrecord
    /// under the Named Object Dictionary. This is the AutoCAD counterpart of
    /// the Archicad add-on's Add-On Object storage: the data travels with the
    /// drawing and needs a normal QSAVE to reach the file on disk.
    /// </summary>
    public static class DrawingStore
    {
        public const string DictionaryName = "RUHSATHESAP";
        private const string ProjectKey = "PROJECT";
        private const string SettingsKey = "SETTINGS";
        /// <summary>Xrecord strings are chunked so no single value gets close
        /// to the DXF string length limit.</summary>
        private const int ChunkSize = 250;

        public static ProjectData LoadProject (Database database)
        {
            string text = ReadString (database, ProjectKey);
            if (string.IsNullOrEmpty (text)) return new ProjectData ();
            try {
                return ProjectJson.Deserialize (text);
            } catch (FormatException) {
                // A corrupted record must not block the command; the user can
                // re-scan the drawing or import a JSON file instead.
                return new ProjectData ();
            }
        }

        public static void SaveProject (Database database, ProjectData project) =>
            WriteString (database, ProjectKey, ProjectJson.Serialize (project, false));

        public static bool HasProject (Database database) => !string.IsNullOrEmpty (ReadString (database, ProjectKey));

        public static DrawingSettings LoadSettings (Database database)
        {
            string text = ReadString (database, SettingsKey);
            if (string.IsNullOrEmpty (text)) {
                var fresh = new DrawingSettings { Unit = DrawingSettings.FromInsUnits ((int) database.Insunits) };
                return fresh;
            }
            return DrawingSettings.FromJson (text);
        }

        public static void SaveSettings (Database database, DrawingSettings settings) =>
            WriteString (database, SettingsKey, settings.ToJson ());

        public static void Clear (Database database)
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                var namedObjects = (DBDictionary) transaction.GetObject (database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (namedObjects.Contains (DictionaryName)) {
                    namedObjects.UpgradeOpen ();
                    namedObjects.Remove (DictionaryName);
                }
                transaction.Commit ();
            }
        }

        private static string ReadString (Database database, string key)
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                var namedObjects = (DBDictionary) transaction.GetObject (database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!namedObjects.Contains (DictionaryName)) return null;
                var dictionary = (DBDictionary) transaction.GetObject (namedObjects.GetAt (DictionaryName), OpenMode.ForRead);
                if (!dictionary.Contains (key)) return null;
                var record = (Xrecord) transaction.GetObject (dictionary.GetAt (key), OpenMode.ForRead);
                if (record.Data == null) return null;

                var builder = new StringBuilder ();
                foreach (TypedValue value in record.Data)
                    if (value.TypeCode == (short) DxfCode.Text && value.Value != null)
                        builder.Append (value.Value.ToString ());
                transaction.Commit ();
                return builder.ToString ();
            }
        }

        private static void WriteString (Database database, string key, string text)
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                var namedObjects = (DBDictionary) transaction.GetObject (database.NamedObjectsDictionaryId, OpenMode.ForWrite);
                DBDictionary dictionary;
                if (namedObjects.Contains (DictionaryName)) {
                    dictionary = (DBDictionary) transaction.GetObject (namedObjects.GetAt (DictionaryName), OpenMode.ForWrite);
                } else {
                    dictionary = new DBDictionary ();
                    namedObjects.SetAt (DictionaryName, dictionary);
                    transaction.AddNewlyCreatedDBObject (dictionary, true);
                }

                var record = new Xrecord { Data = new ResultBuffer (Chunk (text)) };
                dictionary.SetAt (key, record);
                transaction.AddNewlyCreatedDBObject (record, true);
                transaction.Commit ();
            }
        }

        private static TypedValue[] Chunk (string text)
        {
            var values = new List<TypedValue> ();
            text = text ?? string.Empty;
            for (int offset = 0; offset < text.Length; offset += ChunkSize) {
                int length = Math.Min (ChunkSize, text.Length - offset);
                values.Add (new TypedValue ((int) DxfCode.Text, text.Substring (offset, length)));
            }
            if (values.Count == 0) values.Add (new TypedValue ((int) DxfCode.Text, string.Empty));
            return values.ToArray ();
        }
    }
}
