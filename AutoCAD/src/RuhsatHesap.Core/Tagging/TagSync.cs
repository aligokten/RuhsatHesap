using System;
using System.Collections.Generic;
using System.Linq;
using RuhsatHesap.Core.Json;
using RuhsatHesap.Core.Model;

namespace RuhsatHesap.Core.Tagging
{
    /// <summary>One measured, tagged drawing object.</summary>
    public sealed class AreaObservation
    {
        /// <summary>Raw etiket text read from XDATA, a label or the layer name.</summary>
        public string TagText = string.Empty;
        /// <summary>Measured area in square metres (already unit-scaled).</summary>
        public double Area;
        /// <summary>Floor resolved by the scanner: KAT= in the etiket, the kat
        /// sınırı frame the object sits in, or the drawing's active floor.</summary>
        public string FloorName = string.Empty;
        /// <summary>AutoCAD entity handle, stored so a unit can be traced back
        /// to the polylines it was measured from.</summary>
        public string Handle = string.Empty;
        public string Layer = string.Empty;

        private RuhsatTag _tag;
        public RuhsatTag Tag => _tag ?? (_tag = RuhsatTag.Parse (TagText));
        public void SetTag (RuhsatTag tag) { _tag = tag; }
    }

    public sealed class TagSyncResult
    {
        public int Scanned;
        public int Recognized;
        public int Ignored;
        public int Invalid;
        public int CreatedBlocks;
        public int CreatedFloors;
        public int CreatedUnits;
        public int UpdatedUnits;
        public int UpdatedFloorAreas;
        public double ParcelArea;
        public double FootprintArea;
        public double CommonArea;
        public double ShelterArea;
        public int RetainingWalls;
        public readonly List<string> Problems = new List<string> ();

        public void AddProblem (string message)
        {
            // A handful of messages is enough to point the user at the drawing;
            // a badly tagged plan should not flood the command line.
            if (Problems.Count < 20 && !Problems.Contains (message)) Problems.Add (message);
        }
    }

    /// <summary>
    /// Turns tagged polylines into Ruhsat Hesap project data. Re-running with
    /// the same drawing is idempotent: every value this import owns is rebuilt
    /// rather than added to, so manually typed numbers survive.
    /// Port of Src/ZoneSync.cpp with AutoCAD's KAT= floors.
    /// </summary>
    public static class TagSync
    {
        private const double Epsilon = 1e-9;
        private const string CadWallsField = "cadRetainingWalls";

