using System;
using System.Collections.Generic;
using RuhsatHesap.Core.Json;

namespace RuhsatHesap.Core.Model
{
    /// <summary>
    /// Reads and writes the Ruhsat Hesap project file. The written format is
    /// the same "ruhsat-hesap-archicad" schema the Archicad add-on uses, so one
    /// project file can travel between the web panel, the Archicad add-on and
    /// this AutoCAD plug-in. The envelope produced by the web panel's
    /// "JSON İndir" button is also accepted on read.
    /// </summary>
    public static class ProjectJson
    {
        public const string FormatName = "ruhsat-hesap-archicad";

        public static string Serialize (ProjectData project, bool indented = true) =>
            ToJson (project).ToJson (indented);

        public static ProjectData Deserialize (string text)
        {
            JsonValue root = JsonValue.Parse (text);
            return FromJson (root);
        }

        public static ProjectData FromJson (JsonValue root)
        {
            if (!root.IsObject) throw new FormatException ("Proje dosyası bir JSON nesnesi değil.");
            // Web panel envelope: { "project": { "data": {...}, "blocks": [...] } }
            if (root["project"].IsObject) return FromWebEnvelope (root["project"]);
            return FromNative (root);
        }

        public static JsonValue ToJson (ProjectData project)
        {
            JsonValue root = JsonValue.NewObject ();
            root["format"] = JsonValue.String (FormatName);
            root["schemaVersion"] = JsonValue.Number (project.SchemaVersion);
            root["parcel"] = ParcelToJson (project.Parcel);

            JsonValue stories = JsonValue.NewArray ();
            foreach (StoryRecord story in project.ArchicadStories) {
                JsonValue item = JsonValue.NewObject ();
                item["index"] = JsonValue.Number (story.Index);
                item["floorId"] = JsonValue.Number (story.FloorId);
                item["name"] = JsonValue.String (story.Name);
                item["level"] = JsonValue.Number (story.Level);
                stories.Add (item);
            }
            root["archicadStories"] = stories;

            JsonValue blocks = JsonValue.NewArray ();
            foreach (BlockRecord block in project.Blocks) blocks.Add (BlockToJson (block));
            root["blocks"] = blocks;

            JsonValue walls = JsonValue.NewArray ();
            foreach (RetainingWall wall in project.RetainingWalls) {
                JsonValue item = JsonValue.NewObject ();
                item["name"] = JsonValue.String (wall.Name);
                item["area"] = JsonValue.Number (wall.Area);
                walls.Add (item);
            }
            root["retainingWalls"] = walls;

            root["providedParkingSpaces"] = JsonValue.Number (project.ProvidedParkingSpaces);
            root["auxiliaryData"] = project.AuxiliaryData ?? JsonValue.NewObject ();
            return root;
        }

        private static JsonValue ParcelToJson (ParcelInfo parcel)
        {
            JsonValue item = JsonValue.NewObject ();
            item["projectName"] = JsonValue.String (parcel.ProjectName);
            item["city"] = JsonValue.String (parcel.City);
            item["district"] = JsonValue.String (parcel.District);
            item["neighborhood"] = JsonValue.String (parcel.Neighborhood);
            item["block"] = JsonValue.String (parcel.Block);
            item["parcel"] = JsonValue.String (parcel.Parcel);
            item["parcelArea"] = JsonValue.Number (parcel.ParcelArea);
            item["taksRate"] = JsonValue.Number (parcel.TaksRate);
            item["kaksRate"] = JsonValue.Number (parcel.KaksRate);
            item["directEmsal"] = JsonValue.Number (parcel.DirectEmsal);
            item["buildingFootprint"] = JsonValue.Number (parcel.BuildingFootprint);
            item["emsalMethod"] = JsonValue.String (parcel.EmsalMethod);
            return item;
        }

        private static JsonValue BlockToJson (BlockRecord block)
        {
            JsonValue item = JsonValue.NewObject ();
            item["name"] = JsonValue.String (block.Name);
            item["zeroLevel"] = JsonValue.String (block.ZeroLevel);
            item["subbasementLevel"] = JsonValue.String (block.SubbasementLevel);

            JsonValue floors = JsonValue.NewArray ();
            foreach (FloorRecord floor in block.Floors) floors.Add (FloorToJson (floor));
            item["floors"] = floors;

            JsonValue units = JsonValue.NewArray ();
            foreach (IndependentUnit unit in block.Units) units.Add (UnitToJson (unit));
            item["units"] = units;
            return item;
        }

