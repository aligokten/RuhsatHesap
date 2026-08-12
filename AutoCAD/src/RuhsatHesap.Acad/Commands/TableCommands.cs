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
using RuhsatHesap.Core.Reporting;
using RuhsatHesap.Core.Tagging;

namespace RuhsatHesap.Acad.Commands
{
    /// <summary>Çizime tablo yerleştiren komutlar.</summary>
    public sealed class TableCommands
    {
        [CommandMethod ("RHALANTABLO", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void AreaTable () => SelectionTable (false);

        [CommandMethod ("RHALANOZET", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void AreaSummaryTable () => SelectionTable (true);

        /// <summary>
        /// Polyline seçimli alan hesap tablosu. Etiketsiz polylinelar da
        /// listelenir, böylece hızlı bir alan dökümü için etiketleme zorunlu
        /// değildir.
        /// </summary>
        private void SelectionTable (bool grouped)
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            try {
                DrawingSettings settings = DrawingStore.LoadSettings (database);
                ObjectId[] ids = AcadUi.SelectAreaObjects (editor, "Alanı hesaplanacak polylineları seçin");
                if (ids == null) {
                    AcadUi.Write (editor, "Nesne seçilmedi.");
                    return;
                }

                var stats = new ScanStats ();
                List<AreaObservation> observations;
                using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                    observations = DrawingScanner.Collect (database, transaction, ids, settings, true, stats);
                    transaction.Commit ();
                }
                if (observations.Count == 0) {
                    AcadUi.Write (editor, "Seçimde ölçülebilir kapalı alan bulunamadı.");
                    return;
                }

                ReportTable table = grouped
                    ? ReportBuilder.AreaSummary (observations)
                    : ReportBuilder.AreaList (observations);

                if (!InsertTables (document, settings, new[] { table }, "Tablonun sol üst köşesini belirtin")) return;

                AcadUi.Write (editor, observations.Count + " nesne, toplam " +
                    TextUtil.FormatArea (observations.Sum (item => item.Area)) + " m² tabloya yazıldı.");
                foreach (string warning in stats.Warnings) AcadUi.Write (editor, "  ! " + warning);
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Tablo oluşturulamadı: " + exception.Message);
            }
        }

        [CommandMethod ("RHEMSAL", CommandFlags.Modal)]
        public void EmsalTable () => ProjectTable (project => ReportBuilder.Emsal (project));

        [CommandMethod ("RHBB", CommandFlags.Modal)]
        public void UnitTable () => ProjectTable (project => ReportBuilder.Units (project));

        [CommandMethod ("RHINSAAT", CommandFlags.Modal)]
        public void ConstructionTable () => ProjectTable (project => ReportBuilder.Construction (project));

        [CommandMethod ("RHINSAATALANI", CommandFlags.Modal)]
        public void ConstructionByFloorTable () => ProjectTable (project => ReportBuilder.ConstructionByFloor (project));

        [CommandMethod ("RHIRTIFAK", CommandFlags.Modal)]
        public void CondominiumTable () => ProjectTable (project => ReportBuilder.Condominium (project));

        [CommandMethod ("RHOZET", CommandFlags.Modal)]
        public void SummaryTable () => ProjectTable (project => ReportBuilder.Summary (project));

        [CommandMethod ("RHOTOPARKAGAC", CommandFlags.Modal)]
        public void ParkingAndTreesTable () => ProjectTable (project => ReportBuilder.ParkingAndTrees (project));

        [CommandMethod ("RHTABLOLAR", CommandFlags.Modal)]
        public void AllTables ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            try {
                DrawingSettings settings = DrawingStore.LoadSettings (database);
                ProjectData project = DrawingStore.LoadProject (database);
                if (!EnsureProjectData (editor, project)) return;

                IReadOnlyList<ReportTable> tables = ReportBuilder.AllProjectTables (project);
                if (!InsertTables (document, settings, tables, "Tabloların sol üst köşesini belirtin", true)) return;
                AcadUi.Write (editor, tables.Count + " tablo çizildi.");
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Tablolar oluşturulamadı: " + exception.Message);
            }
        }

        /// <summary>
        /// Erases every Ruhsat Hesap tablosu (RH-TABLO katmanı) in the model
        /// uzayı and the active çıktı düzeni. RHTABLOLAR draws a brand new copy
        /// each time rather than updating one in place, so after a re-tarama a
        /// stale table can sit on the canvas next to the fresh one, showing an
        /// outdated kat list -- this clears that ambiguity in one step.
        /// </summary>
        [CommandMethod ("RHTABLOTEMIZLE", CommandFlags.Modal)]
        public void ClearTables ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            using (document.LockDocument ())
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                int erased = EraseExistingTables (database, transaction);
                transaction.Commit ();
                AcadUi.Write (editor, erased > 0
                    ? erased + " tablo silindi."
                    : "RH-TABLO katmanında silinecek tablo bulunamadı.");
            }
        }