        public static TagSyncResult Sync (ProjectData project, IReadOnlyList<AreaObservation> observations)
        {
            var result = new TagSyncResult { Scanned = observations.Count };
            if (project.AuxiliaryData == null || !project.AuxiliaryData.IsObject)
                project.AuxiliaryData = JsonValue.NewObject ();

            ClearPreviousImport (project);

            var unitAggregates = new Dictionary<string, UnitAggregate> (StringComparer.Ordinal);
            var floorAggregates = new Dictionary<string, FloorAggregate> (StringComparer.Ordinal);
            var cadWalls = new List<RetainingWall> ();
            double commonArea = 0.0, shelterArea = 0.0, parcelArea = 0.0, footprintArea = 0.0;
            bool hasParcel = false, hasFootprint = false;

            foreach (AreaObservation observation in observations) {
                RuhsatTag tag = observation.Tag;
                if (!tag.IsRuhsatTag) { result.Ignored++; continue; }
                if (tag.Kind == AreaKind.FloorFrame) { result.Ignored++; continue; }

                string customAreaKey = ResolveCustomAreaKey (project, tag);
                if (observation.Area <= 0.0) {
                    result.Invalid++;
                    result.AddProblem (Describe (observation) + ": alan sıfır (polyline kapalı mı?)");
                    continue;
                }
                if (!tag.Valid) {
                    result.Invalid++;
                    result.AddProblem (Describe (observation) + ": " + (tag.Error.Length > 0 ? tag.Error : "etiket geçersiz"));
                    continue;
                }
                if (tag.Kind == AreaKind.Unknown) {
                    // A free TIP that produced no usable column key, e.g. one
                    // written entirely out of punctuation.
                    result.Invalid++;
                    result.AddProblem (Describe (observation) + ": TIP çözümlenemedi (" + tag.AreaTypeName + ")");
                    continue;
                }

                switch (tag.Kind) {
                    case AreaKind.ParcelBoundary:
                        parcelArea += observation.Area; hasParcel = true; result.Recognized++;
                        continue;
                    case AreaKind.BuildingFootprint:
                        footprintArea += observation.Area; hasFootprint = true; result.Recognized++;
                        continue;
                    case AreaKind.RetainingWall:
                        cadWalls.Add (new RetainingWall {
                            Name = tag.Label.Length > 0 ? tag.Label : "İstinat Duvarı " + (cadWalls.Count + 1),
                            Area = observation.Area
                        });
                        result.Recognized++;
                        continue;
                    case AreaKind.Common:
                        commonArea += observation.Area; result.Recognized++;
                        continue;
                }

                string floorName = observation.FloorName.Trim ();
                if (floorName.Length == 0) floorName = tag.FloorName.Trim ();
                if (tag.NeedsFloor && floorName.Length == 0) {
                    result.Invalid++;
                    result.AddProblem (Describe (observation) + ": kat belirsiz (etikete KAT= ekleyin veya RHKAT ile aktif katı seçin)");
                    continue;
                }

                string unitNumber = tag.UnitNumber.Trim ();
                if (tag.IsUnitArea && unitNumber.Length == 0) {
                    result.Invalid++;
                    result.AddProblem (Describe (observation) + ": BB numarası eksik");
                    continue;
                }

                result.Recognized++;

                if (!tag.IsUnitArea) {
                    string floorKey = tag.BlockName + "\u0001" + floorName;
                    if (!floorAggregates.TryGetValue (floorKey, out FloorAggregate floorAggregate)) {
                        floorAggregate = new FloorAggregate { BlockName = tag.BlockName, FloorName = floorName };
                        floorAggregates[floorKey] = floorAggregate;
                    }
                    switch (tag.Kind) {
                        case AreaKind.Stair:
                        case AreaKind.Hall:
                        case AreaKind.Eave:
                        case AreaKind.Elevator:
                        case AreaKind.CustomFloorArea: {
                            string areaKey = tag.Kind == AreaKind.CustomFloorArea ? customAreaKey : RuhsatTag.FloorAreaKey (tag.Kind);
                            if (areaKey.Length == 0) break;
                            Dictionary<string, double> target = tag.ToThirtyPercentTable
                                ? floorAggregate.ThirtyPercentAreas
                                : floorAggregate.ConstructionAreas;
                            Accumulate (target, areaKey, observation.Area);
                            break;
                        }
                        case AreaKind.Shelter:
                            // Sığınak always feeds Yapı İnşaat Alanı plus the
                            // dedicated sığınak total, never the %30 table.
                            Accumulate (floorAggregate.ConstructionAreas, "siginak", observation.Area);
                            shelterArea += observation.Area;
                            break;
                        case AreaKind.Emsal:
                            floorAggregate.EmsalArea += observation.Area;
                            break;
                        case AreaKind.EmsalOutside:
                            floorAggregate.EmsalOutsideArea += observation.Area;
                            break;
                    }
                    continue;
                }

                string unitKey = tag.BlockName + "\u0001" + unitNumber;
                if (!unitAggregates.TryGetValue (unitKey, out UnitAggregate aggregate)) {
                    aggregate = new UnitAggregate { BlockName = tag.BlockName, UnitNumber = unitNumber };
                    unitAggregates[unitKey] = aggregate;
                }

                // A duplex unit is tagged on more than one floor; the net/brüt
                // zone wins over a balcony, and the lowest floor wins a tie.
                int floorPriority = tag.Kind == AreaKind.Net || tag.Kind == AreaKind.Gross ? 2
                    : tag.Kind == AreaKind.Balcony ? 1 : 0;
                int floorRank = FloorOrder.Rank (floorName);
                if (floorName.Length > 0 && (!aggregate.HasFloor || floorPriority > aggregate.FloorPriority ||
                    (floorPriority == aggregate.FloorPriority && floorRank < aggregate.FloorRank))) {
                    aggregate.Floor = floorName;
                    aggregate.FloorRank = floorRank;
                    aggregate.FloorPriority = floorPriority;
                    aggregate.HasFloor = true;
                }
                if (tag.Quality.Length > 0) aggregate.Quality = tag.Quality;
                aggregate.RoomCount = Math.Max (aggregate.RoomCount, tag.RoomCount);
                aggregate.EntityCount++;
                if (observation.Handle.Length > 0) aggregate.Handles.Add (observation.Handle);

                switch (tag.Kind) {
                    case AreaKind.Net: aggregate.NetArea += observation.Area; aggregate.HasNet = true; break;
                    case AreaKind.Gross: aggregate.GrossArea += observation.Area; aggregate.HasGross = true; break;
                    case AreaKind.ExtensionNet: aggregate.ExtensionNetArea += observation.Area; aggregate.HasExtensionNet = true; break;
                    case AreaKind.ExtensionGross: aggregate.ExtensionGrossArea += observation.Area; aggregate.HasExtensionGross = true; break;
                    case AreaKind.Balcony: aggregate.BalconyArea += observation.Area; aggregate.HasBalcony = true; break;
                }
            }

            ApplyParcelValues (project, hasParcel, parcelArea, hasFootprint, footprintArea, result);
            ApplyCommonAndShelter (project, commonArea, shelterArea, result);
            ApplyRetainingWalls (project, cadWalls, result);
            ApplyFloorAreas (project, floorAggregates.Values, result);
            ApplyUnits (project, unitAggregates.Values, result);

            project.SortUnits ();
            project.SortFloors ();
            return result;
        }

