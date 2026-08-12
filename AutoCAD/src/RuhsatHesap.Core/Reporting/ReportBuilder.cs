using System;
using System.Collections.Generic;
using System.Linq;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Tagging;

namespace RuhsatHesap.Core.Reporting
{
    /// <summary>
    /// Builds every Ruhsat Hesap table from project data. The layouts mirror
    /// Src/ReportExport.cpp so the AutoCAD tables, the Excel workbook and the
    /// Archicad add-on's pafta show the same columns in the same order.
    /// </summary>
    public static class ReportBuilder
    {
        public static IReadOnlyList<string> UsedThirtyPercentKeys (ProjectData project) =>
            CollectKeys (project, floor => floor.ThirtyPercentAreas, null);

        public static IReadOnlyList<string> UsedConstructionKeys (ProjectData project) =>
            CollectKeys (project, floor => floor.ConstructionAreas, "bagimsiz_bolum_brut");

        private static IReadOnlyList<string> CollectKeys (ProjectData project,
            Func<FloorRecord, Dictionary<string, double>> selector, string excluded)
        {
            var keys = new List<string> ();
            foreach (BlockRecord block in project.Blocks)
                foreach (FloorRecord floor in block.Floors)
                    foreach (string key in selector (floor).Keys)
                        if (key != excluded && !keys.Contains (key)) keys.Add (key);
            keys.Sort (StringComparer.Ordinal);
            return keys;
        }

        // ------------------------------------------------------------------
        // Polyline seçimli alan tabloları
        // ------------------------------------------------------------------

        /// <summary>
        /// One row per selected polyline. This is the table the RHALANTABLO
        /// command draws next to the plan.
        /// </summary>
        public static ReportTable AreaList (IReadOnlyList<AreaObservation> observations, string title = "ALAN HESAP TABLOSU")
        {
            var table = new ReportTable (title, "Alan Listesi");
            table.Column ("Sıra", 7)
                 .Column ("Blok", 8)
                 .Column ("BB No", 9)
                 .Column ("Kat", 16)
                 .Column ("Tip", 18)
                 .Column ("Mahal / Açıklama", 22)
                 .Column ("Alan (m²)", 14, true);

            int order = 1;
            foreach (AreaObservation observation in OrderObservations (observations)) {
                RuhsatTag tag = observation.Tag;
                string floorName = observation.FloorName.Length > 0 ? observation.FloorName : tag.FloorName;
                string typeName = tag.IsRuhsatTag
                    ? (tag.AreaTypeName.Length > 0 ? tag.AreaTypeName.ToUpperInvariant () : RuhsatTag.DefaultTypeName (tag.Kind))
                    : "ETİKETSİZ";
                string description = tag.RoomName.Length > 0 ? tag.RoomName
                    : tag.Label.Length > 0 ? tag.Label
                    : observation.Layer;
                table.AddRow (RowKind.Data,
                    ReportCell.OfText (order.ToString (System.Globalization.CultureInfo.InvariantCulture)),
                    ReportCell.OfText (tag.BlockName),
                    ReportCell.OfText (tag.UnitNumber),
                    ReportCell.OfText (floorName),
                    ReportCell.OfText (typeName),
                    ReportCell.OfText (description),
                    ReportCell.OfNumber (observation.Area));
                order++;
            }

            table.AddRow (RowKind.GrandTotal,
                ReportCell.OfText ("TOPLAM", 6),
                ReportCell.OfNumber (observations.Sum (item => item.Area)));
            return table;
        }

