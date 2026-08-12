using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using RuhsatHesap.Core;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Tagging;

namespace RuhsatHesap.Acad.Commands
{
    /// <summary>Ayar, parsel ve proje verisi komutları.</summary>
    public sealed class SettingsCommands
    {
        [CommandMethod ("RHYARDIM", CommandFlags.Modal)]
        public void Help ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;

            AcadUi.WriteHeader (editor, "RUHSAT HESAP — AUTOCAD EKLENTİSİ KOMUTLARI");
            editor.WriteMessage ("\n  Ayarlar");
            editor.WriteMessage ("\n    RHAYAR        Çizim birimi, aktif kat/blok ve tablo yazı yüksekliği");
            editor.WriteMessage ("\n    RHBIRIM       Yalnızca çizim birimini değiştirir (m / cm / mm)");
            editor.WriteMessage ("\n    RHKAT         Etiketlenecek aktif katı belirler");
            editor.WriteMessage ("\n    RHPARSEL      Parsel, ada, TAKS/KAKS bilgilerini girer");
            editor.WriteMessage ("\n    RHOTOPARK     Projede ayrılan otopark sayısını girer");
            editor.WriteMessage ("\n  Etiketleme");
            editor.WriteMessage ("\n    RHETIKET      Seçilen polylinelara RH etiketi yazar");
            editor.WriteMessage ("\n    RHETIKETSIL   Seçilen nesnelerden RH etiketini siler");
            editor.WriteMessage ("\n    RHSOR         Bir nesnenin etiketini ve alanını gösterir");
            editor.WriteMessage ("\n    RHTARA        Çizimi tarar, proje verisini günceller");
            editor.WriteMessage ("\n  Tablolar");
            editor.WriteMessage ("\n    RHALANTABLO   Seçilen polylinelardan alan hesap tablosu");
            editor.WriteMessage ("\n    RHALANOZET    Seçimi blok/kat/tip bazında özetler");
            editor.WriteMessage ("\n    RHEMSAL       Emsal hesap tablosu");
            editor.WriteMessage ("\n    RHBB          Bağımsız bölüm alan tablosu");
            editor.WriteMessage ("\n    RHINSAAT      Yapı inşaat alanı tablosu");
            editor.WriteMessage ("\n    RHINSAATALANI Kat bazlı inşaat alanı tablosu");
            editor.WriteMessage ("\n    RHIRTIFAK     Kat irtifakı tablosu");
            editor.WriteMessage ("\n    RHOZET        Ruhsat hesap özeti");
            editor.WriteMessage ("\n    RHTABLOLAR    Bütün tabloları alt alta çizer");
            editor.WriteMessage ("\n  Aktarım");
            editor.WriteMessage ("\n    RHEXCEL       Formatlı .xlsx çalışma kitabı üretir");
            editor.WriteMessage ("\n    RHCSV         Tabloları .csv olarak yazar");
            editor.WriteMessage ("\n    RHJSONKAYDET  Proje verisini .json olarak kaydeder");
            editor.WriteMessage ("\n    RHJSONAC      Web paneli / Archicad JSON dosyasını okur");
            editor.WriteMessage ("\n    RHVERI        Çizimdeki proje verisinin özetini yazar");
            editor.WriteMessage ("\n    RHTEMIZLE     Çizimdeki Ruhsat Hesap verisini siler");
            editor.WriteMessage ("\n");
            editor.WriteMessage ("\n  Etiket biçimi: RH|BLOK=A|BB=01|KAT=ZEMİN KAT|TIP=NET|ODA=3");
            editor.WriteMessage ("\n  TIP değerleri : " + string.Join (", ", RuhsatTag.ReservedTypes));
            editor.WriteMessage ("\n");
        }

