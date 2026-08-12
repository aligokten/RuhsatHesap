using System;
using System.Collections.Generic;
using System.Linq;

namespace RuhsatHesap.Core.Model
{
    public sealed class ParcelInfo
    {
        public string ProjectName = string.Empty;
        public string City = string.Empty;
        public string District = string.Empty;
        public string Neighborhood = string.Empty;
        public string Block = string.Empty;       // ada
        public string Parcel = string.Empty;      // parsel
        public double ParcelArea;
        public double TaksRate;
        public double KaksRate;
        public double DirectEmsal;
        public double BuildingFootprint;
        public string EmsalMethod = "kaks";       // "kaks" | "direct"
    }

    public sealed class IndependentUnit
    {
        public string Number = string.Empty;
        public string Floor = string.Empty;
        public string Quality = string.Empty;
        public string Owner = string.Empty;
        public string LandShare = string.Empty;
        public int RoomCount;
        public double GrossArea;
        public double NetArea;
        public double ExtensionGrossArea;
        public double ExtensionNetArea;
        public double BalconyArea;

        // Values owned by the AutoCAD polyline import. Re-running RHTARA
        // replaces only its own contribution, so a manually typed unit keeps
        // its numbers.
        public bool CadLinked;
        public int CadEntityCount;
        public List<string> CadHandles = new List<string> ();

        // Archicad add-on bookkeeping, read and written back untouched so a
        // project shared between both plug-ins survives a round trip.
        public bool ArchicadZoneLinked;
        public int ArchicadZoneCount;
        public List<string> ArchicadZoneGuids = new List<string> ();
    }

    public sealed class FloorRecord
    {
        public string Name = string.Empty;
        public Dictionary<string, double> ThirtyPercentAreas = new Dictionary<string, double> (StringComparer.Ordinal);
        public Dictionary<string, double> ConstructionAreas = new Dictionary<string, double> (StringComparer.Ordinal);
        public double EmsalOutsideArea;
        public double EmsalArea;

        // AutoCAD-owned contributions (see IndependentUnit.CadLinked).
        public Dictionary<string, double> CadConstructionAreas = new Dictionary<string, double> (StringComparer.Ordinal);
        public Dictionary<string, double> CadThirtyPercentAreas = new Dictionary<string, double> (StringComparer.Ordinal);
        public double CadEmsalOutsideArea;
        public double CadEmsalArea;

        // Archicad pass-through fields.
        public Dictionary<string, double> ArchicadZoneConstructionAreas = new Dictionary<string, double> (StringComparer.Ordinal);
        public Dictionary<string, double> ArchicadZoneThirtyPercentAreas = new Dictionary<string, double> (StringComparer.Ordinal);
        public double ArchicadZoneEmsalOutsideArea;
        public double ArchicadZoneEmsalArea;
        public bool ArchicadLinked;
        public int ArchicadStoryIndex;
        public int ArchicadFloorId;
        public double ArchicadLevel;

        /// <summary>Order index used when the floor came from a CAD tag; keeps
        /// basements below and upper floors above in the reports.</summary>
        public int SortIndex;
    }

    public sealed class StoryRecord
    {
        public int Index;
        public int FloorId;
        public string Name = string.Empty;
        public double Level;
    }

    public sealed class BlockRecord
    {
        public string Name = string.Empty;
        public string ZeroLevel = string.Empty;
        public string SubbasementLevel = string.Empty;
        public List<FloorRecord> Floors = new List<FloorRecord> ();
        public List<IndependentUnit> Units = new List<IndependentUnit> ();

        /// <summary>
        /// Matches by <see cref="TextUtil.NormalizeFloorKey"/> rather than an
        /// exact string, so "1.KAT" and "1. Kat" resolve to the same row
        /// instead of silently creating two -- one holding the Merdiven area,
        /// the other the bağımsız bölüm brüt, neither table showing both.
        /// </summary>
        public FloorRecord FindFloor (string name)
        {
            string key = TextUtil.NormalizeFloorKey (name);
            return Floors.FirstOrDefault (floor => TextUtil.NormalizeFloorKey (floor.Name) == key);
        }