        /// <summary>Same selection, totalled per blok / kat / tip.</summary>
        public static ReportTable AreaSummary (IReadOnlyList<AreaObservation> observations, string title = "ALAN ÖZET TABLOSU")
        {
            var table = new ReportTable (title, "Alan Özeti");
            table.Column ("Blok", 9)
                 .Column ("Kat", 18)
                 .Column ("Tip", 20)
                 .Column ("Adet", 8, true, 0)
                 .Column ("Alan (m²)", 15, true);

            var groups = OrderObservations (observations)
                .GroupBy (observation => new {
                    Block = observation.Tag.BlockName,
                    Floor = observation.FloorName.Length > 0 ? observation.FloorName : observation.Tag.FloorName,
                    Type = observation.Tag.IsRuhsatTag
                        ? (observation.Tag.AreaTypeName.Length > 0
                            ? observation.Tag.AreaTypeName.ToUpperInvariant ()
                            : RuhsatTag.DefaultTypeName (observation.Tag.Kind))
                        : "ETİKETSİZ"
                });

            foreach (var group in groups) {
                table.AddRow (RowKind.Data,
                    ReportCell.OfText (group.Key.Block),
                    ReportCell.OfText (group.Key.Floor),
                    ReportCell.OfText (group.Key.Type),
                    ReportCell.OfInteger (group.Count ()),
                    ReportCell.OfNumber (group.Sum (item => item.Area)));
            }

            table.AddRow (RowKind.GrandTotal,
                ReportCell.OfText ("GENEL TOPLAM", 3),
                ReportCell.OfInteger (observations.Count),
                ReportCell.OfNumber (observations.Sum (item => item.Area)));
            return table;
        }

        private static IEnumerable<AreaObservation> OrderObservations (IReadOnlyList<AreaObservation> observations) =>
            observations
                .OrderBy (item => item.Tag.BlockName, StringComparer.Ordinal)
                .ThenBy (item => FloorOrder.Rank (item.FloorName.Length > 0 ? item.FloorName : item.Tag.FloorName))
                .ThenBy (item => item.FloorName, StringComparer.Ordinal)
                .ThenBy (item => item.Tag.UnitNumber, TextUtil.UnitNumberComparer.Instance)
                .ThenBy (item => item.Tag.AreaTypeName, StringComparer.Ordinal);

        // ------------------------------------------------------------------
        // Ruhsat hesap tabloları
        // ------------------------------------------------------------------

        public static ReportTable Emsal (ProjectData project)
        {
            IReadOnlyList<string> keys = UsedThirtyPercentKeys (project);
            var table = new ReportTable ("EMSAL HESAP TABLOSU", "Emsal Hesabı");
            table.Column ("Blok", 9).Column ("Kat", 18);
            foreach (string key in keys) table.Column (TextUtil.FriendlyName (key), 15, true);
            table.Column ("%30 Dahil Toplam", 16, true)
                 .Column ("Emsal Dışı", 14, true)
                 .Column ("Emsal Alan", 14, true)
                 .Column ("Toplam İnşaat", 16, true);

            foreach (BlockRecord block in project.Blocks) {
                var blockCells = new List<double> (Enumerable.Repeat (0.0, keys.Count + 4));
                foreach (FloorRecord floor in block.Floors) {
                    var cells = new List<ReportCell> {
                        ReportCell.OfText (block.Name),
                        ReportCell.OfText (floor.Name)
                    };
                    int index = 0;
                    foreach (string key in keys) {
                        floor.ThirtyPercentAreas.TryGetValue (key, out double value);
                        cells.Add (ReportCell.OfNumber (value));
                        blockCells[index++] += value;
                    }
                    double thirtyTotal = CalculationEngine.SumValues (floor.ThirtyPercentAreas);
                    double total = thirtyTotal + floor.EmsalOutsideArea + floor.EmsalArea;
                    cells.Add (ReportCell.OfNumber (thirtyTotal));
                    cells.Add (ReportCell.OfNumber (floor.EmsalOutsideArea));
                    cells.Add (ReportCell.OfNumber (floor.EmsalArea));
                    cells.Add (ReportCell.OfNumber (total));
                    blockCells[index++] += thirtyTotal;
                    blockCells[index++] += floor.EmsalOutsideArea;
                    blockCells[index++] += floor.EmsalArea;
                    blockCells[index] += total;
                    table.AddRow (RowKind.Data, cells.ToArray ());
                }

                var totalCells = new List<ReportCell> { ReportCell.OfText (block.Name + " BLOK TOPLAMI", 2) };
                totalCells.AddRange (blockCells.Select (value => ReportCell.OfNumber (value)));
                table.AddRow (RowKind.BlockTotal, totalCells.ToArray ());
            }

            AddEmptyNotice (table, "Emsal verisi yok — kat sınırlarını " +
                "RH|BLOK=A|KAT=…|TIP=EMSAL etiketleyip RHTARA çalıştırın.");

            var grandCells = new List<ReportCell> { ReportCell.OfText ("GENEL TOPLAM", 2) };
            for (int column = 2; column < table.ColumnCount; column++) {
                double total = table.Rows
                    .Where (row => row.Kind == RowKind.BlockTotal)
                    .Sum (row => ReportTable.CellAt (row, column)?.Value ?? 0.0);
                grandCells.Add (ReportCell.OfNumber (total));
            }
            table.AddRow (RowKind.GrandTotal, grandCells.ToArray ());

            CalculationSummary summary = CalculationEngine.Calculate (project);
            table.AddSection ("EMSAL KONTROLÜ");
            table.AddLabelValue (RowKind.Data, project.Parcel.EmsalMethod == "direct"
                ? "İzin verilen emsal alanı (doğrudan)"
                : "İzin verilen emsal alanı (Parsel × KAKS)", ReportCell.OfNumber (summary.MaxEmsal));
            table.AddLabelValue (RowKind.Data, "Hesaplanan emsal alanı", ReportCell.OfNumber (summary.CalculatedEmsal));
            table.AddLabelValue (RowKind.GrandTotal,
                summary.EmsalOk ? "Kalan emsal hakkı" : "EMSAL AŞIMI",
                ReportCell.OfNumber (summary.EmsalOk ? summary.EmsalBalance : summary.EmsalExcess));
            return table;
        }