        /// <summary>Short "which object" prefix for a problem message.</summary>
        private static string Describe (AreaObservation observation)
        {
            string handle = observation.Handle.Length > 0 ? "<" + observation.Handle + ">" : "<?>";
            return observation.Layer.Length > 0 ? handle + " (" + observation.Layer + ")" : handle;
        }

        private static void Accumulate (Dictionary<string, double> map, string key, double value)
        {
            map.TryGetValue (key, out double current);
            map[key] = current + value;
        }

        private static double WithoutPreviousImport (double currentValue, double importedValue)
        {
            double result = currentValue - importedValue;
            return Math.Abs (result) < Epsilon ? 0.0 : result;
        }

        /// <summary>Removes everything the previous scan of this drawing wrote.</summary>
        private static void ClearPreviousImport (ProjectData project)
        {
            foreach (BlockRecord block in project.Blocks) {
                foreach (FloorRecord floor in block.Floors) {
                    foreach (KeyValuePair<string, double> entry in floor.CadConstructionAreas.ToList ()) {
                        floor.ConstructionAreas.TryGetValue (entry.Key, out double current);
                        double remaining = WithoutPreviousImport (current, entry.Value);
                        if (Math.Abs (remaining) < Epsilon) floor.ConstructionAreas.Remove (entry.Key);
                        else floor.ConstructionAreas[entry.Key] = remaining;
                    }
                    foreach (KeyValuePair<string, double> entry in floor.CadThirtyPercentAreas.ToList ()) {
                        floor.ThirtyPercentAreas.TryGetValue (entry.Key, out double current);
                        double remaining = WithoutPreviousImport (current, entry.Value);
                        if (Math.Abs (remaining) < Epsilon) floor.ThirtyPercentAreas.Remove (entry.Key);
                        else floor.ThirtyPercentAreas[entry.Key] = remaining;
                    }
                    floor.EmsalArea = WithoutPreviousImport (floor.EmsalArea, floor.CadEmsalArea);
                    floor.EmsalOutsideArea = WithoutPreviousImport (floor.EmsalOutsideArea, floor.CadEmsalOutsideArea);
                    floor.CadConstructionAreas.Clear ();
                    floor.CadThirtyPercentAreas.Clear ();
                    floor.CadEmsalArea = 0.0;
                    floor.CadEmsalOutsideArea = 0.0;
                }
                foreach (IndependentUnit unit in block.Units) {
                    if (!unit.CadLinked) continue;
                    unit.RoomCount = 0;
                    unit.GrossArea = 0.0;
                    unit.NetArea = 0.0;
                    unit.ExtensionGrossArea = 0.0;
                    unit.ExtensionNetArea = 0.0;
                    unit.BalconyArea = 0.0;
                    unit.CadEntityCount = 0;
                    unit.CadHandles.Clear ();
                }
            }

            double previousCommon = project.AuxNumber ("cadCommonArea");
            project.SetAuxNumber ("commonArea", WithoutPreviousImport (project.AuxNumber ("commonArea"), previousCommon));
            project.SetAuxNumber ("cadCommonArea", 0.0);

            JsonValue shelter = project.AuxiliaryData["shelter"];
            if (!shelter.IsObject) {
                shelter = JsonValue.NewObject ();
                project.AuxiliaryData["shelter"] = shelter;
            }
            double previousShelter = shelter["cadProvidedArea"].AsDouble ();
            shelter["providedArea"] = JsonValue.Number (WithoutPreviousImport (shelter["providedArea"].AsDouble (), previousShelter));
            shelter["cadProvidedArea"] = JsonValue.Number (0.0);
        }