        private static JsonValue FloorToJson (FloorRecord floor)
        {
            JsonValue item = JsonValue.NewObject ();
            item["name"] = JsonValue.String (floor.Name);
            item["thirtyPercentAreas"] = MapToJson (floor.ThirtyPercentAreas);
            item["constructionAreas"] = MapToJson (floor.ConstructionAreas);
            item["emsalOutsideArea"] = JsonValue.Number (floor.EmsalOutsideArea);
            item["emsalArea"] = JsonValue.Number (floor.EmsalArea);
            if (floor.SortIndex != 0) item["sortIndex"] = JsonValue.Number (floor.SortIndex);

            if (floor.CadConstructionAreas.Count > 0) item["cadConstructionAreas"] = MapToJson (floor.CadConstructionAreas);
            if (floor.CadThirtyPercentAreas.Count > 0) item["cadThirtyPercentAreas"] = MapToJson (floor.CadThirtyPercentAreas);
            if (floor.CadEmsalArea != 0.0) item["cadEmsalArea"] = JsonValue.Number (floor.CadEmsalArea);
            if (floor.CadEmsalOutsideArea != 0.0) item["cadEmsalOutsideArea"] = JsonValue.Number (floor.CadEmsalOutsideArea);

            if (floor.ArchicadZoneConstructionAreas.Count > 0) item["archicadZoneConstructionAreas"] = MapToJson (floor.ArchicadZoneConstructionAreas);
            if (floor.ArchicadZoneThirtyPercentAreas.Count > 0) item["archicadZoneThirtyPercentAreas"] = MapToJson (floor.ArchicadZoneThirtyPercentAreas);
            if (floor.ArchicadZoneEmsalOutsideArea != 0.0) item["archicadZoneEmsalOutsideArea"] = JsonValue.Number (floor.ArchicadZoneEmsalOutsideArea);
            if (floor.ArchicadZoneEmsalArea != 0.0) item["archicadZoneEmsalArea"] = JsonValue.Number (floor.ArchicadZoneEmsalArea);
            if (floor.ArchicadLinked) {
                item["archicadStoryIndex"] = JsonValue.Number (floor.ArchicadStoryIndex);
                item["archicadFloorId"] = JsonValue.Number (floor.ArchicadFloorId);
                item["archicadLevel"] = JsonValue.Number (floor.ArchicadLevel);
            }
            return item;
        }

        private static JsonValue UnitToJson (IndependentUnit unit)
        {
            JsonValue item = JsonValue.NewObject ();
            item["number"] = JsonValue.String (unit.Number);
            item["floor"] = JsonValue.String (unit.Floor);
            item["quality"] = JsonValue.String (unit.Quality);
            item["owner"] = JsonValue.String (unit.Owner);
            item["landShare"] = JsonValue.String (unit.LandShare);
            item["roomCount"] = JsonValue.Number (unit.RoomCount);
            item["grossArea"] = JsonValue.Number (unit.GrossArea);
            item["netArea"] = JsonValue.Number (unit.NetArea);
            item["extensionGrossArea"] = JsonValue.Number (unit.ExtensionGrossArea);
            item["extensionNetArea"] = JsonValue.Number (unit.ExtensionNetArea);
            item["balconyArea"] = JsonValue.Number (unit.BalconyArea);
            if (unit.CadLinked) {
                item["cadLinked"] = JsonValue.Bool (true);
                item["cadEntityCount"] = JsonValue.Number (unit.CadEntityCount);
                item["cadHandles"] = StringsToJson (unit.CadHandles);
            }
            if (unit.ArchicadZoneLinked) {
                item["archicadZoneLinked"] = JsonValue.Bool (true);
                item["archicadZoneCount"] = JsonValue.Number (unit.ArchicadZoneCount);
                item["archicadZoneGuids"] = StringsToJson (unit.ArchicadZoneGuids);
            }
            return item;
        }

        private static JsonValue MapToJson (Dictionary<string, double> values)
        {
            JsonValue item = JsonValue.NewObject ();
            var keys = new List<string> (values.Keys);
            keys.Sort (StringComparer.Ordinal);
            foreach (string key in keys) item[key] = JsonValue.Number (values[key]);
            return item;
        }

        private static JsonValue StringsToJson (IEnumerable<string> values)
        {
            JsonValue array = JsonValue.NewArray ();
            foreach (string value in values) array.Add (JsonValue.String (value));
            return array;
        }

