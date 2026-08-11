using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using RuhsatHesap.Core;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Tagging;

namespace RuhsatHesap.Acad.Commands
{
    /// <summary>Etiketleme ve çizim tarama komutları.</summary>
    public sealed class TaggingCommands
    {
        [CommandMethod ("RHETIKET", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void TagObjects ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            try {
                DrawingSettings settings = DrawingStore.LoadSettings (database);
                ObjectId[] ids = AcadUi.SelectAreaObjects (editor, "Etiketlenecek alan nesnelerini seçin");
                if (ids == null) {
                    AcadUi.Write (editor, "Nesne seçilmedi.");
                    return;
                }

                RuhsatTag defaults = ReadFirstTag (database, ids) ?? new RuhsatTag {
                    BlockName = settings.ActiveBlock,
                    FloorName = settings.ActiveFloor,
                    AreaTypeName = "NET",
                    Kind = AreaKind.Net
                };

                RuhsatTag tag = AskTag (editor, settings, defaults);
                if (tag == null) {
                    AcadUi.Write (editor, "Etiketleme iptal edildi.");
                    return;
                }

                string tagText = tag.Format ();
                int tagged = 0;
                double total = 0.0;
                using (document.LockDocument ())
                using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                    string labelLayer = settings.WriteLabels
                        ? AcadUi.EnsureLayer (database, transaction, settings.LabelLayer, 4)
                        : null;

                    foreach (ObjectId id in ids) {
                        var entity = transaction.GetObject (id, OpenMode.ForWrite) as Entity;
                        if (entity == null) continue;
                        TagStorage.WriteTag (database, transaction, entity, tagText);
                        tagged++;
                        if (GeometryUtil.TryGetArea (entity, out double area, out bool unusedClosed))
                            total += area * settings.AreaFactor;
                        if (labelLayer != null) WriteLabel (transaction, entity, tagText, labelLayer, settings);
                    }
                    transaction.Commit ();
                }

                AcadUi.Write (editor, "Etiket: " + tagText);
                AcadUi.Write (editor, tagged + " nesne etiketlendi, toplam alan " + TextUtil.FormatArea (total) + " m².");
                AcadUi.Write (editor, "Proje verisini güncellemek için RHTARA komutunu çalıştırın.");
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Etiketleme başarısız: " + exception.Message);
            }
        }

        [CommandMethod ("RHETIKETSIL", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ClearTags ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            ObjectId[] ids = AcadUi.SelectAreaObjects (editor, "Etiketi silinecek nesneleri seçin");
            if (ids == null) return;

            int cleared = 0;
            using (document.LockDocument ())
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                foreach (ObjectId id in ids) {
                    var entity = transaction.GetObject (id, OpenMode.ForWrite) as Entity;
                    if (entity == null || TagStorage.ReadTag (entity).Length == 0) continue;
                    TagStorage.ClearTag (database, transaction, entity);
                    cleared++;
                }
                transaction.Commit ();
            }
            AcadUi.Write (editor, cleared + " nesnenin etiketi silindi.");
        }

        [CommandMethod ("RHSOR", CommandFlags.Modal)]
        public void Inspect ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            ObjectId? id = AcadUi.PickEntity (editor, "İncelenecek nesneyi seçin");
            if (id == null) return;

            DrawingSettings settings = DrawingStore.LoadSettings (database);
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                var entity = transaction.GetObject (id.Value, OpenMode.ForRead) as Entity;
                if (entity == null) return;

                string tagText = TagStorage.ReadTag (entity);
                string source = "XDATA";
                if (tagText.Length == 0 && TagStorage.TryParseLayerTag (entity.Layer, out string layerTag)) {
                    tagText = layerTag;
                    source = "katman adı";
                }

                AcadUi.WriteHeader (editor, "NESNE BİLGİSİ");
                AcadUi.Write (editor, "Tür / katman : " + entity.GetType ().Name + " / " + entity.Layer);
                if (GeometryUtil.TryGetArea (entity, out double area, out bool closed)) {
                    AcadUi.Write (editor, "Alan         : " + TextUtil.FormatArea (area * settings.AreaFactor) + " m²" +
                        (closed ? string.Empty : "  (DİKKAT: polyline kapalı değil)"));
                } else {
                    AcadUi.Write (editor, "Alan         : ölçülemedi");
                }

                if (tagText.Length == 0) {
                    AcadUi.Write (editor, "Etiket       : yok — RHETIKET ile etiketleyebilirsiniz.");
                    transaction.Commit ();
                    return;
                }