        private static string ResolveCustomAreaKey (ProjectData project, RuhsatTag tag)
        {
            if (tag.Kind != AreaKind.Unknown || tag.AreaTypeName.Length == 0 || tag.BlockName.Length == 0)
                return string.Empty;

            string field = tag.ToThirtyPercentTable ? "thirtyPercentKeys" : "constructionKeys";
            string key = FindExistingAreaKey (project, field, tag.AreaTypeName);
            if (key.Length == 0) key = TextUtil.NormalizeAreaKey (tag.AreaTypeName);
            if (key.Length == 0) return string.Empty;

            tag.Kind = AreaKind.CustomFloorArea;
            tag.Valid = true;
            tag.Error = string.Empty;
            return key;
        }

        /// <summary>
        /// Finds an existing area column whose spelling matches the TIP text
        /// once case, spacing, punctuation and Turkish characters are folded
        /// away, and returns the stored key so a new polyline keeps feeding the
        /// same column.
        /// </summary>
        private static string FindExistingAreaKey (ProjectData project, string field, string areaTypeName)
        {
            string requested = TextUtil.NormalizeAreaCode (areaTypeName);
            if (requested.Length == 0) return string.Empty;
            foreach (string key in project.AreaColumnKeys (field == "thirtyPercentKeys"))
                if (TextUtil.NormalizeAreaCode (key) == requested) return key;
            return string.Empty;
        }

        private static void EnsureAreaKeyColumn (ProjectData project, string field, string key)
        {
            JsonValue array = project.AuxiliaryData[field];
            if (!array.IsArray) {
                array = JsonValue.NewArray ();
                foreach (string defaultKey in field == "thirtyPercentKeys"
                             ? ProjectData.DefaultThirtyPercentKeys
                             : ProjectData.DefaultConstructionKeys)
                    array.Add (JsonValue.String (defaultKey));
                project.AuxiliaryData[field] = array;
            }
            foreach (JsonValue item in array.Items)
                if (item.IsString && item.StringValue == key) return;
            array.Add (JsonValue.String (key));
        }

