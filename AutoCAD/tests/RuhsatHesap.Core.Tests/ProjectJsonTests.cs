using System.Linq;
using RuhsatHesap.Core;
using RuhsatHesap.Core.Json;
using RuhsatHesap.Core.Model;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    public class ProjectJsonTests
    {
        [Fact]
        public void RoundTripsEveryField ()
        {
            var project = new ProjectData ();
            project.Parcel.ProjectName = "Çınar Apartmanı";
            project.Parcel.City = "İstanbul";
            project.Parcel.Block = "1234";
            project.Parcel.Parcel = "7";
            project.Parcel.ParcelArea = 812.45;
            project.Parcel.TaksRate = 0.4;
            project.Parcel.KaksRate = 1.75;
            project.ProvidedParkingSpaces = 6;
            project.SetAuxNumber ("commonArea", 48.5);

            var block = new BlockRecord { Name = "A", ZeroLevel = "±0.00", SubbasementLevel = "-3.20" };
            var floor = new FloorRecord { Name = "ZEMİN KAT", EmsalArea = 320.5, EmsalOutsideArea = 18.0, SortIndex = 0 };
            floor.ConstructionAreas["merdiven"] = 14.25;
            floor.ThirtyPercentAreas["kat_holu"] = 9.75;
            floor.CadConstructionAreas["merdiven"] = 14.25;
            block.Floors.Add (floor);
            block.Units.Add (new IndependentUnit {
                Number = "01", Floor = "ZEMİN KAT", Quality = "Mesken", RoomCount = 3,
                GrossArea = 92.4, NetArea = 75.0, BalconyArea = 8.2, CadLinked = true, CadEntityCount = 4
            });
            block.Units[0].CadHandles.Add ("1F2");
            project.Blocks.Add (block);
            project.RetainingWalls.Add (new RetainingWall { Name = "Doğu", Area = 42.0 });

            ProjectData restored = ProjectJson.Deserialize (ProjectJson.Serialize (project));

            Assert.Equal ("Çınar Apartmanı", restored.Parcel.ProjectName);
            Assert.Equal ("İstanbul", restored.Parcel.City);
            Assert.Equal (812.45, restored.Parcel.ParcelArea, 2);
            Assert.Equal (1.75, restored.Parcel.KaksRate, 4);
            Assert.Equal (6, restored.ProvidedParkingSpaces);
            Assert.Equal (48.5, restored.CommonArea, 2);

            BlockRecord restoredBlock = Assert.Single (restored.Blocks);
            Assert.Equal ("±0.00", restoredBlock.ZeroLevel);
            FloorRecord restoredFloor = Assert.Single (restoredBlock.Floors);
            Assert.Equal (320.5, restoredFloor.EmsalArea, 2);
            Assert.Equal (14.25, restoredFloor.ConstructionAreas["merdiven"], 2);
            Assert.Equal (9.75, restoredFloor.ThirtyPercentAreas["kat_holu"], 2);
            Assert.Equal (14.25, restoredFloor.CadConstructionAreas["merdiven"], 2);

            IndependentUnit restoredUnit = Assert.Single (restoredBlock.Units);
            Assert.Equal ("01", restoredUnit.Number);
            Assert.Equal (92.4, restoredUnit.GrossArea, 2);
            Assert.True (restoredUnit.CadLinked);
            Assert.Equal (new[] { "1F2" }, restoredUnit.CadHandles.ToArray ());
            Assert.Equal (42.0, Assert.Single (restored.RetainingWalls).Area, 2);
        }

        [Fact]
        public void WritesTheArchicadCompatibleFormatMarker ()
        {
            JsonValue root = JsonValue.Parse (ProjectJson.Serialize (new ProjectData ()));
            Assert.Equal ("ruhsat-hesap-archicad", root["format"].AsString ());
            Assert.Equal (5, root["schemaVersion"].AsInt ());
        }

        [Fact]
        public void ReadsTheWebPanelEnvelope ()
        {
            const string envelope = @"{
              ""app"": ""Ruhsat Hesap"",
              ""project"": {
                ""data"": {
                  ""projectName"": ""Web Projesi"",
                  ""parcelArea"": ""1.250,75"",
                  ""taksRate"": 0.4,
                  ""kaksRate"": ""1,50"",
                  ""emsalMethod"": ""kaks"",
                  ""providedParkingSpaces"": 4,
                  ""auxiliaryData"": { ""commonArea"": 36 }
                },
                ""blocks"": [{
                  ""name"": ""A"",
                  ""floors"": [{ ""name"": ""ZEMİN KAT"", ""emsal"": 300, ""emsalDisi"": 12,
                                 ""exempt30"": { ""merdiven"": 14 },
                                 ""constructionValues"": { ""siginak"": 25 } }],
                  ""units"": [{ ""no"": ""3"", ""floor"": ""ZEMİN KAT"", ""grossArea"": 92.4, ""netArea"": 75 },
                              { ""no"": ""10"", ""floor"": ""ZEMİN KAT"", ""grossArea"": 88 },
                              { ""no"": ""7"", ""floor"": ""ZEMİN KAT"", ""grossArea"": 70 }]
                }],
                ""retainingWalls"": [{ ""name"": ""Kuzey"", ""area"": 18.5 }]
              }
            }";

            ProjectData project = ProjectJson.Deserialize (envelope);

            Assert.Equal ("Web Projesi", project.Parcel.ProjectName);
            Assert.Equal (1250.75, project.Parcel.ParcelArea, 2);   // Türkçe biçimli metin
            Assert.Equal (1.50, project.Parcel.KaksRate, 4);
            Assert.Equal (4, project.ProvidedParkingSpaces);
            Assert.Equal (36.0, project.CommonArea, 2);

            FloorRecord floor = Assert.Single (project.Blocks[0].Floors);
            Assert.Equal (300.0, floor.EmsalArea, 2);
            Assert.Equal (12.0, floor.EmsalOutsideArea, 2);
            Assert.Equal (14.0, floor.ThirtyPercentAreas["merdiven"], 2);
            Assert.Equal (25.0, floor.ConstructionAreas["siginak"], 2);

            // Bağımsız bölümler doğal sırada okunur.
            Assert.Equal (new[] { "3", "7", "10" }, project.Blocks[0].Units.Select (unit => unit.Number).ToArray ());
            Assert.Equal (18.5, Assert.Single (project.RetainingWalls).Area, 2);
        }

        [Theory]
        [InlineData ("1.234,56", 1234.56)]
        [InlineData ("12,5", 12.5)]
        [InlineData ("12.5", 12.5)]
        [InlineData ("1250", 1250.0)]
        [InlineData ("", 0.0)]
        [InlineData ("abc", 0.0)]
        public void NumbersAreParsedInBothNotations (string text, double expected)
        {
            Assert.Equal (expected, TextUtil.ParseNumberLoose (text), 4);
        }

        [Theory]
        [InlineData ("1.KAT", "1. Kat")]
        [InlineData ("1.KAT", "1.  KAT")]
        [InlineData ("1.KAT", "1 . KAT")]
        [InlineData ("1.kat", "1.KAT")]
        [InlineData ("ZEMİN KAT", "zemin kat")]
        [InlineData ("ZEMİN KAT", "Zemin   Kat")]
        public void FloorNameSpellingVariantsShareTheSameKey (string left, string right)
        {
            // Bu tam olarak raporlanan hatanın sebebiydi: BRÜT etiketleri
            // "1.KAT" olarak, MERDIVEN etiketi "1. Kat" olarak yazılmış ve iki
            // ayrı satıra bölünmüştü.
            Assert.Equal (TextUtil.NormalizeFloorKey (left), TextUtil.NormalizeFloorKey (right));
        }

        [Fact]
        public void DifferentFloorsKeepDifferentKeys ()
        {
            Assert.NotEqual (TextUtil.NormalizeFloorKey ("1. KAT"), TextUtil.NormalizeFloorKey ("2. KAT"));
            Assert.NotEqual (TextUtil.NormalizeFloorKey ("ZEMİN KAT"), TextUtil.NormalizeFloorKey ("1. KAT"));
        }

        [Fact]
        public void ExtraStructuresRoundTripThroughJson ()
        {
            var project = new ProjectData ();
            project.ExtraStructures.Add (new ExtraStructure { Name = "Foseptik", Area = 8.5 });

            ProjectData restored = ProjectJson.Deserialize (ProjectJson.Serialize (project));

            ExtraStructure structure = Assert.Single (restored.ExtraStructures);
            Assert.Equal ("Foseptik", structure.Name);
            Assert.Equal (8.5, structure.Area, 2);
        }

        [Theory]
        [InlineData ("1", "2")]
        [InlineData ("2", "10")]
        [InlineData ("9", "10")]
        [InlineData ("A2", "A10")]
        [InlineData ("01", "2")]
        public void UnitNumbersSortNaturally (string first, string second)
        {
            Assert.True (TextUtil.CompareUnitNumbers (first, second) < 0, first + " < " + second + " bekleniyordu");
        }

        [Fact]
        public void JsonSurvivesSpecialCharacters ()
        {
            JsonValue root = JsonValue.NewObject ();
            root["metin"] = JsonValue.String ("Satır1\nSatır2 \"tırnak\" \\ ters");
            root["sayı"] = JsonValue.Number (12.345);
            JsonValue restored = JsonValue.Parse (root.ToJson (true));

            Assert.Equal ("Satır1\nSatır2 \"tırnak\" \\ ters", restored["metin"].AsString ());
            Assert.Equal (12.345, restored["sayı"].AsDouble (), 4);
        }
    }
}
