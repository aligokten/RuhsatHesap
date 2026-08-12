using System.Collections.Generic;
using System.Linq;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Reporting;
using RuhsatHesap.Core.Tagging;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    /// <summary>
    /// The scan report and the empty-table notices are what a user reads when a
    /// table comes out blank, so they are covered like any other output.
    /// </summary>
    public class ScanReportTests
    {
        private static AreaObservation Observation (string tag, double area, string floor)
        {
            return new AreaObservation { TagText = tag, Area = area, FloorName = floor, Handle = "1" };
        }

        [Fact]
        public void ScanReportsAreaPerType ()
        {
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 37.52, "Zemin"),
                Observation ("RH|BLOK=A|KAT=1|TIP=emsal", 47.54, "1"),
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=MERDIVEN|HESAP=EMSAL", 5.76, "Zemin"),
                Observation ("RH|BLOK=A|KAT=1|TIP=MERDIVEN|HESAP=EMSAL", 5.49, "1")
            });

            TypeTally emsal = result.Tallies.Single (tally => tally.Type == "EMSAL");
            Assert.Equal (2, emsal.Count);
            Assert.Equal (85.06, emsal.Area, 2);

            TypeTally stair = result.Tallies.Single (tally => tally.Type == "MERDIVEN");
            Assert.Equal (2, stair.Count);
            Assert.Equal (11.25, stair.Area, 2);
        }

        [Fact]
        public void EmsalAreasReachTheEmsalColumn ()
        {
            // Aynı senaryonun tablo karşılığı: TIP=EMSAL "Emsal Alan" sütununa,
            // HESAP=EMSAL merdiven ise %30 sütununa yazılmalı.
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 37.52, "Zemin"),
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=MERDIVEN|HESAP=EMSAL", 5.76, "Zemin")
            });

            ReportTable table = ReportBuilder.Emsal (project);
            int emsalColumn = table.Columns.FindIndex (column => column.Header == "Emsal Alan");
            int stairColumn = table.Columns.FindIndex (column => column.Header == "Merdiven");
            int totalColumn = table.Columns.FindIndex (column => column.Header == "Toplam İnşaat");

            ReportRow floorRow = table.Rows.First (row => row.Kind == RowKind.Data);
            Assert.Equal (37.52, ReportTable.CellAt (floorRow, emsalColumn).Value.Value, 2);
            Assert.Equal (5.76, ReportTable.CellAt (floorRow, stairColumn).Value.Value, 2);
            Assert.Equal (43.28, ReportTable.CellAt (floorRow, totalColumn).Value.Value, 2);
        }

        [Fact]
        public void EmptyUnitTableExplainsWhy ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 37.52, "Zemin")
            });

            ReportTable units = ReportBuilder.Units (project);
            Assert.DoesNotContain (units.Rows, row => row.Kind == RowKind.Data);
            ReportRow notice = Assert.Single (units.Rows, row => row.Kind == RowKind.Section);
            Assert.Contains ("TIP=NET", notice.Cells[0].Text);

            ReportTable condominium = ReportBuilder.Condominium (project);
            Assert.Contains (condominium.Rows, row => row.Kind == RowKind.Section);
        }

        [Fact]
        public void FilledUnitTableHasNoNotice ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|BB=01|KAT=Zemin|TIP=NET", 75.0, "Zemin"),
                Observation ("RH|BLOK=A|BB=01|KAT=Zemin|TIP=BRUT", 92.4, "Zemin")
            });

            ReportTable units = ReportBuilder.Units (project);
            Assert.Contains (units.Rows, row => row.Kind == RowKind.Data);
            Assert.DoesNotContain (units.Rows, row => row.Kind == RowKind.Section);
            Assert.Contains (units.Rows, row => row.Kind == RowKind.GrandTotal);
        }
    }
}