        public static ReportTable Units (ProjectData project)
        {
            var table = new ReportTable ("BAĞIMSIZ BÖLÜM ALAN TABLOSU", "Bağımsız Bölümler");
            table.Column ("Blok", 8)
                 .Column ("BB No", 9)
                 .Column ("Kat", 16)
                 .Column ("Nitelik", 14)
                 .Column ("Oda", 7, true, 0)
                 .Column ("BB Brüt", 13, true)
                 .Column ("Eklenti Brüt", 14, true)
                 .Column ("Toplam Brüt", 14, true)
                 .Column ("Genel Brüt", 14, true)
                 .Column ("BB Net", 13, true)
                 .Column ("Eklenti Net", 14, true)
                 .Column ("%20 Balkon", 14, true)
                 .Column ("Balkon Alanı", 14, true)
                 .Column ("Otopark Payı", 14, true);

            int unitCount = project.Blocks.Sum (block => block.Units.Count);
            double commonPerUnit = unitCount == 0 ? 0.0 : project.CommonArea / unitCount;

            foreach (BlockRecord block in project.Blocks) {
                foreach (IndependentUnit unit in block.Units) {
                    table.AddRow (RowKind.Data,
                        ReportCell.OfText (block.Name),
                        ReportCell.OfText (unit.Number),
                        ReportCell.OfText (unit.Floor),
                        ReportCell.OfText (unit.Quality),
                        ReportCell.OfInteger (unit.RoomCount),
                        ReportCell.OfNumber (unit.GrossArea),
                        ReportCell.OfNumber (unit.ExtensionGrossArea),
                        ReportCell.OfNumber (unit.GrossArea + unit.ExtensionGrossArea),
                        ReportCell.OfNumber (unit.GrossArea + commonPerUnit),
                        ReportCell.OfNumber (unit.NetArea),
                        ReportCell.OfNumber (unit.ExtensionNetArea),
                        ReportCell.OfNumber (unit.NetArea / 5.0),
                        ReportCell.OfNumber (unit.BalconyArea),
                        ReportCell.OfNumber (CalculationEngine.ParkingContribution (unit.GrossArea)));
                }
            }

            if (AddEmptyNotice (table, "Bağımsız bölüm verisi yok — polylineları " +
                    "RH|BLOK=A|BB=01|KAT=…|TIP=NET (ve TIP=BRUT) etiketleyip RHTARA çalıştırın."))
                return table;

            var totals = new List<ReportCell> { ReportCell.OfText ("GENEL TOPLAM", 5) };
            for (int column = 5; column < table.ColumnCount; column++)
                totals.Add (ReportCell.OfNumber (table.ColumnSum (column)));
            table.AddRow (RowKind.GrandTotal, totals.ToArray ());
            return table;
        }