        private static void ApplyParcelValues (ProjectData project, bool hasParcel, double parcelArea,
            bool hasFootprint, double footprintArea, TagSyncResult result)
        {
            // Only overwrite when the drawing actually carries the boundary,
            // so a value typed with RHPARSEL is not wiped by a scan.
            if (hasParcel) {
                project.Parcel.ParcelArea = Round2 (parcelArea);
                result.ParcelArea = project.Parcel.ParcelArea;
            }
            if (hasFootprint) {
                project.Parcel.BuildingFootprint = Round2 (footprintArea);
                result.FootprintArea = project.Parcel.BuildingFootprint;
            }
        }

        private static void ApplyCommonAndShelter (ProjectData project, double commonArea, double shelterArea, TagSyncResult result)
        {
            project.SetAuxNumber ("commonArea", project.AuxNumber ("commonArea") + commonArea);
            project.SetAuxNumber ("cadCommonArea", commonArea);
            JsonValue shelter = project.AuxiliaryData["shelter"];
            shelter["providedArea"] = JsonValue.Number (shelter["providedArea"].AsDouble () + shelterArea);
            shelter["cadProvidedArea"] = JsonValue.Number (shelterArea);
            result.CommonArea = commonArea;
            result.ShelterArea = shelterArea;
        }

        private static void ApplyRetainingWalls (ProjectData project, List<RetainingWall> cadWalls, TagSyncResult result)
        {
            JsonValue previous = project.AuxiliaryData[CadWallsField];
            var owned = new HashSet<string> (StringComparer.Ordinal);
            if (previous.IsArray)
                foreach (JsonValue item in previous.Items)
                    if (item.IsString) owned.Add (item.StringValue);

            if (owned.Count > 0)
                project.RetainingWalls.RemoveAll (wall => owned.Contains (wall.Name));

            JsonValue names = JsonValue.NewArray ();
            foreach (RetainingWall wall in cadWalls) {
                project.RetainingWalls.Add (wall);
                names.Add (JsonValue.String (wall.Name));
            }
            project.AuxiliaryData[CadWallsField] = names;
            result.RetainingWalls = cadWalls.Count;
        }

        private static void ApplyFloorAreas (ProjectData project, IEnumerable<FloorAggregate> aggregates, TagSyncResult result)
        {
            foreach (FloorAggregate aggregate in aggregates.OrderBy (item => item.BlockName, StringComparer.Ordinal)
                         .ThenBy (item => FloorOrder.Rank (item.FloorName))) {
                BlockRecord block = FindOrCreateBlock (project, aggregate.BlockName, result);
                FloorRecord floor = FindOrCreateFloor (block, aggregate.FloorName, result);
                foreach (KeyValuePair<string, double> entry in aggregate.ConstructionAreas) {
                    double value = Round2 (entry.Value);
                    Accumulate (floor.ConstructionAreas, entry.Key, value);
                    floor.CadConstructionAreas[entry.Key] = value;
                    EnsureAreaKeyColumn (project, "constructionKeys", entry.Key);
                }
                foreach (KeyValuePair<string, double> entry in aggregate.ThirtyPercentAreas) {
                    double value = Round2 (entry.Value);
                    Accumulate (floor.ThirtyPercentAreas, entry.Key, value);
                    floor.CadThirtyPercentAreas[entry.Key] = value;
                    EnsureAreaKeyColumn (project, "thirtyPercentKeys", entry.Key);
                }
                double emsal = Round2 (aggregate.EmsalArea);
                double emsalOutside = Round2 (aggregate.EmsalOutsideArea);
                floor.EmsalArea += emsal;
                floor.EmsalOutsideArea += emsalOutside;
                floor.CadEmsalArea = emsal;
                floor.CadEmsalOutsideArea = emsalOutside;
                result.UpdatedFloorAreas++;
            }
        }

