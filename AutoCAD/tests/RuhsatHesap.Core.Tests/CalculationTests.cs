using RuhsatHesap.Core;
using RuhsatHesap.Core.Model;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    public class CalculationTests
    {
        private static ProjectData Sample ()
        {
            var project = new ProjectData ();
            project.Parcel.ParcelArea = 1000.0;
            project.Parcel.TaksRate = 0.40;
            project.Parcel.KaksRate = 1.50;
            project.Parcel.BuildingFootprint = 400.0;

            var block = new BlockRecord { Name = "A" };
            var ground = new FloorRecord { Name = "ZEMİN KAT", EmsalArea = 380.0, EmsalOutsideArea = 20.0 };
            ground.ConstructionAreas["merdiven"] = 14.0;
            ground.ThirtyPercentAreas["kat_holu"] = 12.0;
            var first = new FloorRecord { Name = "1. KAT", EmsalArea = 380.0 };
            first.ConstructionAreas["merdiven"] = 14.0;
            block.Floors.Add (ground);
            block.Floors.Add (first);

            block.Units.Add (new IndependentUnit { Number = "1", Floor = "ZEMİN KAT", GrossArea = 75.0, NetArea = 62.0 });
            block.Units.Add (new IndependentUnit { Number = "2", Floor = "ZEMİN KAT", GrossArea = 110.0, NetArea = 92.0 });
            block.Units.Add (new IndependentUnit { Number = "3", Floor = "1. KAT", GrossArea = 150.0, NetArea = 128.0 });
            block.Units.Add (new IndependentUnit { Number = "4", Floor = "1. KAT", GrossArea = 200.0, NetArea = 172.0 });
            project.Blocks.Add (block);

            project.RetainingWalls.Add (new RetainingWall { Name = "Doğu", Area = 30.0 });
            return project;
        }

        [Fact]
        public void TaksAndKaksLimits ()
        {
            CalculationSummary summary = CalculationEngine.Calculate (Sample ());
            Assert.Equal (400.0, summary.MaxFootprint, 2);
            Assert.Equal (1500.0, summary.MaxEmsal, 2);
        }

        [Fact]
        public void DirectEmsalOverridesKaks ()
        {
            ProjectData project = Sample ();
            project.Parcel.EmsalMethod = "direct";
            project.Parcel.DirectEmsal = 900.0;

            CalculationSummary summary = CalculationEngine.Calculate (project);
            Assert.Equal (900.0, summary.MaxEmsal, 2);
            Assert.Equal (760.0, summary.CalculatedEmsal, 2);
            Assert.True (summary.EmsalOk);
            Assert.Equal (140.0, summary.EmsalBalance, 2);
        }

        [Fact]
        public void EmsalExcessIsReported ()
        {
            ProjectData project = Sample ();
            project.Parcel.KaksRate = 0.50;   // 500 m² hak, 760 m² hesaplanan

            CalculationSummary summary = CalculationEngine.Calculate (project);
            Assert.False (summary.EmsalOk);
            Assert.Equal (260.0, summary.EmsalExcess, 2);
            Assert.Equal (0.0, summary.EmsalBalance, 2);
        }

        [Fact]
        public void ThirtyPercentTableIsReportedButNotAddedToEmsal ()
        {
            CalculationSummary summary = CalculationEngine.Calculate (Sample ());
            // Aynı davranış web panelinde ve Archicad eklentisinde de geçerli:
            // %30 istisna tablosu ayrı raporlanır, emsale kendiliğinden eklenmez.
            Assert.Equal (12.0, summary.ThirtyPercentTotal, 2);
            Assert.Equal (20.0, summary.EmsalOutsideTotal, 2);
            Assert.Equal (760.0, summary.CalculatedEmsal, 2);
        }

        [Fact]
        public void ConstructionAreaIncludesUnitGrossAndFloorItems ()
        {
            CalculationSummary summary = CalculationEngine.Calculate (Sample ());
            // 14 + 14 merdiven + 535 bağımsız bölüm brüt
            Assert.Equal (563.0, summary.ConstructionArea, 2);
            Assert.Equal (30.0, summary.RetainingWallArea, 2);
            Assert.Equal (593.0, summary.ConstructionGrandTotal, 2);
        }

        [Theory]
        [InlineData (0.0, 0.0)]
        [InlineData (79.0, 1.0 / 3.0)]
        [InlineData (80.0, 0.5)]
        [InlineData (119.0, 0.5)]
        [InlineData (120.0, 1.0)]
        [InlineData (179.0, 1.0)]
        [InlineData (180.0, 2.0)]
        public void ParkingContributionFollowsTheRegulation (double grossArea, double expected)
        {
            Assert.Equal (expected, CalculationEngine.ParkingContribution (grossArea), 6);
        }

        [Fact]
        public void ParkingAndTreesAreRoundedUp ()
        {
            CalculationSummary summary = CalculationEngine.Calculate (Sample ());
            // 1/3 (75) + 1/2 (110) + 1 (150) + 2 (200) = 3,83 -> 4 araç
            Assert.Equal (4, summary.RequiredParkingSpaces);
            // (1000 - 400) / 30 = 20 ağaç
            Assert.Equal (20, summary.RequiredTrees);
            Assert.Equal (4, summary.UnitCount);
        }
    }
}