                RuhsatTag tag = RuhsatTag.Parse (tagText);
                AcadUi.Write (editor, "Etiket       : " + tagText + "  (" + source + ")");
                AcadUi.Write (editor, "Blok / BB    : " + tag.BlockName + " / " + (tag.UnitNumber.Length > 0 ? tag.UnitNumber : "-"));
                AcadUi.Write (editor, "Kat          : " + (tag.FloorName.Length > 0 ? tag.FloorName : "(etikette yok, aktif kat kullanılır)"));
                AcadUi.Write (editor, "Tip          : " + tag.AreaTypeName + " — " + RuhsatTag.Describe (tag.Kind));
                if (tag.ToThirtyPercentTable) AcadUi.Write (editor, "Hesap        : Emsal Hesabı %30 istisna tablosu");
                if (!tag.Valid) AcadUi.Write (editor, "UYARI        : " + tag.Error);
                transaction.Commit ();
            }
        }

        [CommandMethod ("RHTARA", CommandFlags.Modal)]
        public void ScanDrawing ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            try {
                DrawingSettings settings = DrawingStore.LoadSettings (database);
                ProjectData project = DrawingStore.LoadProject (database);
                var stats = new ScanStats ();
                List<AreaObservation> observations;

                using (document.LockDocument ())
                using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                    observations = DrawingScanner.Collect (database, transaction,
                        DrawingScanner.ModelSpaceIds (database, transaction), settings, false, stats);
                    transaction.Commit ();
                }

                TagSyncResult result = TagSync.Sync (project, observations);
                DrawingStore.SaveProject (database, project);

                AcadUi.WriteHeader (editor, "ÇİZİM TARAMASI");
                AcadUi.Write (editor, "Çizim birimi     : " + DrawingUnitInfo.Label (settings.Unit));
                AcadUi.Write (editor, "Etiketli nesne   : " + stats.Tagged + " (kat sınırı: " + stats.FloorFrames + ")");
                AcadUi.Write (editor, "Okunan alan      : " + result.Recognized + ", geçersiz: " + result.Invalid);
                AcadUi.Write (editor, "Blok / kat / BB  : +" + result.CreatedBlocks + " blok, +" + result.CreatedFloors +
                    " kat, +" + result.CreatedUnits + " yeni BB, " + result.UpdatedUnits + " güncellenen BB");
                if (result.ParcelArea > 0.0) AcadUi.Write (editor, "Parsel alanı     : " + TextUtil.FormatArea (result.ParcelArea) + " m²");
                if (result.FootprintArea > 0.0) AcadUi.Write (editor, "Yapı oturumu     : " + TextUtil.FormatArea (result.FootprintArea) + " m²");
                if (result.CommonArea > 0.0) AcadUi.Write (editor, "Ortak alan       : " + TextUtil.FormatArea (result.CommonArea) + " m²");
                if (result.ShelterArea > 0.0) AcadUi.Write (editor, "Sığınak          : " + TextUtil.FormatArea (result.ShelterArea) + " m²");
                if (result.RetainingWalls > 0) AcadUi.Write (editor, "İstinat duvarı   : " + result.RetainingWalls + " adet");

                CalculationSummary summary = CalculationEngine.Calculate (project);
                AcadUi.Write (editor, "Hesaplanan emsal : " + TextUtil.FormatArea (summary.CalculatedEmsal) + " m²" +
                    (summary.MaxEmsal > 0.0
                        ? (summary.EmsalOk
                            ? "  (bakiye " + TextUtil.FormatArea (summary.EmsalBalance) + " m²)"
                            : "  (AŞIM " + TextUtil.FormatArea (summary.EmsalExcess) + " m²)")
                        : "  (emsal hakkı için RHPARSEL)"));

                foreach (string warning in stats.Warnings) AcadUi.Write (editor, "  ! " + warning);
                foreach (string problem in result.Problems) AcadUi.Write (editor, "  ! " + problem);
                if (stats.Tagged == 0)
                    AcadUi.Write (editor, "Etiketli nesne bulunamadı. RHETIKET ile alanları etiketleyin.");
                AcadUi.Write (editor, "Proje verisi çizime kaydedildi. Tablolar için RHTABLOLAR, Excel için RHEXCEL.");
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Tarama başarısız: " + exception.Message);
            }
        }

        private static RuhsatTag ReadFirstTag (Database database, IEnumerable<ObjectId> ids)
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                foreach (ObjectId id in ids) {
                    var entity = transaction.GetObject (id, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;
                    string text = TagStorage.ReadTag (entity);
                    if (text.Length == 0) continue;
                    RuhsatTag tag = RuhsatTag.Parse (text);
                    if (tag.IsRuhsatTag) {
                        transaction.Commit ();
                        return tag;
                    }
                }
                transaction.Commit ();
            }
            return null;
        }

        /// <summary>Asks for every etiket field, pre-filled from the selection.</summary>
        private static RuhsatTag AskTag (Editor editor, DrawingSettings settings, RuhsatTag defaults)
        {
            var tag = new RuhsatTag { IsRuhsatTag = true };

            AcadUi.Write (editor, "TIP değerleri: " + string.Join (", ", RuhsatTag.ReservedTypes));
            AcadUi.Write (editor, "Listede olmayan bir TIP yazarsanız aynı adla yeni bir alan satırı açılır.");
            string typeName = AcadUi.AskString (editor, "TIP", defaults.AreaTypeName.Length > 0
                ? defaults.AreaTypeName
                : RuhsatTag.DefaultTypeName (defaults.Kind));
            if (typeName == null || typeName.Length == 0) return null;
            tag.AreaTypeName = typeName;
            tag.Kind = RuhsatTag.ParseAreaType (typeName);

            if (tag.Kind == AreaKind.ParcelBoundary || tag.Kind == AreaKind.BuildingFootprint)
                return tag;

            if (tag.Kind == AreaKind.RetainingWall) {
                string wallName = AcadUi.AskString (editor, "İstinat duvarı adı", defaults.Label);
                if (wallName == null) return null;
                tag.Label = wallName;
                return tag;
            }

            if (tag.Kind != AreaKind.FloorFrame) {
                string blockName = AcadUi.AskString (editor, "BLOK",
                    defaults.BlockName.Length > 0 ? defaults.BlockName : settings.ActiveBlock);
                if (blockName == null || blockName.Length == 0) return null;
                tag.BlockName = TextUtil.Normalize (blockName);
            }

            if (tag.Kind != AreaKind.Common) {
                string floorName = AcadUi.AskString (editor, "KAT",
                    defaults.FloorName.Length > 0 ? defaults.FloorName : settings.ActiveFloor);
                if (floorName == null) return null;
                tag.FloorName = floorName;
            }

            if (tag.IsUnitArea) {
                string unitNumber = AcadUi.AskString (editor, "Bağımsız bölüm no (BB)", defaults.UnitNumber);
                if (unitNumber == null || unitNumber.Length == 0) return null;
                tag.UnitNumber = unitNumber;

                string roomName = AcadUi.AskString (editor, "Mahal adı (isteğe bağlı)", defaults.RoomName);
                if (roomName == null) return null;
                tag.RoomName = roomName;

                int? roomCount = AcadUi.AskInteger (editor, "Oda sayısı", defaults.RoomCount);
                if (roomCount == null) return null;
                tag.RoomCount = roomCount.Value;

                string quality = AcadUi.AskString (editor, "Nitelik (Mesken, Dükkan ...)",
                    defaults.Quality.Length > 0 ? defaults.Quality : "Mesken");
                if (quality == null) return null;
                tag.Quality = quality;
            }

            if (RuhsatTag.SupportsHesapEmsal (tag.Kind) || tag.Kind == AreaKind.Unknown) {
                tag.ToThirtyPercentTable = AcadUi.AskYesNo (editor,
                    "Emsal Hesabı %30 istisna tablosuna yazılsın mı? [Evet/Hayir]", defaults.ToThirtyPercentTable);
                // A free TIP is only resolved during the scan; treat it as a
                // custom floor area so HESAP=EMSAL is written into the etiket.
                if (tag.Kind == AreaKind.Unknown) tag.Kind = AreaKind.CustomFloorArea;
            }

            return tag;
        }

        /// <summary>
        /// Draws the etiket text inside the outline, on its own layer. The
        /// label is written into the same space the tagged object lives in, so
        /// tagging from a paper space viewport does not scatter texts.
        /// </summary>
        private static void WriteLabel (Transaction transaction, Entity entity,
            string tagText, string layer, DrawingSettings settings)
        {
            var space = transaction.GetObject (entity.BlockId, OpenMode.ForWrite) as BlockTableRecord;
            if (space == null) return;
            Point3d anchor = GeometryUtil.RepresentativePoint (entity);
            var label = new MText {
                Contents = tagText,
                Location = anchor,
                TextHeight = settings.EffectiveTextHeight * 0.7,
                Attachment = AttachmentPoint.MiddleCenter,
                Layer = layer
            };
            space.AppendEntity (label);
            transaction.AddNewlyCreatedDBObject (label, true);
        }
    }
}