        private static ProjectData FromNative (JsonValue root)
        {
            var project = new ProjectData ();
            if (root["schemaVersion"].IsNumber) project.SchemaVersion = root["schemaVersion"].AsInt (5);
            ReadParcel (root["parcel"], project.Parcel);
            project.ProvidedParkingSpaces = root["providedParkingSpaces"].AsInt ();
            if (root["auxiliaryData"].IsObject) project.AuxiliaryData = root["auxiliaryData"];

            foreach (JsonValue story in root["archicadStories"].Items) {
                project.ArchicadStories.Add (new StoryRecord {
                    Index = story["index"].AsInt (),
                    FloorId = story["floorId"].AsInt (),
                    Name = story["name"].AsString (),
                    Level = story["level"].AsDouble ()
                });
            }

            foreach (JsonValue blockJson in root["blocks"].Items) {
                var block = new BlockRecord {
                    Name = blockJson["name"].AsString (),
                    ZeroLevel = blockJson["zeroLevel"].AsString (),
                    SubbasementLevel = blockJson["subbasementLevel"].AsString ()
                };
                foreach (JsonValue floorJson in blockJson["floors"].Items) {
                    var floor = new FloorRecord {
                        Name = floorJson["name"].AsString (),
                        EmsalOutsideArea = floorJson["emsalOutsideArea"].AsDouble (),
                        EmsalArea = floorJson["emsalArea"].AsDouble (),
                        SortIndex = floorJson["sortIndex"].AsInt (),
                        CadEmsalArea = floorJson["cadEmsalArea"].AsDouble (),
                        CadEmsalOutsideArea = floorJson["cadEmsalOutsideArea"].AsDouble (),
                        ArchicadZoneEmsalArea = floorJson["archicadZoneEmsalArea"].AsDouble (),
                        ArchicadZoneEmsalOutsideArea = floorJson["archicadZoneEmsalOutsideArea"].AsDouble (),
                        ArchicadLinked = floorJson.Has ("archicadStoryIndex") || floorJson.Has ("archicadFloorId"),
                        ArchicadStoryIndex = floorJson["archicadStoryIndex"].AsInt (),
                        ArchicadFloorId = floorJson["archicadFloorId"].AsInt (),
                        ArchicadLevel = floorJson["archicadLevel"].AsDouble ()
                    };
                    ReadMap (floorJson["thirtyPercentAreas"], floor.ThirtyPercentAreas);
                    ReadMap (floorJson["constructionAreas"], floor.ConstructionAreas);
                    ReadMap (floorJson["cadThirtyPercentAreas"], floor.CadThirtyPercentAreas);
                    ReadMap (floorJson["cadConstructionAreas"], floor.CadConstructionAreas);
                    ReadMap (floorJson["archicadZoneThirtyPercentAreas"], floor.ArchicadZoneThirtyPercentAreas);
                    ReadMap (floorJson["archicadZoneConstructionAreas"], floor.ArchicadZoneConstructionAreas);
                    block.Floors.Add (floor);
                }
                foreach (JsonValue unitJson in blockJson["units"].Items)
                    block.Units.Add (ReadUnit (unitJson, "number"));
                project.Blocks.Add (block);
            }

            foreach (JsonValue wallJson in root["retainingWalls"].Items) {
                project.RetainingWalls.Add (new RetainingWall {
                    Name = wallJson["name"].AsString (),
                    Area = wallJson["area"].AsDouble ()
                });
            }

            project.SortUnits ();
            return project;
        }