        public static ReportTable Condominium (ProjectData project)
        {
            var table = new ReportTable ("KAT İRTİFAKI TABLOSU", "Kat İrtifakı");
            table.Column ("Blok", 8)
                 .Column ("BB No", 9)
                 .Column ("Arsa Payı", 12)
                 .Column ("Bulunduğu Kat", 16)
                 .Column ("Niteliği", 14)
                 .Column ("Eklenti", 11)
                 .Column ("BB Brüt Alanı", 15, true)
                 .Column ("BB Net Alanı", 15, true)
                 .Column ("Maliki", 18);

            foreach (BlockRecord block in project.Blocks) {
                foreach (IndependentUnit unit in block.Units) {
                    table.AddRow (RowKind.Data,
                        ReportCell.OfText (block.Name),
                        ReportCell.OfText (unit.Number),
                        ReportCell.OfText (unit.LandShare),
                        ReportCell.OfText (unit.Floor),
                        ReportCell.OfText (unit.Quality),
                        ReportCell.OfText (unit.ExtensionGrossArea > 0.0 ? "Eklenti" : string.Empty),
                        ReportCell.OfNumber (unit.GrossArea),
                        ReportCell.OfNumber (unit.NetArea),
                        ReportCell.OfText (unit.Owner));
                }
            }

            if (AddEmptyNotice (table, "Kat irtifakı verisi yok — bağımsız bölümler " +
                    "TIP=NET / TIP=BRUT etiketlerinden okunur."))
                return table;

            table.AddRow (RowKind.GrandTotal,
                ReportCell.OfText ("GENEL TOPLAM", 6),
                ReportCell.OfNumber (table.ColumnSum (6)),
                ReportCell.OfNumber (table.ColumnSum (7)),
                ReportCell.Empty);
            return table;
        }

        public static ReportTable Construction (ProjectData project)
        {
            IReadOnlyList<string> keys = UsedConstructionKeys (project);
            var table = new ReportTable ("YAPI İNŞAAT ALANI", "Yapı İnşaat Alanı");
            table.Column ("Blok", 9).Column ("Kat", 18).Column ("BB Brüt Alanı", 16, true);
            foreach (string key in keys) table.Column (TextUtil.FriendlyName (key), 15, true);
            table.Column ("Kat Yüzölçümü", 16, true);

            foreach (BlockRecord block in project.Blocks) {
                foreach (FloorRecord floor in block.Floors) {
                    var cells = new List<ReportCell> {
                        ReportCell.OfText (block.Name),
                        ReportCell.OfText (floor.Name)
                    };
                    double unitGross = block.UnitGrossOnFloor (floor.Name);
                    cells.Add (ReportCell.OfNumber (unitGross));
                    double extraTotal = 0.0;
                    foreach (string key in keys) {
                        floor.ConstructionAreas.TryGetValue (key, out double value);
                        extraTotal += value;
                        cells.Add (ReportCell.OfNumber (value));
                    }
                    cells.Add (ReportCell.OfNumber (unitGross + extraTotal));
                    table.AddRow (RowKind.Data, cells.ToArray ());
                }
            }

            if (AddEmptyNotice (table, "Yapı inşaat alanı verisi yok — kat kalemlerini " +
                    "TIP=MERDIVEN, TIP=ASANSOR, TIP=SIGINAK … etiketleyin."))
                return table;

            var totals = new List<ReportCell> { ReportCell.OfText ("GENEL TOPLAM", 2) };
            for (int column = 2; column < table.ColumnCount; column++)
                totals.Add (ReportCell.OfNumber (table.ColumnSum (column)));
            table.AddRow (RowKind.GrandTotal, totals.ToArray ());
            return table;
        }