        /// <summary>Sum of the gross area of every unit sitting on a floor.</summary>
        public double UnitGrossOnFloor (string floorName)
        {
            string key = TextUtil.NormalizeFloorKey (floorName);
            return Units.Where (unit => TextUtil.NormalizeFloorKey (unit.Floor) == key).Sum (unit => unit.GrossArea);
        }
    }

    public sealed class RetainingWall
    {
        public string Name = string.Empty;
        public double Area;
    }

    /// <summary>
    /// Site-level construction item that is not an istinat duvarı -- foseptik,
    /// trafo binası, su deposu binası vb. Its area adds directly into the Yapı
    /// İnşaat Alanı total, the same way a retaining wall adds into the
    /// construction grand total, but the two are reported separately since
    /// they answer different questions on a ruhsat dosyası.
    /// </summary>
    public sealed class ExtraStructure
    {
        public string Name = string.Empty;
        public double Area;
    }

    public sealed class ProjectData
    {
        public int SchemaVersion = 5;
        public ParcelInfo Parcel = new ParcelInfo ();
        public List<StoryRecord> ArchicadStories = new List<StoryRecord> ();
        public List<BlockRecord> Blocks = new List<BlockRecord> ();
        public List<RetainingWall> RetainingWalls = new List<RetainingWall> ();
        public List<ExtraStructure> ExtraStructures = new List<ExtraStructure> ();
        public int ProvidedParkingSpaces;

        /// <summary>
        /// Free-form panel data: area column lists (thirtyPercentKeys /
        /// constructionKeys), commonArea, shelter, condominium ... Stored as a
        /// JSON object so keys written by the web panel or the Archicad add-on
        /// survive a round trip through AutoCAD.
        /// </summary>
        public Json.JsonValue AuxiliaryData = Json.JsonValue.NewObject ();

        public static readonly string[] DefaultThirtyPercentKeys =
            { "merdiven", "acik_cikma", "sacak", "havuz", "asansor", "kat_holu", "giris_terasi" };

        public static readonly string[] DefaultConstructionKeys =
            { "merdiven", "asansor", "bosluklar", "siginak", "sacak", "makina_odasi",
              "enerji_odasi", "hol", "su_deposu", "haberlesme_odasi" };

        public BlockRecord FindBlock (string normalizedName) =>
            Blocks.FirstOrDefault (block => TextUtil.Normalize (block.Name) == normalizedName);

        public double AuxNumber (string key)
        {
            Json.JsonValue value = AuxiliaryData[key];
            return value.IsNumber || value.IsString ? value.AsDouble () : 0.0;
        }

        public void SetAuxNumber (string key, double value) => AuxiliaryData[key] = Json.JsonValue.Number (value);

        /// <summary>Total common area distributed over the independent units.</summary>
        public double CommonArea => AuxNumber ("commonArea");

        public IReadOnlyList<string> AreaColumnKeys (bool thirtyPercent)
        {
            string field = thirtyPercent ? "thirtyPercentKeys" : "constructionKeys";
            Json.JsonValue stored = AuxiliaryData[field];
            var keys = new List<string> ();
            if (stored.IsArray) {
                foreach (Json.JsonValue item in stored.Items)
                    if (item.IsString && item.StringValue.Length > 0 && !keys.Contains (item.StringValue))
                        keys.Add (item.StringValue);
            }
            if (keys.Count == 0)
                keys.AddRange (thirtyPercent ? DefaultThirtyPercentKeys : DefaultConstructionKeys);
            return keys;
        }

        public void SortUnits ()
        {
            foreach (BlockRecord block in Blocks) {
                List<IndependentUnit> ordered = block.Units
                    .OrderBy (unit => unit.Number, TextUtil.UnitNumberComparer.Instance)
                    .ToList ();
                block.Units.Clear ();
                block.Units.AddRange (ordered);
            }
        }

        /// <summary>
        /// Orders floors of every block by their CAD sort index, keeping
        /// records that never got one (manually typed, or imported from
        /// Archicad) in their original relative position.
        /// </summary>
        public void SortFloors ()
        {
            foreach (BlockRecord block in Blocks) {
                List<FloorRecord> ordered = block.Floors.OrderBy (floor => floor.SortIndex).ToList ();
                block.Floors.Clear ();
                block.Floors.AddRange (ordered);
            }
        }
    }
}
