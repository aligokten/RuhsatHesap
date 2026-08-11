using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using RuhsatHesap.Core;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Reporting;
using AcadWindows = Autodesk.AutoCAD.Windows;

namespace RuhsatHesap.Acad.Commands
{
    /// <summary>Excel, CSV ve JSON aktarım komutları.</summary>
    public sealed class ExportCommands
    {
        [CommandMethod ("RHEXCEL", CommandFlags.Modal)]
        public void ExportExcel ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;

            try {
                ProjectData project = DrawingStore.LoadProject (document.Database);
                if (!HasData (editor, project)) return;

                string path = AskSavePath (document, "Excel çalışma kitabını kaydet", "xlsx");
                if (path == null) return;

                IReadOnlyList<ReportTable> tables = ReportBuilder.AllProjectTables (project);
                XlsxWriter.Write (path, tables);
                AcadUi.Write (editor, tables.Count + " sayfalı çalışma kitabı yazıldı: " + path);
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Excel dosyası yazılamadı: " + exception.Message);
            }
        }

        [CommandMethod ("RHCSV", CommandFlags.Modal)]
        public void ExportCsv ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;

            try {
                ProjectData project = DrawingStore.LoadProject (document.Database);
                if (!HasData (editor, project)) return;

                string path = AskSavePath (document, "Tabloları CSV olarak kaydet", "csv");
                if (path == null) return;

                CsvWriter.Write (path, ReportBuilder.AllProjectTables (project));
                AcadUi.Write (editor, "CSV dosyası yazıldı: " + path);
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "CSV dosyası yazılamadı: " + exception.Message);
            }
        }

        [CommandMethod ("RHJSONKAYDET", CommandFlags.Modal)]
        public void SaveJson ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;

            try {
                ProjectData project = DrawingStore.LoadProject (document.Database);
                if (!HasData (editor, project)) return;

                string path = AskSavePath (document, "Proje verisini JSON olarak kaydet", "json");
                if (path == null) return;

                File.WriteAllText (path, ProjectJson.Serialize (project, true), new System.Text.UTF8Encoding (false));
                AcadUi.Write (editor, "Proje dosyası yazıldı: " + path);
                AcadUi.Write (editor, "Bu dosya Ruhsat Hesap web paneli ve Archicad eklentisi ile de açılabilir.");
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "JSON dosyası yazılamadı: " + exception.Message);
            }
        }

        [CommandMethod ("RHJSONAC", CommandFlags.Modal)]
        public void OpenJson ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;

            try {
                var dialog = new AcadWindows.OpenFileDialog ("Ruhsat Hesap proje dosyasını aç", string.Empty, "json",
                    "RuhsatHesapJsonOpen", AcadWindows.OpenFileDialog.OpenFileDialogFlags.NoUrls);
                if (dialog.ShowDialog () != System.Windows.Forms.DialogResult.OK) return;

                string text = File.ReadAllText (dialog.Filename);
                ProjectData project = ProjectJson.Deserialize (text);

                if (DrawingStore.HasProject (document.Database) &&
                    !AcadUi.AskYesNo (editor, "Çizimdeki mevcut proje verisi değiştirilsin mi? [Evet/Hayir]", false)) {
                    AcadUi.Write (editor, "Vazgeçildi.");
                    return;
                }

                using (document.LockDocument ()) {
                    DrawingStore.SaveProject (document.Database, project);
                }

                CalculationSummary summary = CalculationEngine.Calculate (project);
                AcadUi.Write (editor, "Proje okundu: " + project.Blocks.Count + " blok, " + summary.UnitCount + " bağımsız bölüm.");
                AcadUi.Write (editor, "Hesaplanan emsal: " + TextUtil.FormatArea (summary.CalculatedEmsal) + " m²");
                AcadUi.Write (editor, "RHTARA çalıştırırsanız çizimdeki etiketli alanlar bu veriye yeniden işlenir.");
            } catch (FormatException exception) {
                AcadUi.Write (editor, "JSON dosyası okunamadı: " + exception.Message);
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "JSON dosyası açılamadı: " + exception.Message);
            }
        }

        private static bool HasData (Editor editor, ProjectData project)
        {
            if (project.Blocks.Count > 0 || project.Parcel.ParcelArea > 0.0) return true;
            AcadUi.Write (editor, "Çizimde proje verisi yok. Önce RHETIKET + RHTARA çalıştırın.");
            return false;
        }

        /// <summary>Suggests a file name next to the DWG and asks where to save.</summary>
        private static string AskSavePath (Document document, string title, string extension)
        {
            string drawingName = document.Database.Filename;
            string suggestion = string.IsNullOrEmpty (drawingName)
                ? "RuhsatHesap." + extension
                : Path.Combine (Path.GetDirectoryName (drawingName) ?? string.Empty,
                    Path.GetFileNameWithoutExtension (drawingName) + "_RuhsatHesap." + extension);

            var dialog = new AcadWindows.SaveFileDialog (title, suggestion, extension, "RuhsatHesapSave",
                AcadWindows.SaveFileDialog.SaveFileDialogFlags.NoFtpSites);
            if (dialog.ShowDialog () != System.Windows.Forms.DialogResult.OK) return null;
            return dialog.Filename;
        }
    }
}
