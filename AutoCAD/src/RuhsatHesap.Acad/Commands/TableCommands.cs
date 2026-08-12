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
                if (!InsertTables (document, settings, tables, "Tabloların sol üst köşesini belirtin")) return;
                AcadUi.Write (editor, tables.Count + " tablo çizildi.");
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Tablolar oluşturulamadı: " + exception.Message);
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
                if (!InsertTables (document, settings, new[] { table }, "Tablonun sol üst köşesini belirtin")) return;
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

        private static bool InsertTables (Document document, DrawingSettings settings,
            IEnumerable<ReportTable> tables, string message)
        {
            Editor editor = document.Editor;
            Point3d? position = AcadUi.PickPoint (editor, message);
            if (position == null) {
                AcadUi.Write (editor, "Yerleştirme iptal edildi.");
                return false;
            }

            using (document.LockDocument ())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction ()) {
                BlockTableRecord space = AcadUi.CurrentSpace (document.Database, transaction);
                TableRenderer.InsertStack (document.Database, transaction, space, tables, position.Value, settings);
                transaction.Commit ();
            }
            return true;
        }
    }
}
