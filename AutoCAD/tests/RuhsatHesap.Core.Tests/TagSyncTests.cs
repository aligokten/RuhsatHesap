using System.Collections.Generic;
using System.Linq;
using RuhsatHesap.Core;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Reporting;
using RuhsatHesap.Core.Tagging;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    public class TagSyncTests
    {
        private static AreaObservation Observation (string tag, double area, string floor = "", string handle = "1A")
        {
            return new AreaObservation { TagText = tag, Area = area, FloorName = floor, Handle = handle };
        }

        private static List<AreaObservation> SampleFlat ()
        {
            return new List<AreaObservation> {
                Observation ("RH|BLOK=A|BB=01|TIP=NET|ODA=3|NITELIK=Mesken", 24.5, "ZEMİN KAT", "100"),
                Observation ("RH|BLOK=A|BB=01|TIP=NET", 50.5, "ZEMİN KAT", "101"),
                Observation ("RH|BLOK=A|BB=01|TIP=BRUT", 92.4, "ZEMİN KAT", "102"),
                Observation ("RH|BLOK=A|BB=01|TIP=BALKON", 8.2, "ZEMİN KAT", "103"),
                Observation ("RH|BLOK=A|BB=02|TIP=NET", 70.0, "ZEMİN KAT", "104"),
                Observation ("RH|BLOK=A|BB=02|TIP=BRUT", 88.0, "ZEMİN KAT", "105"),
                Observation ("RH|BLOK=A|TIP=MERDIVEN", 14.0, "ZEMİN KAT", "106"),
                Observation ("RH|BLOK=A|TIP=EMSAL", 190.4, "ZEMİN KAT", "107"),
                Observation ("RH|BLOK=A|TIP=EMSAL_DISI", 12.0, "ZEMİN KAT", "108"),
                Observation ("RH|TIP=PARSEL", 500.0, "", "109"),
                Observation ("RH|TIP=OTURUM", 190.0, "", "110"),
                Observation ("RH|TIP=ORTAK", 30.0, "", "111")
            };
        }

        [Fact]
        public void UnitAreasAreAggregatedPerIndependentUnit ()
        {
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, SampleFlat ());

            BlockRecord block = Assert.Single (project.Blocks);
            Assert.Equal ("A", block.Name);
            Assert.Equal (2, block.Units.Count);
            Assert.Equal (0, result.Invalid);

            IndependentUnit first = block.Units.First ();
            Assert.Equal ("01", first.Number);
            Assert.Equal (75.0, first.NetArea, 2);      // 24,50 + 50,50
            Assert.Equal (92.4, first.GrossArea, 2);
            Assert.Equal (8.2, first.BalconyArea, 2);
            Assert.Equal (3, first.RoomCount);
            Assert.Equal ("Mesken", first.Quality);
            Assert.Equal ("ZEMİN KAT", first.Floor);
            Assert.True (first.CadLinked);
            Assert.Equal (4, first.CadEntityCount);
        }

        [Fact]
        public void FloorLevelAreasFeedTheirTables ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, SampleFlat ());

            FloorRecord floor = project.Blocks[0].Floors.Single (item => item.Name == "ZEMİN KAT");
            Assert.Equal (14.0, floor.ConstructionAreas["merdiven"], 2);
            Assert.Equal (190.4, floor.EmsalArea, 2);
            Assert.Equal (12.0, floor.EmsalOutsideArea, 2);
            Assert.Empty (floor.ThirtyPercentAreas);
        }

        [Fact]
        public void HesapEmsalMovesTheAreaToTheThirtyPercentTable ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|TIP=MERDIVEN|HESAP=EMSAL", 14.0, "1. KAT"),
                Observation ("RH|BLOK=A|TIP=ASANSOR", 4.0, "1. KAT")
            });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (14.0, floor.ThirtyPercentAreas["merdiven"], 2);
            Assert.False (floor.ConstructionAreas.ContainsKey ("merdiven"));
            Assert.Equal (4.0, floor.ConstructionAreas["asansor"], 2);
        }

        [Fact]
        public void ParcelFootprintCommonAndShelterAreRead ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, SampleFlat ().Concat (new[] {
                Observation ("RH|BLOK=A|TIP=SIGINAK", 25.0, "1. BODRUM", "112"),
                Observation ("RH|TIP=ISTINAT|AD=Doğu İstinat", 42.0, "", "113")
            }).ToList ());

            Assert.Equal (500.0, project.Parcel.ParcelArea, 2);
            Assert.Equal (190.0, project.Parcel.BuildingFootprint, 2);
            Assert.Equal (30.0, project.CommonArea, 2);
            Assert.Equal (25.0, project.AuxiliaryData["shelter"]["providedArea"].AsDouble (), 2);
            RetainingWall wall = Assert.Single (project.RetainingWalls);
            Assert.Equal ("Doğu İstinat", wall.Name);
            Assert.Equal (42.0, wall.Area, 2);
        }

        [Fact]
        public void RepeatedScanIsIdempotent ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, SampleFlat ());
            string first = ProjectJson.Serialize (project, false);

            TagSync.Sync (project, SampleFlat ());
            string second = ProjectJson.Serialize (project, false);

            Assert.Equal (first, second);
            Assert.Equal (75.0, project.Blocks[0].Units[0].NetArea, 2);
            Assert.Equal (30.0, project.CommonArea, 2);
            Assert.Empty (project.RetainingWalls);
        }

        [Fact]
        public void ManuallyTypedValuesSurviveAScan ()
        {
            var project = new ProjectData ();
            var block = new BlockRecord { Name = "A" };
            block.Units.Add (new IndependentUnit { Number = "09", NetArea = 60.0, GrossArea = 70.0, Quality = "Dükkan" });
            var floor = new FloorRecord { Name = "ZEMİN KAT" };
            floor.ConstructionAreas["su_deposu"] = 12.0;
            block.Floors.Add (floor);
            project.Blocks.Add (block);

            TagSync.Sync (project, SampleFlat ());

            IndependentUnit manual = project.Blocks[0].Units.Single (unit => unit.Number == "09");
            Assert.Equal (60.0, manual.NetArea, 2);
            Assert.Equal ("Dükkan", manual.Quality);
            Assert.Equal (12.0, project.Blocks[0].Floors.Single (item => item.Name == "ZEMİN KAT").ConstructionAreas["su_deposu"], 2);
        }

        [Fact]
        public void FreeTypeCreatesItsOwnColumn ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|TIP=Havuz Kenarı", 18.0, "ÇATI KATI")
            });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (18.0, floor.ConstructionAreas["havuz_kenarı"], 2);
            Assert.Contains ("havuz_kenarı", project.AreaColumnKeys (false));
        }

        [Fact]
        public void FreeTypeMatchesAnExistingColumnDespiteSpelling ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|TIP=YANGIN_MERDIVENI", 10.0, "1. KAT")
            });
            // "Yangın Merdiveni" folds onto the key created above instead of
            // opening a second, near-identical column.
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|TIP=Yangın Merdiveni", 12.0, "1. KAT")
            });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Single (floor.ConstructionAreas);
            Assert.Equal (12.0, floor.ConstructionAreas["yangin_merdiveni"], 2);
        }

        [Fact]
        public void DuplexUnitKeepsItsLowerFloor ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|BB=05|TIP=NET", 60.0, "3. KAT", "200"),
                Observation ("RH|BLOK=A|BB=05|TIP=NET", 40.0, "4. KAT", "201"),
                Observation ("RH|BLOK=A|BB=05|TIP=BALKON", 6.0, "4. KAT", "202")
            });

            IndependentUnit unit = project.Blocks[0].Units.Single ();
            Assert.Equal ("3. KAT", unit.Floor);
            Assert.Equal (100.0, unit.NetArea, 2);
        }

        [Fact]
        public void InvalidTagsAreCountedAndReported ()
        {
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BB=01|TIP=NET", 20.0, "ZEMİN KAT", "300"),          // BLOK eksik
                Observation ("RH|BLOK=A|TIP=NET", 20.0, "ZEMİN KAT", "301"),         // BB eksik
                Observation ("RH|BLOK=A|BB=1|TIP=NET", 20.0, "", "302"),             // kat eksik
                Observation ("RH|BLOK=A|BB=1|TIP=NET", 0.0, "ZEMİN KAT", "303"),     // alan yok
                Observation ("SALON", 20.0, "ZEMİN KAT", "304")                      // etiketsiz
            });

            Assert.Equal (4, result.Invalid);
            Assert.Equal (1, result.Ignored);
            Assert.Equal (0, result.Recognized);
            Assert.NotEmpty (result.Problems);
            Assert.Empty (project.Blocks);
        }

        [Fact]
        public void UnitsAreSortedNaturally ()
        {
            var project = new ProjectData ();
            var observations = new List<AreaObservation> ();
            foreach (string number in new[] { "10", "7", "9", "8" })
                observations.Add (Observation ("RH|BLOK=A|BB=" + number + "|TIP=NET", 50.0, "1. KAT", number));
            TagSync.Sync (project, observations);

            Assert.Equal (new[] { "7", "8", "9", "10" }, project.Blocks[0].Units.Select (unit => unit.Number).ToArray ());
        }

        [Fact]
        public void FloorsAreOrderedFromBasementUp ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|TIP=EMSAL", 100.0, "1. KAT", "400"),
                Observation ("RH|BLOK=A|TIP=EMSAL", 120.0, "ZEMİN KAT", "401"),
                Observation ("RH|BLOK=A|TIP=EMSAL", 90.0, "1. BODRUM", "402")
            });

            Assert.Equal (new[] { "1. BODRUM", "ZEMİN KAT", "1. KAT" },
                project.Blocks[0].Floors.Select (floor => floor.Name).ToArray ());
        }

        [Fact]
        public void DifferentlyCasedFloorNamesMergeIntoOneRow ()
        {
            // Reproduces the reported bug: BRUT etiketleri "1.KAT" olarak,
            // MERDIVEN etiketi "1. Kat" olarak yazılmıştı -- ikisi de aynı katı
            // ifade ediyor ve tek satırda toplanmalı.
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|BB=01|KAT=1.KAT|TIP=NET", 60.0, ""),
                Observation ("RH|BLOK=A|BB=01|KAT=1. Kat|TIP=BRUT", 75.0, ""),
                Observation ("RH|BLOK=A|KAT=1.  KAT|TIP=MERDIVEN", 12.0, ""),
                Observation ("RH|BLOK=A|KAT=1.kat|TIP=EMSAL", 200.0, "")
            });

            FloorRecord floor = Assert.Single (project.Blocks[0].Floors);
            Assert.Equal (12.0, floor.ConstructionAreas["merdiven"], 2);
            Assert.Equal (200.0, floor.EmsalArea, 2);
            Assert.Equal (75.0, project.Blocks[0].UnitGrossOnFloor (floor.Name), 2);

            // Yapı İnşaat Alanı satırı da aynı katı gösterip BB brütünü içerir.
            ReportTable construction = ReportBuilder.Construction (project);
            ReportRow dataRow = Assert.Single (construction.Rows, row => row.Kind == RowKind.Data);
            int grossColumn = construction.Columns.FindIndex (column => column.Header == "BB Brüt Alanı");
            Assert.Equal (75.0, ReportTable.CellAt (dataRow, grossColumn).Value.Value, 2);

            Assert.Contains (result.Problems, message => message.Contains ("Aynı kat farklı yazılmış"));
        }

        [Fact]
        public void ExtraStructureNeedsNoBlockOrFloorAndFeedsConstructionArea ()
        {
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|TIP=EK_YAPI|AD=Foseptik", 8.5, ""),
                Observation ("RH|TIP=EK_YAPI|AD=Trafo Binası", 6.0, "")
            });

            Assert.Equal (2, result.ExtraStructures);
            Assert.Equal (2, project.ExtraStructures.Count);
            Assert.Equal ("Foseptik", project.ExtraStructures[0].Name);
            Assert.Equal (8.5, project.ExtraStructures[0].Area, 2);

            CalculationSummary summary = CalculationEngine.Calculate (project);
            Assert.Equal (14.5, summary.ExtraStructureArea, 2);
            Assert.Equal (14.5, summary.ConstructionGrandTotal, 2);
        }

        [Fact]
        public void RepeatedScanReplacesOnlyItsOwnExtraStructures ()
        {
            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|TIP=EK_YAPI|AD=Foseptik", 8.5, "")
            });
            project.ExtraStructures.Add (new ExtraStructure { Name = "Elle Eklenen Su Deposu", Area = 5.0 });

            TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|TIP=EK_YAPI|AD=Foseptik", 9.0, "")
            });

            Assert.Equal (2, project.ExtraStructures.Count);
            Assert.Equal (9.0, project.ExtraStructures.Single (item => item.Name == "Foseptik").Area, 2);
            Assert.Equal (5.0, project.ExtraStructures.Single (item => item.Name == "Elle Eklenen Su Deposu").Area, 2);
        }

        [Fact]
        public void NearIdenticalAreasInTheSameBucketWarnAboutDoubleTagging ()
        {
            // Bir HATCH ve onu çevreleyen sınır polyline'ının ikisi de aynı
            // kaleme (BLOK+KAT+TIP) etiketlenmişse, alan neredeyse 2 katına
            // çıkar -- bu, kullanıcının bildirdiği "2x katı" hatasının en
            // olası nedenidir.
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 46.40, "Zemin", "200"),
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 46.41, "Zemin", "201")
            });

            Assert.Contains (result.Problems, message =>
                message.Contains ("<200>") && message.Contains ("<201>") &&
                message.Contains ("aynı alana sahip"));

            // Her ikisi de aynı katman toplamına eklendiği için değer de
            // gerçekte tek bir alanın iki katı olur -- bu, uyarının işaret
            // ettiği asıl hatalı sonuçtur.
            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (92.81, floor.EmsalArea, 2);
        }

        [Fact]
        public void GenuinelyDifferentAreasInTheSameBucketDoNotWarn ()
        {
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 46.40, "Zemin", "200"),
                Observation ("RH|BLOK=A|KAT=1|TIP=EMSAL", 51.20, "1", "201")
            });

            Assert.DoesNotContain (result.Problems, message => message.Contains ("aynı alana sahip"));
        }

        [Fact]
        public void DuplicateUnitAreasAlsoWarn ()
        {
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|BB=01|TIP=NET", 75.00, "Zemin", "300"),
                Observation ("RH|BLOK=A|BB=01|TIP=NET", 75.01, "Zemin", "301")
            });

            Assert.Contains (result.Problems, message =>
                message.Contains ("<300>") && message.Contains ("<301>") &&
                message.Contains ("aynı alana sahip"));
        }
    }
}
