using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using RuhsatHesap.Core.Tagging;

namespace RuhsatHesap.Acad
{
    /// <summary>
    /// Stores the RH etiketi on the drawing object. The etiket lives in XDATA
    /// under the RUHSATHESAP application name, so it is invisible, survives
    /// copy/paste and WBLOCK, and never disturbs the drawing's appearance.
    /// Two fallbacks are read as well, for drawings that were annotated before
    /// the plug-in was installed: an RH| text placed inside the closed outline,
    /// and the RH-BLOK_A-TIP_NET layer-name convention. Both conversions live
    /// in <see cref="TagText"/>.
    /// </summary>
    public static class TagStorage
    {
        public const string ApplicationName = "RUHSATHESAP";
        private const int ChunkSize = 200;

        public static void EnsureRegApp (Database database, Transaction transaction)
        {
            var table = (RegAppTable) transaction.GetObject (database.RegAppTableId, OpenMode.ForRead);
            if (table.Has (ApplicationName)) return;
            table.UpgradeOpen ();
            var record = new RegAppTableRecord { Name = ApplicationName };
            table.Add (record);
            transaction.AddNewlyCreatedDBObject (record, true);
        }

        /// <summary>Returns the etiket stored on the entity, or an empty string.</summary>
        public static string ReadTag (Entity entity)
        {
            using (ResultBuffer buffer = entity.GetXDataForApplication (ApplicationName)) {
                if (buffer == null) return string.Empty;
                var builder = new StringBuilder ();
                foreach (TypedValue value in buffer) {
                    if (value.TypeCode == (short) DxfCode.ExtendedDataAsciiString && value.Value != null)
                        builder.Append (value.Value.ToString ());
                }
                return builder.ToString ();
            }
        }

        public static void WriteTag (Database database, Transaction transaction, Entity entity, string tagText)
        {
            EnsureRegApp (database, transaction);
            if (!entity.IsWriteEnabled) entity.UpgradeOpen ();
            var values = new List<TypedValue> {
                new TypedValue ((int) DxfCode.ExtendedDataRegAppName, ApplicationName)
            };
            tagText = tagText ?? string.Empty;
            for (int offset = 0; offset < tagText.Length; offset += ChunkSize) {
                int length = Math.Min (ChunkSize, tagText.Length - offset);
                values.Add (new TypedValue ((int) DxfCode.ExtendedDataAsciiString, tagText.Substring (offset, length)));
            }
            using (var buffer = new ResultBuffer (values.ToArray ()))
                entity.XData = buffer;
        }

        public static void ClearTag (Database database, Transaction transaction, Entity entity)
        {
            EnsureRegApp (database, transaction);
            if (!entity.IsWriteEnabled) entity.UpgradeOpen ();
            // An XDATA buffer holding only the application name removes this
            // application's data from the entity.
            using (var buffer = new ResultBuffer (new TypedValue ((int) DxfCode.ExtendedDataRegAppName, ApplicationName)))
                entity.XData = buffer;
        }

        public static bool TryParseLayerTag (string layerName, out string tagText) =>
            TagText.TryParseLayerName (layerName, out tagText);

        public static bool LooksLikeTag (string text) => TagText.LooksLikeTag (text);

        public static string StripMTextFormatting (string text) => TagText.StripMTextFormatting (text);
    }
}
