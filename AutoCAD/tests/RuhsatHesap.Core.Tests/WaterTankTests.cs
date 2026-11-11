using System.Linq;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Reporting;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    /// <summary>
    /// Kullanma suyu, yangın suyu ve yağmur suyu deposu hesapları. The numbers
    /// are worked by hand in each test so a changed formula cannot pass by
    /// agreeing with itself.
    /// </summary>
    public class WaterTankTests
    {
        private static ProjectData ProjectWithUnits (int unitCount)
        {
            var project = new ProjectData ();
            var block = new BlockRecord { Name = "A" };
            for (int index = 1; index <= unitCount; index++)
                block.Units.Add (new IndependentUnit { Number = index.ToString (), GrossArea = 100.0 });
            project.Blocks.Add (block);
            return project;
        }

        [Fact]
        public void DomesticTankIsPersonsTimesDailyUseTimesDays ()
        {
            ProjectData project = ProjectWithUnits (6);
            var settings = new WaterTankSettings { PersonsPerUnit = 4, DailyLitresPerPerson = 150, StorageDays = 2 };

            WaterTankResult result = WaterTankCalculator.Calculate (project, settings);

            Assert.Equal (6, result.UnitCount);
            Assert.Equal (24.0, result.Persons, 2);            // 6 BB × 4 kişi
            Assert.False (result.PersonsAreManual);
            Assert.Equal (7.2, result.DomesticRequired, 2);    // 24 × 150 × 2 ÷ 1000
        }

        [Fact]
        public void ManualPersonCountOverridesTheUnitCount ()
        {
            ProjectData project = ProjectWithUnits (6);
            var settings = new WaterTankSettings { ManualPersons = 10, DailyLitresPerPerson = 200, StorageDays = 1 };

            WaterTankResult result = WaterTankCalculator.Calculate (project, settings);

            Assert.True (result.PersonsAreManual);
            Assert.Equal (10.0, result.Persons, 2);
            Assert.Equal (2.0, result.DomesticRequired, 2);    // 10 × 200 × 1 ÷ 1000
        }

        [Fact]
        public void FireTankSumsOnlyTheSystemsTheBuildingHas ()
        {
            var project = new ProjectData ();
            var settings = new WaterTankSettings {
                HazardClass = FireHazardClass.Low, HasCabinets = true, HasHydrants = false
            };
            settings.ApplyHazardDefaults ();

            WaterTankResult result = WaterTankCalculator.Calculate (project, settings);

            // Yalnız yangın dolabı: 100 L/dak × 30 dak = 3 m³; hidrant sayılmaz.
            Assert.Equal (0.0, result.HydrantFlow, 2);
            Assert.Equal (100.0, result.TotalFireFlow, 2);
            Assert.Equal (3.0, result.FireRequired, 2);
        }

        [Fact]
        public void FireTankAddsSprinklerCabinetAndHydrantFlows ()
        {
            var project = new ProjectData ();
            var settings = new WaterTankSettings {
                HazardClass = FireHazardClass.Ordinary34, HasCabinets = true, HasHydrants = true,
                SprinklerDensity = 5.0, SprinklerArea = 216.0
            };
            settings.ApplyHazardDefaults ();

            WaterTankResult result = WaterTankCalculator.Calculate (project, settings);

            Assert.Equal (1080.0, result.SprinklerFlow, 2);    // 5 mm/dak × 216 m²
            Assert.Equal (2180.0, result.TotalFireFlow, 2);    // 1080 + 100 + 1000
            Assert.Equal (130.8, result.FireRequired, 2);      // 2180 × 60 ÷ 1000
        }

        [Theory]
        [InlineData (FireHazardClass.Low, 100.0, 400.0, 30.0)]
        [InlineData (FireHazardClass.Ordinary12, 100.0, 400.0, 60.0)]
        [InlineData (FireHazardClass.Ordinary34, 100.0, 1000.0, 60.0)]
        [InlineData (FireHazardClass.High, 200.0, 1500.0, 90.0)]
        public void HazardClassDefaultsFollowTheRegulationTable (
            FireHazardClass hazardClass, double cabinet, double hydrant, double minutes)
        {
            var settings = new WaterTankSettings { HazardClass = hazardClass };
            settings.ApplyHazardDefaults ();

            Assert.Equal (cabinet, settings.CabinetFlow, 2);
            Assert.Equal (hydrant, settings.HydrantFlow, 2);
            Assert.Equal (minutes, settings.DurationMinutes, 2);
        }

        [Fact]
        public void RainTankIsSixPercentOfTheAnnualYield ()
        {
            // Bakanlık kılavuzundaki örnek: 1500 m² çatı izdüşümü, yıllık
            // toplanabilir su 423 792 L, depo en az 25,43 m³.
            var project = new ProjectData ();
            project.Parcel.ParcelArea = 3000.0;
            var settings = new WaterTankSettings {
                RoofArea = 1500.0, AnnualRainfall = 392.4, RoofCoefficient = 0.8, FilterEfficiency = 0.9
            };

            WaterTankResult result = WaterTankCalculator.Calculate (project, settings);

            Assert.Equal (423792.0, result.AnnualYieldLitres, 0);
            Assert.Equal (25.43, result.RainRequired, 2);
            Assert.True (result.RainMandatoryByParcel);
            Assert.False (result.RoofAreaFromFootprint);
        }

        [Fact]
        public void RainTankFallsBackToTheBuildingFootprint ()
        {
            var project = new ProjectData ();
            project.Parcel.ParcelArea = 310.45;
            project.Parcel.BuildingFootprint = 46.5;
            var settings = new WaterTankSettings { AnnualRainfall = 1000.0 };

            WaterTankResult result = WaterTankCalculator.Calculate (project, settings);

            Assert.True (result.RoofAreaFromFootprint);
            Assert.Equal (46.5, result.RoofArea, 2);
            Assert.Equal (2.01, result.RainRequired, 2);       // 46,5 × 1000 × 0,8 × 0,9 × 0,06 ÷ 1000
            Assert.False (result.RainMandatoryByParcel);       // 2000 m² altı
        }

        [Fact]
        public void SettingsRoundTripThroughTheProjectJson ()
        {
            var project = new ProjectData ();
            var settings = new WaterTankSettings {
                PersonsPerUnit = 3.5, ManualPersons = 12, DailyLitresPerPerson = 180, StorageDays = 2,
                DomesticProvided = 5, HazardClass = FireHazardClass.High, SprinklerDensity = 7.5,
                SprinklerArea = 260, HasCabinets = false, HasHydrants = true, CabinetFlow = 200,
                HydrantFlow = 1500, DurationMinutes = 90, FireProvided = 150, RoofArea = 220,
                AnnualRainfall = 1150, RoofCoefficient = 0.9, FilterEfficiency = 0.95,
                StorageRatio = 0.08, RainProvided = 20
            };
            settings.Save (project);

            ProjectData reloaded = ProjectJson.Deserialize (ProjectJson.Serialize (project, false));
            WaterTankSettings loaded = WaterTankSettings.Load (reloaded);

            Assert.Equal (3.5, loaded.PersonsPerUnit, 3);
            Assert.Equal (12.0, loaded.ManualPersons, 3);
            Assert.Equal (FireHazardClass.High, loaded.HazardClass);
            Assert.False (loaded.HasCabinets);
            Assert.True (loaded.HasHydrants);
            Assert.Equal (90.0, loaded.DurationMinutes, 3);
            Assert.Equal (1150.0, loaded.AnnualRainfall, 3);
            Assert.Equal (0.08, loaded.StorageRatio, 4);
            Assert.Equal (20.0, loaded.RainProvided, 3);
        }

        [Fact]
        public void AProjectWithoutSettingsUsesTheDefaults ()
        {
            WaterTankSettings settings = WaterTankSettings.Load (new ProjectData ());

            Assert.Equal (4.0, settings.PersonsPerUnit, 2);
            Assert.Equal (150.0, settings.DailyLitresPerPerson, 2);
            Assert.Equal (0.06, settings.StorageRatio, 4);
            Assert.True (settings.HasCabinets);
            Assert.False (settings.HasHydrants);
        }

        [Fact]
        public void ReportStatesEveryResultWithItsCheck ()
        {
            ProjectData project = ProjectWithUnits (2);
            project.Parcel.BuildingFootprint = 100.0;
            var settings = new WaterTankSettings {
                AnnualRainfall = 1000.0, DomesticProvided = 2.0, FireProvided = 1.0
            };
            settings.Save (project);

            ReportTable table = ReportBuilder.WaterTanks (project);
            ReportRow[] totals = table.Rows.Where (row => row.Kind == RowKind.GrandTotal).ToArray ();

            Assert.Equal (3, totals.Length);
            Assert.Equal (1.2, ReportTable.CellAt (totals[0], 2).Value.Value, 2);   // 8 kişi × 150 ÷ 1000
            Assert.Equal (3.0, ReportTable.CellAt (totals[1], 2).Value.Value, 2);   // 100 L/dak × 30 dak
            Assert.Equal (4.32, ReportTable.CellAt (totals[2], 2).Value.Value, 2);  // 100×1000×0,8×0,9×0,06

            string[] notes = table.Rows.Select (row => ReportTable.CellAt (row, 1)?.Text ?? string.Empty).ToArray ();
            Assert.Contains ("SAĞLANDI", notes);                                    // kullanma suyu: 2,0 ≥ 1,2
            Assert.Contains (notes, note => note.StartsWith ("YETERSİZ"));          // yangın: 1,0 < 3,0
            Assert.Contains ("Sağlanan hacim girilmedi", notes);                    // yağmur
        }
    }
}