        /// <summary>Kat bazlı inşaat alanı: satırlar kat, sütunlar blok.</summary>
        public static ReportTable ConstructionByFloor (ProjectData project)
        {
            var table = new ReportTable ("İNŞAAT ALANI HESABI", "İnşaat Alanı");
            table.Column ("Kat / Alan", 22);
            foreach (BlockRecord block in project.Blocks) table.Column (block.Name + " BLOK", 16, true);
            table.Column ("TOPLAM", 16, true);

            // Different blocks may spell the same kat slightly differently
            // ("1.KAT" vs "1. Kat"); collapse those onto one row the same way
            // BlockRecord.FindFloor does, or the row is silently duplicated.
            var floorNames = new List<string> ();
            var seenFloorKeys = new HashSet<string> (StringComparer.Ordinal);
            foreach (BlockRecord block in project.Blocks) {
                foreach (FloorRecord floor in block.Floors) {
                    string key = TextUtil.NormalizeFloorKey (floor.Name);
                    if (seenFloorKeys.Add (key)) floorNames.Add (floor.Name);
                }
            }
            floorNames = floorNames.OrderBy (FloorOrder.Rank).ToList ();

            foreach (string floorName in floorNames) {
                var cells = new List<ReportCell> { ReportCell.OfText (floorName) };
                double rowTotal = 0.0;
                foreach (BlockRecord block in project.Blocks) {
                    FloorRecord floor = block.FindFloor (floorName);
                    double value = 0.0;
                    if (floor != null) {
                        value = block.UnitGrossOnFloor (floorName);
                        foreach (KeyValuePair<string, double> entry in floor.ConstructionAreas)
                            if (entry.Key != "bagimsiz_bolum_brut") value += entry.Value;
                    }
                    rowTotal += value;
                    cells.Add (ReportCell.OfNumber (value));
                }
                cells.Add (ReportCell.OfNumber (rowTotal));
                table.AddRow (RowKind.Data, cells.ToArray ());
            }

            double retainingTotal = project.RetainingWalls.Sum (wall => wall.Area);
            if (retainingTotal > 0.0) {
                var wallCells = new List<ReportCell> { ReportCell.OfText ("İstinat Duvarı") };
                foreach (BlockRecord unusedBlock in project.Blocks) wallCells.Add (ReportCell.OfText ("—"));
                wallCells.Add (ReportCell.OfNumber (retainingTotal));
                table.AddRow (RowKind.Section, wallCells.ToArray ());
            }

            double extraStructureTotal = project.ExtraStructures.Sum (structure => structure.Area);
            if (extraStructureTotal > 0.0) {
                var extraCells = new List<ReportCell> { ReportCell.OfText ("Ek Yapılar (Foseptik vb.)") };
                foreach (BlockRecord unusedBlock in project.Blocks) extraCells.Add (ReportCell.OfText ("—"));
                extraCells.Add (ReportCell.OfNumber (extraStructureTotal));
                table.AddRow (RowKind.Section, extraCells.ToArray ());
            }

            var totals = new List<ReportCell> { ReportCell.OfText ("GENEL TOPLAM") };
            for (int column = 1; column < table.ColumnCount; column++) {
                double total = table.ColumnSum (column);
                if (column == table.ColumnCount - 1) total += retainingTotal + extraStructureTotal;
                totals.Add (ReportCell.OfNumber (total));
            }
            table.AddRow (RowKind.GrandTotal, totals.ToArray ());
            return table;
        }

        public static ReportTable RetainingWalls (ProjectData project)
        {
            var table = new ReportTable ("İSTİNAT DUVARI ALAN HESABI", "İstinat Duvarı");
            table.Column ("İstinat Duvarı", 28).Column ("Alan (m²)", 16, true);
            foreach (RetainingWall wall in project.RetainingWalls)
                table.AddRow (RowKind.Data, ReportCell.OfText (wall.Name), ReportCell.OfNumber (wall.Area));
            table.AddRow (RowKind.GrandTotal, ReportCell.OfText ("TOPLAM"), ReportCell.OfNumber (table.ColumnSum (1)));
            return table;
        }

