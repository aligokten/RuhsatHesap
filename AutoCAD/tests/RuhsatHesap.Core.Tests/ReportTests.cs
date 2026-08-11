using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using RuhsatHesap.Core;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Reporting;
using RuhsatHesap.Core.Tagging;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    public class ReportTests
    {
        private static ProjectData Sample ()
        {
            var project = new ProjectData ();
            var observations = new List<AreaObservation> {
                New ("RH|BLOK=A|BB=01|TIP=NET|ODA=3|NITELIK=Mesken", 75.0, "ZEMİN KAT"),
                New ("RH|BLOK=A|BB=01|TIP=BRUT", 92.4, "ZEMİN KAT"),
                New ("RH|BLOK=A|BB=02|TIP=NET", 88.0, "ZEMİN KAT"),
                New ("RH|BLOK=A|BB=02|TIP=BRUT", 105.0, "ZEMİN KAT"),
                New ("RH|BLOK=A|TIP=MERDIVEN", 14.0, "ZEMİN KAT"),
                New ("RH|BLOK=A|TIP=MERDIVEN|HESAP=EMSAL", 12.0, "1. KAT"),
                New ("RH|BLOK=A|TIP=EMSAL", 197.4, "ZEMİN KAT"),
                New ("RH|BLOK=A|TIP=EMSAL", 197.4, "1. KAT"),
                New ("RH|BLOK=B|TIP=EMSAL", 150.0, "ZEMİN KAT"),
                New ("RH|TIP=PARSEL", 1000.0),
                New ("RH|TIP=ISTINAT|AD=Doğu", 24.0)
            };
            TagSync.Sync (project, observations);
            project.Parcel.TaksRate = 0.4;
            project.Parcel.KaksRate = 1.5;
            project.Parcel.BuildingFootprint = 320.0;
            return project;
        }

        private static AreaObservation New (string tag, double area, string floor = "")
        {
            return new AreaObservation { TagText = tag, Area = area, FloorName = floor, Handle = Guid.NewGuid ().ToString ("N").Substring (0, 4) };
        }

        [Fact]
        public void AreaListTotalsTheSelection ()
        {
            var observations = new List<AreaObservation> {
                New ("RH|BLOK=A|BB=01|TIP=NET", 24.5, "ZEMİN KAT"),
                New ("RH|BLOK=A|BB=01|TIP=NET", 50.5, "ZEMİN KAT"),
                New ("PLAN CIZGISI", 10.0, "ZEMİN KAT")
            };

            ReportTable table = ReportBuilder.AreaList (observations);
            Assert.Equal (7, table.ColumnCount);
            Assert.Equal (4, table.Rows.Count);   // 3 satır + toplam

            ReportRow total = table.Rows.Last ();
            Assert.Equal (RowKind.GrandTotal, total.Kind);
            Assert.Equal (85.0, ReportTable.CellAt (total, 6).Value.Value, 2);

            // Etiketsiz nesne de listelenir, "ETİKETSİZ" tipiyle.
            Assert.Contains (table.Rows, row => ReportTable.CellAt (row, 4)?.Text == "ETİKETSİZ");
        }

        [Fact]
        public void AreaSummaryGroupsByBlockFloorAndType ()
        {
            var observations = new List<AreaObservation> {
                New ("RH|BLOK=A|BB=01|TIP=NET", 24.5, "ZEMİN KAT"),
                New ("RH|BLOK=A|BB=02|TIP=NET", 50.5, "ZEMİN KAT"),
                New ("RH|BLOK=A|BB=01|TIP=BALKON", 8.0, "ZEMİN KAT")
            };

            ReportTable table = ReportBuilder.AreaSummary (observations);
            List<ReportRow> dataRows = table.Rows.Where (row => row.Kind == RowKind.Data).ToList ();
            Assert.Equal (2, dataRows.Count);

            ReportRow netRow = dataRows.Single (row => ReportTable.CellAt (row, 2).Text == "NET");
            Assert.Equal (2.0, ReportTable.CellAt (netRow, 3).Value.Value, 2);
            Assert.Equal (75.0, ReportTable.CellAt (netRow, 4).Value.Value, 2);
        }

        [Fact]
        public void EmsalTableTotalsPerBlockAndOverall ()
        {
            ProjectData project = Sample ();
            ReportTable table = ReportBuilder.Emsal (project);

            List<ReportRow> blockTotals = table.Rows.Where (row => row.Kind == RowKind.BlockTotal).ToList ();
            Assert.Equal (2, blockTotals.Count);

            int emsalColumn = table.Columns.FindIndex (column => column.Header == "Emsal Alan");
            Assert.True (emsalColumn > 0);
            Assert.Equal (394.8, ReportTable.CellAt (blockTotals[0], emsalColumn).Value.Value, 2);
            Assert.Equal (150.0, ReportTable.CellAt (blockTotals[1], emsalColumn).Value.Value, 2);

            ReportRow grandTotal = table.Rows.First (row => row.Kind == RowKind.GrandTotal);
            Assert.Equal (544.8, ReportTable.CellAt (grandTotal, emsalColumn).Value.Value, 2);
        }

        [Fact]
        public void EmsalTableCarriesTheThirtyPercentColumns ()
        {
            ReportTable table = ReportBuilder.Emsal (Sample ());
            // Yalnız HESAP=EMSAL ile yönlendirilen merdiven %30 tablosuna girer.
            Assert.Contains (table.Columns, column => column.Header == "Merdiven");
            Assert.Contains (table.Columns, column => column.Header == "%30 Dahil Toplam");
        }

        [Fact]
        public void UnitTableSpreadsTheCommonArea ()
        {
            ProjectData project = Sample ();
            project.SetAuxNumber ("commonArea", 40.0);   // 2 bağımsız bölüm -> 20 m²

            ReportTable table = ReportBuilder.Units (project);
            ReportRow firstUnit = table.Rows.First (row => row.Kind == RowKind.Data);
            int generalGross = table.Columns.FindIndex (column => column.Header == "Genel Brüt");
            Assert.Equal (112.4, ReportTable.CellAt (firstUnit, generalGross).Value.Value, 2);
        }

        [Fact]
        public void ConstructionTableAddsUnitGrossToFloorItems ()
        {
            ReportTable table = ReportBuilder.Construction (Sample ());
            ReportRow groundFloor = table.Rows.First (row => row.Kind == RowKind.Data &&
                ReportTable.CellAt (row, 1).Text == "ZEMİN KAT");

            int totalColumn = table.ColumnCount - 1;
            // 92,40 + 105,00 bağımsız bölüm brütü + 14,00 merdiven
            Assert.Equal (211.4, ReportTable.CellAt (groundFloor, totalColumn).Value.Value, 2);
        }

        [Fact]
        public void SummaryTableReportsTheEmsalCheck ()
        {
            ReportTable table = ReportBuilder.Summary (Sample ());
            string emsalCheck = table.Rows
                .Where (row => ReportTable.CellAt (row, 0)?.Text == "Emsal Kontrolü")
                .Select (row => ReportTable.CellAt (row, 1).Text)
                .Single ();

            // 1000 × 1,5 = 1500 m² hak, 544,80 m² hesaplanan
            Assert.StartsWith ("UYGUN", emsalCheck);
            Assert.Contains ("955,20", emsalCheck);
        }

        [Fact]
        public void AllProjectTablesAreProduced ()
        {
            IReadOnlyList<ReportTable> tables = ReportBuilder.AllProjectTables (Sample ());
            Assert.Equal (7, tables.Count);   // istinat duvarı da var
            Assert.All (tables, table => Assert.NotEmpty (table.Columns));
            Assert.All (tables, table => Assert.NotEmpty (table.Rows));
        }

        [Fact]
        public void ExcelWorkbookIsAValidPackage ()
        {
            string path = Path.Combine (Path.GetTempPath (), "ruhsat-" + Guid.NewGuid ().ToString ("N") + ".xlsx");
            try {
                IReadOnlyList<ReportTable> tables = ReportBuilder.AllProjectTables (Sample ());
                XlsxWriter.Write (path, tables);

                using (var archive = ZipFile.OpenRead (path)) {
                    Assert.NotNull (archive.GetEntry ("[Content_Types].xml"));
                    Assert.NotNull (archive.GetEntry ("_rels/.rels"));
                    Assert.NotNull (archive.GetEntry ("xl/workbook.xml"));
                    Assert.NotNull (archive.GetEntry ("xl/styles.xml"));
                    Assert.Equal (tables.Count, archive.Entries.Count (entry => entry.FullName.StartsWith ("xl/worksheets/")));

                    // Her parça geçerli XML olmalı.
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        using (Stream stream = entry.Open ())
                            XDocument.Load (stream);
                    }

                    using (Stream stream = archive.GetEntry ("xl/worksheets/sheet1.xml").Open ()) {
                        XDocument sheet = XDocument.Load (stream);
                        Assert.Contains (sheet.Descendants ().Where (element => element.Name.LocalName == "t"),
                            element => element.Value == "RUHSAT HESAP ÖZETİ");
                    }
                }
            } finally {
                if (File.Exists (path)) File.Delete (path);
            }
        }

        [Fact]
        public void SheetNamesStayWithinExcelLimits ()
        {
            var tables = new List<ReportTable> {
                new ReportTable ("Bir", "Çok Uzun Bir Sayfa Adı Otuz Bir Karakteri Aşıyor").Column ("A", 10),
                new ReportTable ("İki", "Çok Uzun Bir Sayfa Adı Otuz Bir Karakteri Aşıyor").Column ("A", 10),
                new ReportTable ("Üç", "Ge/çer[siz]*isim").Column ("A", 10)
            };
            foreach (ReportTable table in tables) table.AddRow (RowKind.Data, ReportCell.OfText ("x"));

            string path = Path.Combine (Path.GetTempPath (), "ruhsat-" + Guid.NewGuid ().ToString ("N") + ".xlsx");
            try {
                XlsxWriter.Write (path, tables);
                using (var archive = ZipFile.OpenRead (path))
                using (Stream stream = archive.GetEntry ("xl/workbook.xml").Open ()) {
                    XDocument workbook = XDocument.Load (stream);
                    List<string> names = workbook.Descendants ()
                        .Where (element => element.Name.LocalName == "sheet")
                        .Select (element => element.Attribute ("name").Value)
                        .ToList ();

                    Assert.Equal (3, names.Count);
                    Assert.Equal (names.Count, names.Distinct (StringComparer.OrdinalIgnoreCase).Count ());
                    Assert.All (names, name => Assert.True (name.Length <= 31));
                    Assert.All (names, name => Assert.DoesNotContain ('/', name));
                }
            } finally {
                if (File.Exists (path)) File.Delete (path);
            }
        }

        [Fact]
        public void CsvUsesSemicolonsAndTurkishDecimals ()
        {
            string csv = CsvWriter.Build (new[] { ReportBuilder.Summary (Sample ()) });
            Assert.Contains ("Hesap Kalemi;Değer", csv);
            Assert.Contains ("1.000,00", csv);   // parsel alanı
        }
    }
}
