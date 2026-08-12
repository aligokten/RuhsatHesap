using System.Collections.Generic;
using System.Linq;
using RuhsatHesap.Core.Model;
using RuhsatHesap.Core.Tagging;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    /// <summary>
    /// A TIP=EMSAL sınırı is usually drawn as the plain room outline, with no
    /// notch cut out for a merdiven/hol/asansör/emsal dışı alan that sits
    /// inside it and is separately etiketlenmiş. TagSync.Sync must subtract
    /// that nested alan from the emsal sınırı so it is not counted twice --
    /// once inside the (too large) emsal alanı, once again in its own kalem.
    /// These use synthetic square outlines since there is no AutoCAD entity
    /// in this test project; the point-in-polygon math itself is CAD-free.
    /// </summary>
    public class ContainedAreaReductionTests
    {
        private static AreaObservation Observation (string tag, double area, string floor, string handle)
        {
            return new AreaObservation { TagText = tag, Area = area, FloorName = floor, Handle = handle };
        }

        private static void SetSquare (AreaObservation observation, double minX, double minY, double maxX, double maxY)
        {
            observation.Polygon = new (double X, double Y)[] {
                (minX, minY), (maxX, minY), (maxX, maxY), (minX, maxY)
            };
        }

        private static void SetAnchor (AreaObservation observation, double x, double y)
        {
            observation.AnchorX = x;
            observation.AnchorY = y;
        }

        [Fact]
        public void ThirtyPercentAreaNestedInsideEmsalSiniriIsSubtracted ()
        {
            AreaObservation container = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 100.0, "Zemin", "E1");
            SetSquare (container, 0, 0, 10, 10);

            AreaObservation nested = Observation ("RH|BLOK=A|KAT=Zemin|TIP=MERDIVEN|HESAP=EMSAL", 4.69, "Zemin", "M1");
            SetAnchor (nested, 2, 2);

            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> { container, nested });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (95.31, floor.EmsalArea, 2);
            Assert.Equal (4.69, floor.ThirtyPercentAreas["merdiven"], 2);

            Assert.Contains (result.Problems, message =>
                message.Contains ("<E1>") && message.Contains ("<M1>") && message.Contains ("otomatik"));
        }

        [Fact]
        public void EmsalDisiAlanNestedInsideEmsalSiniriIsAlsoSubtracted ()
        {
            AreaObservation container = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 100.0, "Zemin", "E2");
            SetSquare (container, 0, 0, 10, 10);

            AreaObservation nested = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL_DISI", 6.0, "Zemin", "D1");
            SetAnchor (nested, 5, 5);

            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> { container, nested });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (94.0, floor.EmsalArea, 2);
            Assert.Equal (6.0, floor.EmsalOutsideArea, 2);
        }

        [Fact]
        public void NestedAreaOutsideEmsalSiniriIsNotSubtracted ()
        {
            AreaObservation container = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 100.0, "Zemin", "E3");
            SetSquare (container, 0, 0, 10, 10);

            AreaObservation nested = Observation ("RH|BLOK=A|KAT=Zemin|TIP=MERDIVEN|HESAP=EMSAL", 4.69, "Zemin", "M2");
            SetAnchor (nested, 50, 50); // well outside the emsal sınırı

            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> { container, nested });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (100.0, floor.EmsalArea, 2);
            Assert.DoesNotContain (result.Problems, message => message.Contains ("otomatik çıkarıldı"));
        }

        [Fact]
        public void SmallestContainingEmsalSiniriIsReducedWhenSeveralOverlap ()
        {
            AreaObservation big = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 300.0, "Zemin", "BIG");
            SetSquare (big, 0, 0, 20, 20);

            AreaObservation small = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 80.0, "Zemin", "SMALL");
            SetSquare (small, 0, 0, 10, 10);

            AreaObservation nested = Observation ("RH|BLOK=A|KAT=Zemin|TIP=MERDIVEN|HESAP=EMSAL", 4.69, "Zemin", "M3");
            SetAnchor (nested, 2, 2); // inside both

            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> { big, small, nested });

            // The smaller (more specific) sınır loses the area, not the big one.
            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (375.31, floor.EmsalArea, 2); // 300 + (80 - 4.69)
            Assert.Contains (result.Problems, message => message.Contains ("<SMALL>") && message.Contains ("<M3>"));
        }

        [Fact]
        public void DifferentFloorsDoNotInterfere ()
        {
            AreaObservation container = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 100.0, "Zemin", "E4");
            SetSquare (container, 0, 0, 10, 10);

            // Same XY position as the container, but a different kat -- floor
            // plans are routinely drawn stacked at identical coordinates.
            AreaObservation nested = Observation ("RH|BLOK=A|KAT=1|TIP=MERDIVEN|HESAP=EMSAL", 4.69, "1", "M4");
            SetAnchor (nested, 2, 2);

            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> { container, nested });

            FloorRecord zemin = project.Blocks[0].Floors.Single (floor => floor.Name == "Zemin");
            Assert.Equal (100.0, zemin.EmsalArea, 2);
        }

        [Fact]
        public void SubtractionNeverDrivesEmsalSiniriBelowZero ()
        {
            AreaObservation container = Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 3.0, "Zemin", "E5");
            SetSquare (container, 0, 0, 10, 10);

            AreaObservation nested = Observation ("RH|BLOK=A|KAT=Zemin|TIP=MERDIVEN|HESAP=EMSAL", 10.0, "Zemin", "M5");
            SetAnchor (nested, 2, 2);

            var project = new ProjectData ();
            TagSync.Sync (project, new List<AreaObservation> { container, nested });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (0.0, floor.EmsalArea, 2);
            // The nested kalemi itself still keeps its full, un-clamped area.
            Assert.Equal (10.0, floor.ThirtyPercentAreas["merdiven"], 2);
        }

        [Fact]
        public void ObservationsWithoutPolygonDataAreNeverReduced ()
        {
            // Regression guard: RHALANTABLO and every pre-existing test build
            // AreaObservation without Polygon/Anchor -- the feature must stay
            // off for them rather than matching on the default (0,0) point.
            var project = new ProjectData ();
            TagSyncResult result = TagSync.Sync (project, new List<AreaObservation> {
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=EMSAL", 46.40, "Zemin", "E6"),
                Observation ("RH|BLOK=A|KAT=Zemin|TIP=MERDIVEN|HESAP=EMSAL", 4.69, "Zemin", "M6")
            });

            FloorRecord floor = project.Blocks[0].Floors.Single ();
            Assert.Equal (46.40, floor.EmsalArea, 2);
            Assert.DoesNotContain (result.Problems, message => message.Contains ("otomatik çıkarıldı"));
        }
    }
}