        /// <summary>
        /// Site-level items outside istinat duvarı -- foseptik, trafo, su
        /// deposu binası vb. -- tagged with TIP=EK_YAPI. Their total feeds
        /// Yapı İnşaat Alanı the same way İstinat Duvarı does.
        /// </summary>
        public static ReportTable ExtraStructures (ProjectData project)
        {
            var table = new ReportTable ("EK YAPILAR (FOSEPTİK, TRAFO, SU DEPOSU VB.)", "Ek Yapılar");
            table.Column ("Ek Yapı", 28).Column ("Alan (m²)", 16, true);
            foreach (ExtraStructure structure in project.ExtraStructures)
                table.AddRow (RowKind.Data, ReportCell.OfText (structure.Name), ReportCell.OfNumber (structure.Area));
            table.AddRow (RowKind.GrandTotal, ReportCell.OfText ("TOPLAM"), ReportCell.OfNumber (table.ColumnSum (1)));
            return table;
        }

        /// <summary>Parsel bilgileri, TAKS/KAKS kontrolleri, ağaç ve otopark.</summary>
        public static ReportTable Summary (ProjectData project)
        {
            CalculationSummary summary = CalculationEngine.Calculate (project);
            var table = new ReportTable ("RUHSAT HESAP ÖZETİ", "Özet");
            table.Column ("Hesap Kalemi", 34).Column ("Değer", 20, true);

            table.AddSection ("PARSEL BİLGİLERİ");
            AddText (table, "Proje", project.Parcel.ProjectName);
            AddText (table, "İl / İlçe", Join (project.Parcel.City, project.Parcel.District));
            AddText (table, "Mahalle", project.Parcel.Neighborhood);
            AddText (table, "Ada / Parsel", Join (project.Parcel.Block, project.Parcel.Parcel));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Parsel Alanı (m²)"), ReportCell.OfNumber (project.Parcel.ParcelArea));
            AddText (table, "TAKS Oranı", TextUtil.FormatRate (project.Parcel.TaksRate));
            AddText (table, "KAKS / Emsal Oranı", TextUtil.FormatRate (project.Parcel.KaksRate));

            table.AddSection ("TAKS / KAKS KONTROLLERİ");
            table.AddRow (RowKind.Data, ReportCell.OfText ("Azami TAKS Alanı (m²)"), ReportCell.OfNumber (summary.MaxFootprint));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Yapı Oturum Alanı (m²)"), ReportCell.OfNumber (project.Parcel.BuildingFootprint));
            AddText (table, "TAKS Kontrolü", project.Parcel.BuildingFootprint <= summary.MaxFootprint + 0.005
                ? "UYGUN"
                : "AŞIM: " + TextUtil.FormatArea (project.Parcel.BuildingFootprint - summary.MaxFootprint) + " m²");
            table.AddRow (RowKind.Data, ReportCell.OfText ("Azami Emsal Alanı (m²)"), ReportCell.OfNumber (summary.MaxEmsal));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Hesaplanan Emsal (m²)"), ReportCell.OfNumber (summary.CalculatedEmsal));
            AddText (table, "Emsal Kontrolü", summary.EmsalOk
                ? "UYGUN — bakiye " + TextUtil.FormatArea (summary.EmsalBalance) + " m²"
                : "AŞIM: " + TextUtil.FormatArea (summary.EmsalExcess) + " m²");

            table.AddSection ("ALAN TOPLAMLARI");
            table.AddRow (RowKind.Data, ReportCell.OfText ("Emsal Dışı Alan (m²)"), ReportCell.OfNumber (summary.EmsalOutsideTotal));
            table.AddRow (RowKind.Data, ReportCell.OfText ("%30 İstisna Tablosu Toplamı (m²)"), ReportCell.OfNumber (summary.ThirtyPercentTotal));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Toplam Ortak Alan (m²)"), ReportCell.OfNumber (project.CommonArea));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Yapı İnşaat Alanı (m²)"), ReportCell.OfNumber (summary.ConstructionArea));
            table.AddRow (RowKind.Data, ReportCell.OfText ("İstinat Duvarı (m²)"), ReportCell.OfNumber (summary.RetainingWallArea));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Ek Yapılar (Foseptik vb.) (m²)"), ReportCell.OfNumber (summary.ExtraStructureArea));
            table.AddRow (RowKind.GrandTotal, ReportCell.OfText ("TOPLAM İNŞAAT ALANI (m²)"), ReportCell.OfNumber (summary.ConstructionGrandTotal));