        /// <summary>Reads the envelope written by the web panel's JSON export.</summary>
        private static ProjectData FromWebEnvelope (JsonValue webProject)
        {
            var project = new ProjectData ();
            JsonValue data = webProject["data"];
            ReadParcel (data, project.Parcel);
            project.ProvidedParkingSpaces = data["providedParkingSpaces"].AsInt ();
            if (data["auxiliaryData"].IsObject) project.AuxiliaryData = data["auxiliaryData"];
            foreach (JsonValue story in data["archicadStories"].Items) {
                project.ArchicadStories.Add (new StoryRecord {
                    Index = story["index"].AsInt (),
                    FloorId = story["floorId"].AsInt (),
                    Name = story["name"].AsString (),
                    Level = story["level"].AsDouble ()
                });
            }

            foreach (JsonValue blockJson in webProject["blocks"].Items) {
                var block = new BlockRecord {
                    Name = blockJson["name"].AsString (),
                    ZeroLevel = blockJson["zeroLevel"].AsString (),
                    SubbasementLevel = blockJson["subbasementLevel"].AsString ()
                };
                foreach (JsonValue floorJson in blockJson["floors"].Items) {
                    var floor = new FloorRecord {
                        Name = floorJson["name"].AsString (),
                        // The panel names these fields differently from the
                        // native schema; accept both spellings.
                        EmsalArea = floorJson.Has ("emsal") ? floorJson["emsal"].AsDouble () : floorJson["emsalArea"].AsDouble (),
                        EmsalOutsideArea = floorJson.Has ("emsalDisi") ? floorJson["emsalDisi"].AsDouble () : floorJson["emsalOutsideArea"].AsDouble (),
                        ArchicadZoneEmsalArea = floorJson["archicadZoneEmsalArea"].AsDouble (),
                        ArchicadZoneEmsalOutsideArea = floorJson["archicadZoneEmsalOutsideArea"].AsDouble (),
                        ArchicadLinked = floorJson.Has ("archicadStoryIndex") || floorJson.Has ("archicadFloorId"),
                        ArchicadStoryIndex = floorJson["archicadStoryIndex"].AsInt (),
                        ArchicadFloorId = floorJson["archicadFloorId"].AsInt (),
                        ArchicadLevel = floorJson["archicadLevel"].AsDouble ()
                    };
                    ReadMap (floorJson.Has ("exempt30") ? floorJson["exempt30"] : floorJson["thirtyPercentAreas"], floor.ThirtyPercentAreas);
                    ReadMap (floorJson.Has ("constructionValues") ? floorJson["constructionValues"] : floorJson["constructionAreas"], floor.ConstructionAreas);
                    ReadMap (floorJson["archicadZoneConstructionAreas"], floor.ArchicadZoneConstructionAreas);
                    ReadMap (floorJson["archicadZoneThirtyPercentAreas"], floor.ArchicadZoneThirtyPercentAreas);
                    block.Floors.Add (floor);
                }
                foreach (JsonValue unitJson in blockJson["units"].Items)
                    block.Units.Add (ReadUnit (unitJson, "no"));
                project.Blocks.Add (block);
            }

            foreach (JsonValue wallJson in webProject["retainingWalls"].Items) {
                project.RetainingWalls.Add (new RetainingWall {
                    Name = wallJson["name"].AsString (),
                    Area = wallJson["area"].AsDouble ()
                });
            }

            project.SortUnits ();
            return project;
        }

        private static IndependentUnit ReadUnit (JsonValue unitJson, string numberField)
        {
            var unit = new IndependentUnit {
                Number = unitJson.Has (numberField) ? unitJson[numberField].AsString () : unitJson["number"].AsString (),
                Floor = unitJson["floor"].AsString (),
                Quality = unitJson["quality"].AsString (),
                Owner = unitJson["owner"].AsString (),
                LandShare = unitJson["landShare"].AsString (),
                RoomCount = unitJson["roomCount"].AsInt (),
                GrossArea = unitJson["grossArea"].AsDouble (),
                NetArea = unitJson["netArea"].AsDouble (),
                ExtensionGrossArea = unitJson["extensionGrossArea"].AsDouble (),
                ExtensionNetArea = unitJson["extensionNetArea"].AsDouble (),
                BalconyArea = unitJson["balconyArea"].AsDouble (),
                CadLinked = unitJson["cadLinked"].AsBool (),
                CadEntityCount = unitJson["cadEntityCount"].AsInt (),
                ArchicadZoneLinked = unitJson["archicadZoneLinked"].AsBool (),
                ArchicadZoneCount = unitJson["archicadZoneCount"].AsInt ()
            };
            foreach (JsonValue handle in unitJson["cadHandles"].Items)
                if (handle.IsString) unit.CadHandles.Add (handle.StringValue);
            foreach (JsonValue guid in unitJson["archicadZoneGuids"].Items)
                if (guid.IsString) unit.ArchicadZoneGuids.Add (guid.StringValue);
            return unit;
        }

        private static void ReadParcel (JsonValue source, ParcelInfo parcel)
        {
            if (!source.IsObject) return;
            parcel.ProjectName = source["projectName"].AsString (parcel.ProjectName);
            parcel.City = source["city"].AsString (parcel.City);
            parcel.District = source["district"].AsString (parcel.District);
            parcel.Neighborhood = source["neighborhood"].AsString (parcel.Neighborhood);
            parcel.Block = source["block"].AsString (parcel.Block);
            parcel.Parcel = source["parcel"].AsString (parcel.Parcel);
            parcel.ParcelArea = source["parcelArea"].AsDouble ();
            parcel.TaksRate = source["taksRate"].AsDouble ();
            parcel.KaksRate = source["kaksRate"].AsDouble ();
            parcel.DirectEmsal = source["directEmsal"].AsDouble ();
            parcel.BuildingFootprint = source["buildingFootprint"].AsDouble ();
            string method = source["emsalMethod"].AsString ();
            if (method.Length > 0) parcel.EmsalMethod = method;
        }

        private static void ReadMap (JsonValue source, Dictionary<string, double> target)
        {
            if (!source.IsObject) return;
            foreach (KeyValuePair<string, JsonValue> member in source.Members) {
                double value = member.Value.AsDouble ();
                if (value != 0.0 || member.Value.IsNumber) target[member.Key] = value;
            }
        }
    }
}