        private void ProjectTable (Func<ProjectData, ReportTable> builder)
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            try {
                DrawingSettings settings = DrawingStore.LoadSettings (database);
                ProjectData project = DrawingStore.LoadProject (database);
                if (!EnsureProjectData (editor, project)) return;

                ReportTable table = builder (project);
                if (!InsertTables (document, settings, new[] { table }, "Tablonun sol üst köşesini belirtin", true)) return;
                AcadUi.Write (editor, table.Title + " çizildi.");
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Tablo oluşturulamadı: " + exception.Message);
            }
        }

        private static bool EnsureProjectData (Editor editor, ProjectData project)
        {
            if (project.Blocks.Count > 0 || project.Parcel.ParcelArea > 0.0) return true;
            AcadUi.Write (editor, "Çizimde proje verisi yok. Önce alanları RHETIKET ile etiketleyip RHTARA çalıştırın " +
                "veya RHJSONAC ile bir proje dosyası açın.");
            return false;
        }

        /// <param name="offerToReplaceOld">
        /// Ruhsat hesap tablolarını yeniden çizen komutlar için true: RHTARA
        /// sonrası tekrar çalıştırıldığında, eski (artık güncel olmayan) tablo
        /// canvasta kalıp kafa karıştırmasın diye önce silinmesi önerilir.
        /// Seçime dayalı RHALANTABLO/RHALANOZET için false -- o tablolar birer
        /// anlık görüntüdür, birikmeleri beklenen bir davranıştır.
        /// </param>
        private static bool InsertTables (Document document, DrawingSettings settings,
            IEnumerable<ReportTable> tables, string message, bool offerToReplaceOld = false)
        {
            Editor editor = document.Editor;
            Database database = document.Database;

            if (offerToReplaceOld) {
                int existing = CountExistingTables (database);
                if (existing > 0 && AcadUi.AskYesNo (editor,
                        existing + " adet önceki Ruhsat Hesap tablosu bulundu. Yeni tablo(lar) çizilmeden önce " +
                        "eskiler silinsin mi? [Evet/Hayır]", true)) {
                    using (document.LockDocument ())
                    using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                        EraseExistingTables (database, transaction);
                        transaction.Commit ();
                    }
                }
            }

            Point3d? position = AcadUi.PickPoint (editor, message);
            if (position == null) {
                AcadUi.Write (editor, "Yerleştirme iptal edildi.");
                return false;
            }

            using (document.LockDocument ())
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                BlockTableRecord space = AcadUi.CurrentSpace (database, transaction);
                TableRenderer.InsertStack (database, transaction, space, tables, position.Value, settings);
                transaction.Commit ();
            }
            return true;
        }

        /// <summary>Model uzayı ve aktif düzendeki RH-TABLO katmanlı tablo sayısı.</summary>
        private static int CountExistingTables (Database database)
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction ()) {
                int count = CountOrEraseTables (database, transaction, erase: false);
                transaction.Commit ();
                return count;
            }
        }

        private static int EraseExistingTables (Database database, Transaction transaction) =>
            CountOrEraseTables (database, transaction, erase: true);

        private static int CountOrEraseTables (Database database, Transaction transaction, bool erase)
        {
            int total = 0;
            foreach (ObjectId spaceId in new[] { GetModelSpaceId (database, transaction), database.CurrentSpaceId }) {
                if (spaceId.IsNull || spaceId.IsErased) continue;
                var space = (BlockTableRecord) transaction.GetObject (spaceId, OpenMode.ForRead);
                foreach (ObjectId id in space) {
                    if (id.IsErased) continue;
                    if (!(transaction.GetObject (id, OpenMode.ForRead) is Table table)) continue;
                    if (!string.Equals (table.Layer, TableRenderer.TableLayer, StringComparison.OrdinalIgnoreCase)) continue;
                    total++;
                    if (erase) {
                        table.UpgradeOpen ();
                        table.Erase ();
                    }
                }
            }
            return total;
        }

        private static ObjectId GetModelSpaceId (Database database, Transaction transaction)
        {
            var blockTable = (BlockTable) transaction.GetObject (database.BlockTableId, OpenMode.ForRead);
            return blockTable[BlockTableRecord.ModelSpace];
        }
    }
}