            table.AddSection ("DİĞER HESAPLAR");
            table.AddRow (RowKind.Data, ReportCell.OfText ("Bağımsız Bölüm Sayısı"), ReportCell.OfInteger (summary.UnitCount));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Gerekli Ağaç Sayısı"), ReportCell.OfInteger (summary.RequiredTrees));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Gerekli Otopark"), ReportCell.OfInteger (summary.RequiredParkingSpaces));
            table.AddRow (RowKind.Data, ReportCell.OfText ("Projede Ayrılan Otopark"), ReportCell.OfInteger (project.ProvidedParkingSpaces));
            AddText (table, "Otopark Kontrolü", summary.ParkingOk (project.ProvidedParkingSpaces)
                ? "SAĞLANDI"
                : "EKSİK: " + (summary.RequiredParkingSpaces - project.ProvidedParkingSpaces));
            return table;
        }

        /// <summary>
        /// Otopark ve ağaç sayısı yalnızca tek bir sayı olarak verildiğinde
        /// nereden geldiği belli olmuyor. Bu tablo her ikisini de yönetmelik
        /// aralıklarını ve bağımsız bölüm bazlı payları göstererek açıklar.
        /// </summary>
        public static ReportTable ParkingAndTrees (ProjectData project)
        {
            CalculationSummary summary = CalculationEngine.Calculate (project);
            var table = new ReportTable ("OTOPARK VE AĞAÇ HESABI (AÇIKLAMALI)", "Otopark ve Ağaç");
            table.Column ("Blok", 9).Column ("BB No", 9).Column ("Brüt Alan (m²)", 15, true)
                 .Column ("Uygulanan Aralık", 30).Column ("Otopark Payı", 13, true);

            table.AddSection ("AĞAÇ HESABI");
            table.AddLabelValue (RowKind.Data, "Parsel Alanı (m²)", ReportCell.OfNumber (project.Parcel.ParcelArea));
            table.AddLabelValue (RowKind.Data, "Yapı Oturum Alanı (m²)", ReportCell.OfNumber (project.Parcel.BuildingFootprint));
            table.AddLabelValue (RowKind.Data, "Bahçe Alanı (Parsel − Oturum) (m²)", ReportCell.OfNumber (summary.GardenArea));
            table.AddLabelValue (RowKind.Data, "Hesap Yöntemi",
                ReportCell.OfText ("Bahçe Alanı ÷ 30 m², sonuç yukarı yuvarlanır"));
            table.AddLabelValue (RowKind.Data, "İşlem",
                ReportCell.OfText (TextUtil.FormatArea (summary.GardenArea) + " ÷ 30 = " +
                    TextUtil.FormatArea (project.Parcel.ParcelArea > 0.0 ? summary.GardenArea / 30.0 : 0.0, 3)));
            table.AddLabelValue (RowKind.GrandTotal, "Gerekli Ağaç Sayısı", ReportCell.OfInteger (summary.RequiredTrees));

            table.AddSection ("OTOPARK ARALIKLARI (Yönetmelik Esası)");
            AddParkingBracketRow (table, "0 – 80 m² (80 dahil değil)", "1/3 araç");
            AddParkingBracketRow (table, "80 – 120 m² (120 dahil değil)", "1/2 araç");
            AddParkingBracketRow (table, "120 – 180 m² (180 dahil değil)", "1 araç");
            AddParkingBracketRow (table, "180 m² ve üzeri", "2 araç");

            table.AddSection ("BAĞIMSIZ BÖLÜM BAZINDA OTOPARK HESABI");
            foreach (BlockRecord block in project.Blocks) {
                foreach (IndependentUnit unit in block.Units) {
                    table.AddRow (RowKind.Data,
                        ReportCell.OfText (block.Name),
                        ReportCell.OfText (unit.Number),
                        ReportCell.OfNumber (unit.GrossArea),
                        ReportCell.OfText (ParkingBracketLabel (unit.GrossArea)),
                        ReportCell.OfNumber (CalculationEngine.ParkingContribution (unit.GrossArea), 3));
                }
            }
            if (summary.UnitCount == 0)
                table.AddRow (RowKind.Data, ReportCell.OfText (
                    "Bağımsız bölüm verisi yok — otopark payı hesaplanamıyor. TIP=NET / TIP=BRUT etiketleyip RHTARA çalıştırın.", 5));

            table.AddLabelValue (RowKind.GrandTotal, "Toplam Otopark Payı (ondalık)",
                ReportCell.OfNumber (summary.RawParkingSpaces, 3));
            table.AddLabelValue (RowKind.GrandTotal, "Yukarı Yuvarlanmış (Gerekli Otopark)",
                ReportCell.OfInteger (summary.RequiredParkingSpaces));
            table.AddLabelValue (RowKind.Data, "Projede Ayrılan Otopark",
                ReportCell.OfInteger (project.ProvidedParkingSpaces));
            table.AddLabelValue (RowKind.GrandTotal, "Otopark Kontrolü",
                ReportCell.OfText (summary.ParkingOk (project.ProvidedParkingSpaces)
                    ? "SAĞLANDI"
                    : "EKSİK: " + (summary.RequiredParkingSpaces - project.ProvidedParkingSpaces) + " araç"));
            return table;
        }

        private static void AddParkingBracketRow (ReportTable table, string range, string share)
        {
            table.AddRow (RowKind.Data, ReportCell.Empty, ReportCell.Empty, ReportCell.Empty,
                ReportCell.OfText (range), ReportCell.OfText (share));
        }

        /// <summary>Which yönetmelik aralığı a unit's brüt alanı falls into,
        /// shown next to its otopark payı so the number is never bare.</summary>
        private static string ParkingBracketLabel (double grossArea)
        {
            if (grossArea <= 0.0) return "—";
            if (grossArea < 80.0) return "0–80 m² → 1/3 araç";
            if (grossArea < 120.0) return "80–120 m² → 1/2 araç";
            if (grossArea < 180.0) return "120–180 m² → 1 araç";
            return "180+ m² → 2 araç";
        }

        /// <summary>
        /// A table with no data row is the most common surprise: the drawing
        /// simply has no etiket feeding it. Say so inside the table instead of
        /// drawing an empty grid.
        /// </summary>
        private static bool AddEmptyNotice (ReportTable table, string message)
        {
            foreach (ReportRow row in table.Rows)
                if (row.Kind == RowKind.Data) return false;
            table.AddSection (message);
            return true;
        }

        private static void AddText (ReportTable table, string label, string value)
        {
            table.AddRow (RowKind.Data, ReportCell.OfText (label), ReportCell.OfText (value ?? string.Empty));
        }

        private static string Join (string left, string right)
        {
            left = (left ?? string.Empty).Trim ();
            right = (right ?? string.Empty).Trim ();
            if (left.Length == 0) return right;
            if (right.Length == 0) return left;
            return left + " / " + right;
        }

        /// <summary>Every project table, in the order the RHTABLOLAR command draws them.</summary>
        public static IReadOnlyList<ReportTable> AllProjectTables (ProjectData project)
        {
            var tables = new List<ReportTable> {
                Summary (project),
                Emsal (project),
                Units (project),
                Construction (project),
                ConstructionByFloor (project),
                Condominium (project)
            };
            if (project.RetainingWalls.Count > 0) tables.Add (RetainingWalls (project));
            if (project.ExtraStructures.Count > 0) tables.Add (ExtraStructures (project));
            tables.Add (ParkingAndTrees (project));
            return tables;
        }
    }
}
