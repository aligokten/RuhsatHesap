using System;
using System.Collections.Generic;
using System.Linq;
using RuhsatHesap.Core.Geometry;
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

        /// <summary>A point that lies inside this object's outline. Only kat
        /// (floor-level) etiketler carry one; leave at (0,0) for anything the
        /// nested-area reduction never needs to test.</summary>
        public double AnchorX, AnchorY;

        /// <summary>
        /// Rough outline in drawing (world) coordinates, sampled by the
        /// scanner. Only populated for TIP=EMSAL objects, which are the only
        /// ones that ever act as a container in <see cref="TagSync"/>'s
        /// nested-area reduction; every other kalem leaves this empty.
        /// </summary>
        public IReadOnlyList<(double X, double Y)> Polygon = System.Array.Empty<(double, double)> ();

        private RuhsatTag _tag;
        public RuhsatTag Tag => _tag ?? (_tag = RuhsatTag.Parse (TagText));
        public void SetTag (RuhsatTag tag) { _tag = tag; }
    }

    /// <summary>How much area one TIP contributed, for the scan report.</summary>
    public sealed class TypeTally
    {
        public string Type = string.Empty;
        public int Count;
        public double Area;
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
        public int ExtraStructures;
        public readonly List<string> Problems = new List<string> ();

        /// <summary>Recognised area per TIP, so the scan report shows at a
        /// glance which etiket actually reached the tables.</summary>
        public readonly SortedDictionary<string, TypeTally> ByType =
            new SortedDictionary<string, TypeTally> (StringComparer.Ordinal);

        internal void Tally (string type, double area)
        {
            if (string.IsNullOrEmpty (type)) type = "?";
            if (!ByType.TryGetValue (type, out TypeTally tally)) {
                tally = new TypeTally { Type = type };
                ByType[type] = tally;
            }
            tally.Count++;
            tally.Area += area;
        }

        public IEnumerable<TypeTally> Tallies => ByType.Values;

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
        private const string CadExtraStructuresField = "cadExtraStructures";

        public static TagSyncResult Sync (ProjectData project, IReadOnlyList<AreaObservation> observations)
        {
            var result = new TagSyncResult { Scanned = observations.Count };
            if (project.AuxiliaryData == null || !project.AuxiliaryData.IsObject)
                project.AuxiliaryData = JsonValue.NewObject ();

            ClearPreviousImport (project);
            DetectDuplicateAreas (observations, result);

            var unitAggregates = new Dictionary<string, UnitAggregate> (StringComparer.Ordinal);
            var floorAggregates = new Dictionary<string, FloorAggregate> (StringComparer.Ordinal);
            var cadWalls = new List<RetainingWall> ();
            var cadExtraStructures = new List<ExtraStructure> ();
            var resolved = new List<ResolvedObservation> ();
            double commonArea = 0.0, shelterArea = 0.0, parcelArea = 0.0, footprintArea = 0.0;
            bool hasParcel = false, hasFootprint = false;

            // Pass 1: parse, validate and resolve every observation (including
            // the deferred TIP=CustomFloorArea promotion, which needs the
            // project's own area-key columns) but do not aggregate yet -- the
            // nested-area reduction below must see every kalem's final Kind
            // and floor before any of it is summed into a table cell.
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

                string typeName = TypeName (tag);
                switch (tag.Kind) {
                    case AreaKind.ParcelBoundary:
                        parcelArea += observation.Area; hasParcel = true; result.Recognized++;
                        result.Tally (typeName, observation.Area);
                        continue;
                    case AreaKind.BuildingFootprint:
                        footprintArea += observation.Area; hasFootprint = true; result.Recognized++;
                        result.Tally (typeName, observation.Area);
                        continue;
                    case AreaKind.RetainingWall:
                        cadWalls.Add (new RetainingWall {
                            Name = tag.Label.Length > 0 ? tag.Label : "İstinat Duvarı " + (cadWalls.Count + 1),
                            Area = observation.Area
                        });
                        result.Recognized++;
                        result.Tally (typeName, observation.Area);
                        continue;
                    case AreaKind.ExtraStructure:
                        cadExtraStructures.Add (new ExtraStructure {
                            Name = tag.Label.Length > 0 ? tag.Label : "Ek Yapı " + (cadExtraStructures.Count + 1),
                            Area = observation.Area
                        });
                        result.Recognized++;
                        result.Tally (typeName, observation.Area);
                        continue;
                    case AreaKind.Common:
                        commonArea += observation.Area; result.Recognized++;
                        result.Tally (typeName, observation.Area);
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

                resolved.Add (new ResolvedObservation {
                    Observation = observation, Tag = tag, FloorName = floorName,
                    UnitNumber = unitNumber, CustomAreaKey = customAreaKey, TypeName = typeName
                });
            }

            // Pass 1.5: an emsal sınırı is usually drawn as the plain room
            // outline, with no notch cut out for a merdiven/hol/asansör/emsal
            // dışı alan that sits inside it and is separately etiketlenmiş --
            // subtract each such nested alan from the emsal sınırı before its
            // (still too large) area gets aggregated below, so it is not
            // counted once inside the emsal alanı and again in its own kalem.
            ReduceContainedAreas (resolved, result);

            // Pass 2: aggregate using each kalem's already-resolved Kind, kat
            // and (for emsal sınırları) now-corrected Area.
            foreach (ResolvedObservation item in resolved) {
                AreaObservation observation = item.Observation;
                RuhsatTag tag = item.Tag;
                string floorName = item.FloorName;
                string customAreaKey = item.CustomAreaKey;
                string unitNumber = item.UnitNumber;
                string typeName = item.TypeName;

                result.Recognized++;
                result.Tally (typeName, observation.Area);

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
                    case AreaKind.Balcony: {
                        aggregate.BalconyArea += observation.Area; aggregate.HasBalcony = true;
                        // Bağımsız bölüm balkonu kendi payının yanında, emsal
                        // hesabının %30 istisna tablosundaki Açık Çıkma
                        // kalemine de kat düzeyinde yansır.
                        if (floorName.Length > 0) {
                            FloorAggregate balconyFloor = GetOrCreateFloorAggregate (floorAggregates, tag.BlockName, floorName);
                            Accumulate (balconyFloor.ThirtyPercentAreas, "acik_cikma", observation.Area);
                            EnsureAreaKeyColumn (project, "thirtyPercentKeys", "acik_cikma");
                        }
                        break;
                    }
                }
            }

            WarnAboutFloorSpellingVariants (floorAggregates.Values, unitAggregates.Values, result);
            ApplyParcelValues (project, hasParcel, parcelArea, hasFootprint, footprintArea, result);
            ApplyCommonAndShelter (project, commonArea, shelterArea, result);
            ApplyRetainingWalls (project, cadWalls, result);
            ApplyExtraStructures (project, cadExtraStructures, result);
            ApplyFloorAreas (project, floorAggregates.Values, result);
            ApplyUnits (project, unitAggregates.Values, result);

            project.SortUnits ();
            project.SortFloors ();
            return result;
        }

        /// <summary>
        /// Different KAT= spellings for the same kat now merge into one floor
        /// row (see BlockRecord.FindFloor), which fixes the split-table bug,
        /// but the drawing itself is still inconsistent. Flagging it here means
        /// the next tarama tells the user exactly which two spellings to unify,
        /// instead of leaving them to notice a merged row later.
        /// </summary>
        private static void WarnAboutFloorSpellingVariants (IEnumerable<FloorAggregate> floorAggregates,
            IEnumerable<UnitAggregate> unitAggregates, TagSyncResult result)
        {
            var spellingsByKey = new Dictionary<string, HashSet<string>> (StringComparer.Ordinal);
            void Note (string blockName, string floorName)
            {
                if (floorName.Length == 0) return;
                string dictKey = blockName + "|" + TextUtil.NormalizeFloorKey (floorName);
                if (!spellingsByKey.TryGetValue (dictKey, out HashSet<string> spellings)) {
                    spellings = new HashSet<string> (StringComparer.Ordinal);
                    spellingsByKey[dictKey] = spellings;
                }
                spellings.Add (floorName);
            }

            foreach (FloorAggregate aggregate in floorAggregates) Note (aggregate.BlockName, aggregate.FloorName);
            foreach (UnitAggregate aggregate in unitAggregates) if (aggregate.HasFloor) Note (aggregate.BlockName, aggregate.Floor);

            foreach (HashSet<string> spellings in spellingsByKey.Values) {
                if (spellings.Count <= 1) continue;
                result.AddProblem ("Aynı kat farklı yazılmış ve birleştirildi: " +
                    string.Join (" / ", spellings.OrderBy (value => value, StringComparer.Ordinal)) +
                    " — tek bir yazım kullanmanız önerilir.");
            }
        }

        /// <summary>
        /// A very common AutoCAD habit is drawing a closed boundary and then
        /// hatching it for a fill -- and if both the boundary curve and the
        /// hatch get selected and etiketlenmiş the same way, RHTARA sums the
        /// identical region twice, doubling the result. A hatch's area and its
        /// boundary curve's area are computed by different code paths inside
        /// AutoCAD but describe the same geometry, so they land within a
        /// fraction of a percent of each other -- close enough that two
        /// independently drawn rooms essentially never coincide by accident,
        /// but a hatch/boundary pair always does. Flags the pair by handle so
        /// the user can remove one etiket instead of the total being silently
        /// wrong.
        /// </summary>
        private static void DetectDuplicateAreas (IReadOnlyList<AreaObservation> observations, TagSyncResult result)
        {
            // A hatch drawn "pick a point inside the walls" and its boundary
            // curve are not measured through the same code path, and the wall
            // thickness the hatch stops at makes them differ by a genuinely
            // visible amount -- a real reported case had 4,69 and 4,68 m²,
            // i.e. %0,213 apart, which a %0,2 threshold missed by a hair while
            // silently doubling the kalem. %1 is still far tighter than two
            // separately drawn rooms ever land by accident, and this is only
            // an advisory warning, never a change to the numbers.
            const double RelativeTolerance = 0.01; // %1

            var buckets = new Dictionary<string, List<AreaObservation>> (StringComparer.Ordinal);
            foreach (AreaObservation observation in observations) {
                RuhsatTag tag = observation.Tag;
                if (!tag.IsRuhsatTag || !tag.Valid || observation.Area <= 0.0) continue;
                string key = DuplicateBucketKey (tag);
                if (key == null) continue;
                if (!buckets.TryGetValue (key, out List<AreaObservation> list)) {
                    list = new List<AreaObservation> ();
                    buckets[key] = list;
                }
                list.Add (observation);
            }

            foreach (List<AreaObservation> list in buckets.Values) {
                if (list.Count < 2) continue;
                for (int first = 0; first < list.Count; first++) {
                    for (int second = first + 1; second < list.Count; second++) {
                        double areaA = list[first].Area;
                        double areaB = list[second].Area;
                        double relativeDifference = Math.Abs (areaA - areaB) / Math.Max (areaA, areaB);
                        if (relativeDifference > RelativeTolerance) continue;
                        result.AddProblem ("ÇİFT ETİKET: <" + list[first].Handle + "> ve <" + list[second].Handle +
                            "> neredeyse aynı alana sahip (" + TextUtil.FormatArea (areaA) + " ve " +
                            TextUtil.FormatArea (areaB) + " m²) ve aynı kaleme yazılıyor; bu kalem " +
                            TextUtil.FormatArea (areaA + areaB) + " m² olarak hesaplanır. Aynı bölgeyi hem " +
                            "taralı (HATCH) hem sınır çizgisiyle (polyline/region) etiketlemiş olabilirsiniz — " +
                            "RHSOR ile iki nesneyi bulup yalnız birinde etiket bırakın.");
                    }
                }
            }
        }

        /// <summary>
        /// Groups an etiket into the same bucket another etiket lands in only
        /// when the two would add into the exact same cell of a table --
        /// same bağımsız bölüm alanı, or same kat + TIP. Returns null for
        /// kalemler that are legitimately allowed to repeat with the same area
        /// (İSTİNAT, EK_YAPI -- distinct walls or buildings can coincidentally
        /// share a size) and for CustomFloorArea, whose column is only resolved
        /// once project context is available, later in Sync.
        /// </summary>
        private static string DuplicateBucketKey (RuhsatTag tag)
        {
            if (RuhsatTag.IsUnitAreaKind (tag.Kind))
                return "U|" + tag.BlockName + "|" + tag.UnitNumber.Trim () + "|" + tag.Kind;
            switch (tag.Kind) {
                case AreaKind.Stair:
                case AreaKind.Hall:
                case AreaKind.Eave:
                case AreaKind.Elevator:
                case AreaKind.Shelter:
                case AreaKind.Emsal:
                case AreaKind.EmsalOutside:
                    return "F|" + tag.BlockName + "|" + TextUtil.NormalizeFloorKey (tag.FloorName) + "|" + tag.Kind;
                case AreaKind.ParcelBoundary: return "P";
                case AreaKind.BuildingFootprint: return "T";
                default: return null;
            }
        }

        /// <summary>
        /// Kinds that legitimately sit inside a TIP=EMSAL sınırı: it is
        /// normally drawn as the plain room outline, with no notch cut out
        /// for a merdiven/hol/saçak/asansör, serbest %30 kalemi or emsal dışı
        /// alan that sits inside it and is separately etiketlenmiş. Those
        /// still feed their own %30/emsal dışı kalemi, but must be missing
        /// from the (too large) raw emsal alanı or they are counted twice.
        /// </summary>
        private static readonly HashSet<AreaKind> NestedAreaKinds = new HashSet<AreaKind> {
            AreaKind.Stair, AreaKind.Hall, AreaKind.Eave, AreaKind.Elevator,
            AreaKind.CustomFloorArea, AreaKind.EmsalOutside
        };

        /// <summary>
        /// Subtracts every nested %30/emsal dışı kalemi from the smallest
        /// TIP=EMSAL sınırı (same blok, aynı kat) whose sampled outline
        /// contains its representative point. Only a point is tested, the
        /// same simplification RHTARA already uses to match etiketler to kat
        /// sınırı çerçeveleri (see DrawingScanner.FindFrame) -- a full
        /// polygon-in-polygon test is not worth the extra complexity for
        /// plans where the nested alan genuinely sits inside its container.
        /// Objects the scanner never sampled a Polygon for (RHALANTABLO's
        /// free selection, or plain unit tests) simply never match, so this
        /// is a no-op unless the caller opted in.
        /// </summary>
        private static void ReduceContainedAreas (List<ResolvedObservation> resolved, TagSyncResult result)
        {
            var groups = new Dictionary<string, List<ResolvedObservation>> (StringComparer.Ordinal);
            foreach (ResolvedObservation item in resolved) {
                if (item.Tag.IsUnitArea) continue;
                if (item.Tag.Kind != AreaKind.Emsal && !NestedAreaKinds.Contains (item.Tag.Kind)) continue;
                string key = item.Tag.BlockName + "|" + TextUtil.NormalizeFloorKey (item.FloorName);
                if (!groups.TryGetValue (key, out List<ResolvedObservation> list)) {
                    list = new List<ResolvedObservation> ();
                    groups[key] = list;
                }
                list.Add (item);
            }

            foreach (List<ResolvedObservation> group in groups.Values) {
                List<ResolvedObservation> containers = group.Where (item => item.Tag.Kind == AreaKind.Emsal &&
                    item.Observation.Polygon.Count >= 3).ToList ();
                if (containers.Count == 0) continue;

                foreach (ResolvedObservation nested in group.Where (item => NestedAreaKinds.Contains (item.Tag.Kind))) {
                    ResolvedObservation best = null;
                    foreach (ResolvedObservation container in containers) {
                        if (ReferenceEquals (container, nested)) continue;
                        if (!PolygonMath.PointInPolygon (container.Observation.Polygon,
                                nested.Observation.AnchorX, nested.Observation.AnchorY)) continue;
                        if (best == null || container.Observation.Area < best.Observation.Area) best = container;
                    }
                    if (best == null) continue;

                    double subtract = Math.Min (nested.Observation.Area, best.Observation.Area);
                    if (subtract <= Epsilon) continue;
                    best.Observation.Area -= subtract;
                    result.AddProblem ("<" + best.Observation.Handle + "> emsal alanından <" + nested.Observation.Handle +
                        "> (TIP=" + nested.TypeName + ", " + TextUtil.FormatArea (subtract) + " m²) otomatik " +
                        "çıkarıldı (iç sınır dış sınırın içinde tespit edildi); kalan emsal alanı " +
                        TextUtil.FormatArea (best.Observation.Area) + " m².");
                }
            }
        }

        /// <summary>Canonical TIP name used as the tally key.</summary>
        private static string TypeName (RuhsatTag tag)
        {
            string name = tag.AreaTypeName.Length > 0 ? tag.AreaTypeName : RuhsatTag.DefaultTypeName (tag.Kind);
            return TextUtil.Normalize (name);
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

        /// <summary>
        /// A second entry point into the floor aggregate dictionary for
        /// kalemler that arrive through the bağımsız bölüm branch (balkon)
        /// rather than the blok+kat branch that owns floorKey -- the key
        /// format only has to be internally consistent within one Sync call,
        /// since ApplyFloorAreas merges every FloorAggregate it is given onto
        /// the matching FloorRecord by (normalized) name regardless of which
        /// dictionary key produced it.
        /// </summary>
        private static FloorAggregate GetOrCreateFloorAggregate (
            Dictionary<string, FloorAggregate> floorAggregates, string blockName, string floorName)
        {
            string key = blockName + "|BB-KAT|" + floorName;
            if (!floorAggregates.TryGetValue (key, out FloorAggregate aggregate)) {
                aggregate = new FloorAggregate { BlockName = blockName, FloorName = floorName };
                floorAggregates[key] = aggregate;
            }
            return aggregate;
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

        private static void ApplyExtraStructures (ProjectData project, List<ExtraStructure> cadExtraStructures, TagSyncResult result)
        {
            JsonValue previous = project.AuxiliaryData[CadExtraStructuresField];
            var owned = new HashSet<string> (StringComparer.Ordinal);
            if (previous.IsArray)
                foreach (JsonValue item in previous.Items)
                    if (item.IsString) owned.Add (item.StringValue);

            if (owned.Count > 0)
                project.ExtraStructures.RemoveAll (structure => owned.Contains (structure.Name));

            JsonValue names = JsonValue.NewArray ();
            foreach (ExtraStructure structure in cadExtraStructures) {
                project.ExtraStructures.Add (structure);
                names.Add (JsonValue.String (structure.Name));
            }
            project.AuxiliaryData[CadExtraStructuresField] = names;
            result.ExtraStructures = cadExtraStructures.Count;
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
                    // Accumulate rather than assign: two differently-cased KAT=
                    // values for the same conceptual floor now resolve to the
                    // same FloorRecord (see BlockRecord.FindFloor), so more than
                    // one FloorAggregate can legitimately land on it.
                    Accumulate (floor.CadConstructionAreas, entry.Key, value);
                    EnsureAreaKeyColumn (project, "constructionKeys", entry.Key);
                }
                foreach (KeyValuePair<string, double> entry in aggregate.ThirtyPercentAreas) {
                    double value = Round2 (entry.Value);
                    Accumulate (floor.ThirtyPercentAreas, entry.Key, value);
                    Accumulate (floor.CadThirtyPercentAreas, entry.Key, value);
                    EnsureAreaKeyColumn (project, "thirtyPercentKeys", entry.Key);
                }
                double emsal = Round2 (aggregate.EmsalArea);
                double emsalOutside = Round2 (aggregate.EmsalOutsideArea);
                floor.EmsalArea += emsal;
                floor.EmsalOutsideArea += emsalOutside;
                floor.CadEmsalArea += emsal;
                floor.CadEmsalOutsideArea += emsalOutside;
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

        /// <summary>One observation once its Kind, kat and (for a serbest
        /// TIP) area-key are fully resolved but not yet aggregated -- the
        /// hand-off point between Sync's resolve pass and its aggregation
        /// pass, with the nested-area reduction running in between.</summary>
        private sealed class ResolvedObservation
        {
            public AreaObservation Observation;
            public RuhsatTag Tag;
            public string FloorName = string.Empty;
            public string UnitNumber = string.Empty;
            public string CustomAreaKey = string.Empty;
            public string TypeName = string.Empty;
        }

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