        [CommandMethod ("RHAYAR", CommandFlags.Modal)]
        public void Settings ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            try {
                DrawingSettings settings = DrawingStore.LoadSettings (database);
                AcadUi.WriteHeader (editor, "RUHSAT HESAP AYARLARI");

                string unitKeyword = AcadUi.AskKeyword (editor,
                    "Çizim birimi [Metre/Santimetre/Milimetre]",
                    new[] { "Metre", "Santimetre", "Milimetre" },
                    DrawingUnitInfo.Keyword (settings.Unit));
                if (unitKeyword == null) return;
                if (DrawingUnitInfo.TryParse (unitKeyword, out DrawingUnit unit)) settings.Unit = unit;

                string block = AcadUi.AskString (editor, "Aktif blok adı", settings.ActiveBlock);
                if (block == null) return;
                settings.ActiveBlock = block.Length == 0 ? "A" : TextUtil.Normalize (block);

                string floor = AcadUi.AskString (editor, "Aktif kat adı (KAT= yazılmayan etiketler için)", settings.ActiveFloor);
                if (floor == null) return;
                settings.ActiveFloor = floor;

                double? textHeight = AcadUi.AskDouble (editor,
                    "Tablo yazı yüksekliği (çizim birimi)", settings.EffectiveTextHeight);
                if (textHeight == null) return;
                settings.TableTextHeight = textHeight.Value;

                string textStyle = AcadUi.AskString (editor,
                    "Tablo yazı tipi / stil adı", settings.TableTextStyle);
                if (textStyle == null) return;
                settings.TableTextStyle = textStyle;

                settings.WriteLabels = AcadUi.AskYesNo (editor, "Etiketlerken görünür yazı da eklensin mi? [Evet/Hayir]", settings.WriteLabels);

                DrawingStore.SaveSettings (database, settings);
                ReportSettings (editor, settings);
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Ayarlar kaydedilemedi: " + exception.Message);
            }
        }

        [CommandMethod ("RHBIRIM", CommandFlags.Modal)]
        public void Unit ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            DrawingSettings settings = DrawingStore.LoadSettings (database);
            string keyword = AcadUi.AskKeyword (editor, "Çizim birimi [Metre/Santimetre/Milimetre]",
                new[] { "Metre", "Santimetre", "Milimetre" }, DrawingUnitInfo.Keyword (settings.Unit));
            if (keyword == null) return;
            if (!DrawingUnitInfo.TryParse (keyword, out DrawingUnit unit)) return;
            settings.Unit = unit;
            DrawingStore.SaveSettings (database, settings);
            AcadUi.Write (editor, "Çizim birimi: " + DrawingUnitInfo.Label (unit) +
                " (1 çizim birimi² = " + TextUtil.FormatArea (settings.AreaFactor, 6) + " m²)");
        }

        [CommandMethod ("RHKAT", CommandFlags.Modal)]
        public void ActiveFloor ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            DrawingSettings settings = DrawingStore.LoadSettings (database);
            string floor = AcadUi.AskString (editor, "Aktif kat adı", settings.ActiveFloor);
            if (floor == null) return;
            settings.ActiveFloor = floor;
            DrawingStore.SaveSettings (database, settings);
            AcadUi.Write (editor, floor.Length > 0
                ? "Aktif kat: " + floor
                : "Aktif kat temizlendi; etiketlerde KAT= kullanın veya kat sınırı çizin.");
        }

        [CommandMethod ("RHPARSEL", CommandFlags.Modal)]
        public void Parcel ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            try {
                DrawingSettings settings = DrawingStore.LoadSettings (database);
                ProjectData project = DrawingStore.LoadProject (database);
                ParcelInfo parcel = project.Parcel;

                AcadUi.WriteHeader (editor, "PARSEL BİLGİLERİ");
                string value;
                if ((value = AcadUi.AskString (editor, "Proje adı", parcel.ProjectName)) == null) return;
                parcel.ProjectName = value;
                if ((value = AcadUi.AskString (editor, "İl", parcel.City)) == null) return;
                parcel.City = value;
                if ((value = AcadUi.AskString (editor, "İlçe", parcel.District)) == null) return;
                parcel.District = value;
                if ((value = AcadUi.AskString (editor, "Mahalle", parcel.Neighborhood)) == null) return;
                parcel.Neighborhood = value;
                if ((value = AcadUi.AskString (editor, "Ada", parcel.Block)) == null) return;
                parcel.Block = value;
                if ((value = AcadUi.AskString (editor, "Parsel", parcel.Parcel)) == null) return;
                parcel.Parcel = value;

                if (AcadUi.AskYesNo (editor, "Parsel alanı çizimden okunsun mu? [Evet/Hayir]", parcel.ParcelArea <= 0.0)) {
                    double? measured = MeasureSelection (document, settings, "Parsel sınırı polylineını seçin");
                    if (measured != null) parcel.ParcelArea = Math.Round (measured.Value, 2);
                }
                double? area = AcadUi.AskDouble (editor, "Parsel alanı (m²)", parcel.ParcelArea);
                if (area == null) return;
                parcel.ParcelArea = area.Value;

                double? taks = AcadUi.AskDouble (editor, "TAKS oranı (örn. 0.40)", parcel.TaksRate);
                if (taks == null) return;
                parcel.TaksRate = taks.Value;

                string method = AcadUi.AskKeyword (editor, "Emsal yöntemi [Kaks/Dogrudan]",
                    new[] { "Kaks", "Dogrudan" }, parcel.EmsalMethod == "direct" ? "Dogrudan" : "Kaks");
                if (method == null) return;
                parcel.EmsalMethod = string.Equals (method, "Dogrudan", StringComparison.OrdinalIgnoreCase) ? "direct" : "kaks";

                if (parcel.EmsalMethod == "direct") {
                    double? direct = AcadUi.AskDouble (editor, "Doğrudan emsal hakkı (m²)", parcel.DirectEmsal);
                    if (direct == null) return;
                    parcel.DirectEmsal = direct.Value;
                } else {
                    double? kaks = AcadUi.AskDouble (editor, "KAKS / emsal oranı (örn. 1.50)", parcel.KaksRate);
                    if (kaks == null) return;
                    parcel.KaksRate = kaks.Value;
                }

                if (AcadUi.AskYesNo (editor, "Yapı oturum alanı çizimden okunsun mu? [Evet/Hayir]", parcel.BuildingFootprint <= 0.0)) {
                    double? measured = MeasureSelection (document, settings, "Yapı oturum alanı polylineını seçin");
                    if (measured != null) parcel.BuildingFootprint = Math.Round (measured.Value, 2);
                }
                double? footprint = AcadUi.AskDouble (editor, "Yapı oturum alanı (m²)", parcel.BuildingFootprint);
                if (footprint == null) return;
                parcel.BuildingFootprint = footprint.Value;

                DrawingStore.SaveProject (database, project);

                CalculationSummary summary = CalculationEngine.Calculate (project);
                AcadUi.Write (editor, "Azami TAKS alanı : " + TextUtil.FormatArea (summary.MaxFootprint) + " m²");
                AcadUi.Write (editor, "Azami emsal alanı: " + TextUtil.FormatArea (summary.MaxEmsal) + " m²");
                AcadUi.Write (editor, "Parsel bilgileri çizime kaydedildi. (DWG'yi kaydetmeyi unutmayın.)");
            } catch (System.Exception exception) {
                AcadUi.Write (editor, "Parsel bilgileri kaydedilemedi: " + exception.Message);
            }
        }

        [CommandMethod ("RHOTOPARK", CommandFlags.Modal)]
        public void Parking ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            ProjectData project = DrawingStore.LoadProject (database);
            CalculationSummary summary = CalculationEngine.Calculate (project);
            AcadUi.Write (editor, "Gerekli otopark: " + summary.RequiredParkingSpaces + " araç");
            int? provided = AcadUi.AskInteger (editor, "Projede ayrılan otopark sayısı", project.ProvidedParkingSpaces);
            if (provided == null) return;
            project.ProvidedParkingSpaces = provided.Value;
            DrawingStore.SaveProject (database, project);
            AcadUi.Write (editor, summary.ParkingOk (project.ProvidedParkingSpaces)
                ? "Otopark sayısı sağlandı."
                : "Eksik otopark: " + (summary.RequiredParkingSpaces - project.ProvidedParkingSpaces) + " araç");
        }

        [CommandMethod ("RHVERI", CommandFlags.Modal)]
        public void ProjectInfo ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            Database database = document.Database;

            DrawingSettings settings = DrawingStore.LoadSettings (database);
            ProjectData project = DrawingStore.LoadProject (database);
            CalculationSummary summary = CalculationEngine.Calculate (project);

            AcadUi.WriteHeader (editor, "ÇİZİMDEKİ RUHSAT HESAP VERİSİ");
            ReportSettings (editor, settings);
            AcadUi.Write (editor, "Proje            : " + (project.Parcel.ProjectName.Length > 0 ? project.Parcel.ProjectName : "(adsız)"));
            AcadUi.Write (editor, "Ada / Parsel     : " + project.Parcel.Block + " / " + project.Parcel.Parcel);
            AcadUi.Write (editor, "Parsel alanı     : " + TextUtil.FormatArea (project.Parcel.ParcelArea) + " m²");
            AcadUi.Write (editor, "Blok sayısı      : " + project.Blocks.Count);
            AcadUi.Write (editor, "Bağımsız bölüm   : " + summary.UnitCount);
            AcadUi.Write (editor, "Hesaplanan emsal : " + TextUtil.FormatArea (summary.CalculatedEmsal) + " m²");
            AcadUi.Write (editor, "İzin verilen emsal: " + TextUtil.FormatArea (summary.MaxEmsal) + " m²");
            AcadUi.Write (editor, "Yapı inşaat alanı: " + TextUtil.FormatArea (summary.ConstructionArea) + " m²");
            foreach (BlockRecord block in project.Blocks) {
                AcadUi.Write (editor, "  " + block.Name + " blok: " + block.Floors.Count + " kat, " +
                    block.Units.Count + " bağımsız bölüm");
            }
        }

        [CommandMethod ("RHTEMIZLE", CommandFlags.Modal)]
        public void ClearData ()
        {
            Document document = AcadUi.ActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;

            if (!AcadUi.AskYesNo (editor, "Çizimdeki bütün Ruhsat Hesap verisi silinsin mi? [Evet/Hayir]", false)) {
                AcadUi.Write (editor, "Vazgeçildi.");
                return;
            }
            using (document.LockDocument ()) {
                DrawingStore.Clear (document.Database);
            }
            AcadUi.Write (editor, "Proje verisi silindi. Etiketler çizimde kalmaya devam eder; RHTARA ile yeniden okunabilir.");
        }

        private static void ReportSettings (Editor editor, DrawingSettings settings)
        {
            AcadUi.Write (editor, "Çizim birimi     : " + DrawingUnitInfo.Label (settings.Unit));
            AcadUi.Write (editor, "Aktif blok / kat : " + settings.ActiveBlock + " / " +
                (settings.ActiveFloor.Length > 0 ? settings.ActiveFloor : "(tanımsız)"));
            AcadUi.Write (editor, "Tablo yazı yük.  : " + TextUtil.FormatArea (settings.EffectiveTextHeight, 3));
            AcadUi.Write (editor, "Tablo yazı tipi  : " +
                (settings.TableTextStyle.Length > 0 ? settings.TableTextStyle : "(tablo stilinden)"));
        }

        /// <summary>Lets the user select outlines and returns their total area in m².</summary>
        internal static double? MeasureSelection (Document document, DrawingSettings settings, string message)
        {
            Editor editor = document.Editor;
            Autodesk.AutoCAD.DatabaseServices.ObjectId[] ids = AcadUi.SelectAreaObjects (editor, message);
            if (ids == null) return null;

            double total = 0.0;
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction ()) {
                foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId id in ids) {
                    var entity = transaction.GetObject (id, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;
                    if (GeometryUtil.TryGetArea (entity, out double area, out bool unusedClosed))
                        total += area * settings.AreaFactor;
                }
                transaction.Commit ();
            }
            AcadUi.Write (editor, "Ölçülen alan: " + TextUtil.FormatArea (total) + " m² (" + ids.Length + " nesne)");
            return total;
        }
    }
}