        private static void ApplyUnits (ProjectData project, IEnumerable<UnitAggregate> aggregates, TagSyncResult result)
        {
            foreach (UnitAggregate aggregate in aggregates.OrderBy (item => item.BlockName, StringComparer.Ordinal)
                         .ThenBy (item => item.UnitNumber, TextUtil.UnitNumberComparer.Instance)) {
                BlockRecord block = FindOrCreateBlock (project, aggregate.BlockName, result);
                IndependentUnit unit = block.Units.FirstOrDefault (item => item.Number.Trim () == aggregate.UnitNumber);
                if (unit == null) {
                    unit = new IndependentUnit { Number = aggregate.UnitNumber };
                    block.Units.Add (unit);
                    result.CreatedUnits++;
                } else {
                    result.UpdatedUnits++;
                }

                if (aggregate.HasFloor) {
                    unit.Floor = aggregate.Floor;
                    FindOrCreateFloor (block, aggregate.Floor, result);
                }
                if (aggregate.Quality.Length > 0) unit.Quality = aggregate.Quality;
                if (aggregate.RoomCount > 0 || unit.CadLinked) unit.RoomCount = aggregate.RoomCount;
                if (aggregate.HasNet || unit.CadLinked) unit.NetArea = Round2 (aggregate.NetArea);
                if (aggregate.HasGross || unit.CadLinked) unit.GrossArea = Round2 (aggregate.GrossArea);
                if (aggregate.HasExtensionNet || unit.CadLinked) unit.ExtensionNetArea = Round2 (aggregate.ExtensionNetArea);
                if (aggregate.HasExtensionGross || unit.CadLinked) unit.ExtensionGrossArea = Round2 (aggregate.ExtensionGrossArea);
                if (aggregate.HasBalcony || unit.CadLinked) unit.BalconyArea = Round2 (aggregate.BalconyArea);
                unit.CadLinked = true;
                unit.CadEntityCount = aggregate.EntityCount;
                unit.CadHandles.Clear ();
                unit.CadHandles.AddRange (aggregate.Handles.OrderBy (handle => handle, StringComparer.Ordinal));
            }
        }

        private static BlockRecord FindOrCreateBlock (ProjectData project, string normalizedName, TagSyncResult result)
        {
            BlockRecord block = project.FindBlock (normalizedName);
            if (block != null) return block;
            block = new BlockRecord { Name = normalizedName };
            project.Blocks.Add (block);
            result.CreatedBlocks++;
            return block;
        }

        private static FloorRecord FindOrCreateFloor (BlockRecord block, string floorName, TagSyncResult result)
        {
            if (floorName.Length == 0) floorName = "KAT";
            FloorRecord floor = block.FindFloor (floorName);
            if (floor == null) {
                floor = new FloorRecord { Name = floorName, SortIndex = FloorOrder.Rank (floorName) };
                block.Floors.Add (floor);
                result.CreatedFloors++;
            } else if (floor.SortIndex == 0) {
                floor.SortIndex = FloorOrder.Rank (floorName);
            }
            return floor;
        }

        private static double Round2 (double value) => Math.Round (value, 2, MidpointRounding.AwayFromZero);

        private sealed class UnitAggregate
        {
            public string BlockName = string.Empty;
            public string UnitNumber = string.Empty;
            public string Floor = string.Empty;
            public int FloorRank;
            public int FloorPriority = -1;
            public bool HasFloor;
            public string Quality = string.Empty;
            public int RoomCount;
            public double NetArea, GrossArea, ExtensionNetArea, ExtensionGrossArea, BalconyArea;
            public bool HasNet, HasGross, HasExtensionNet, HasExtensionGross, HasBalcony;
            public int EntityCount;
            public readonly HashSet<string> Handles = new HashSet<string> (StringComparer.Ordinal);
        }

        private sealed class FloorAggregate
        {
            public string BlockName = string.Empty;
            public string FloorName = string.Empty;
            public readonly Dictionary<string, double> ConstructionAreas = new Dictionary<string, double> (StringComparer.Ordinal);
            public readonly Dictionary<string, double> ThirtyPercentAreas = new Dictionary<string, double> (StringComparer.Ordinal);
            public double EmsalArea;
            public double EmsalOutsideArea;
        }
    }
}
