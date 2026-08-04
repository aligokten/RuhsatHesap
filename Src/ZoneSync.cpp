#include "ZoneSync.hpp"

#include <algorithm>
#include <cctype>
#include <cmath>
#include <map>
#include <set>
#include <sstream>
#include <tuple>

namespace RuhsatHesap {

namespace {

std::string Trim (std::string value)
{
    const auto isSpace = [] (unsigned char character) { return std::isspace (character) != 0; };
    value.erase (value.begin (), std::find_if (value.begin (), value.end (), [&] (unsigned char c) { return !isSpace (c); }));
    value.erase (std::find_if (value.rbegin (), value.rend (), [&] (unsigned char c) { return !isSpace (c); }).base (), value.end ());
    return value;
}

void ReplaceAll (std::string& value, const std::string& from, const std::string& to)
{
    std::size_t position = 0;
    while ((position = value.find (from, position)) != std::string::npos) {
        value.replace (position, from.size (), to);
        position += to.size ();
    }
}

std::string Normalize (std::string value)
{
    value = Trim (std::move (value));
    ReplaceAll (value, "ı", "I"); ReplaceAll (value, "İ", "I");
    ReplaceAll (value, "ş", "S"); ReplaceAll (value, "Ş", "S");
    ReplaceAll (value, "ğ", "G"); ReplaceAll (value, "Ğ", "G");
    ReplaceAll (value, "ü", "U"); ReplaceAll (value, "Ü", "U");
    ReplaceAll (value, "ö", "O"); ReplaceAll (value, "Ö", "O");
    ReplaceAll (value, "ç", "C"); ReplaceAll (value, "Ç", "C");
    std::transform (value.begin (), value.end (), value.begin (), [] (unsigned char c) { return static_cast<char> (std::toupper (c)); });
    return value;
}

ZoneAreaType ParseAreaType (const std::string& value)
{
    const std::string normalized = Normalize (value);
    if (normalized == "NET") return ZoneAreaType::Net;
    if (normalized == "BRUT" || normalized == "GROSS") return ZoneAreaType::Gross;
    if (normalized == "EKLENTI_NET" || normalized == "EKLENTINET") return ZoneAreaType::ExtensionNet;
    if (normalized == "EKLENTI_BRUT" || normalized == "EKLENTIBRUT") return ZoneAreaType::ExtensionGross;
    if (normalized == "BALKON") return ZoneAreaType::Balcony;
    if (normalized == "ORTAK") return ZoneAreaType::Common;
    if (normalized == "MERDIVEN") return ZoneAreaType::Stair;
    if (normalized == "HOL") return ZoneAreaType::Hall;
    if (normalized == "EMSAL") return ZoneAreaType::Emsal;
    if (normalized == "EMSAL_DISI" || normalized == "EMSALDISI") return ZoneAreaType::EmsalOutside;
    if (normalized == "SIGINAK") return ZoneAreaType::Shelter;
    return ZoneAreaType::Unknown;
}

bool IsIndependentUnitArea (ZoneAreaType areaType)
{
    return areaType == ZoneAreaType::Net ||
        areaType == ZoneAreaType::Gross ||
        areaType == ZoneAreaType::ExtensionNet ||
        areaType == ZoneAreaType::ExtensionGross ||
        areaType == ZoneAreaType::Balcony;
}

double JsonNumber (const nlohmann::json& object, const char* key)
{
    if (!object.is_object () || !object.contains (key) || !object.at (key).is_number ()) return 0.0;
    return object.at (key).get<double> ();
}

double WithoutPreviousImport (double currentValue, double importedValue)
{
    const double result = currentValue - importedValue;
    return std::abs (result) < 1.0e-9 ? 0.0 : result;
}

struct UnitAggregate {
    std::string blockName;
    std::string unitNumber;
    std::string floor;
    int floorIndex = 0;
    int floorPriority = -1;
    bool hasFloor = false;
    std::string quality;
    int roomCount = 0;
    double netArea = 0.0;
    double grossArea = 0.0;
    double extensionNetArea = 0.0;
    double extensionGrossArea = 0.0;
    double balconyArea = 0.0;
    bool hasNet = false;
    bool hasGross = false;
    bool hasExtensionNet = false;
    bool hasExtensionGross = false;
    bool hasBalcony = false;
    std::set<std::string> guids;
    int zoneCount = 0;
};

struct FloorAreaAggregate {
    std::string blockName;
    std::string floorName;
    int floorIndex = 0;
    std::map<std::string, double> constructionAreas;
    double emsalArea = 0.0;
    double emsalOutsideArea = 0.0;
};

BlockRecord& FindOrCreateBlock (ProjectData& project, const std::string& normalizedBlockName, ZoneSyncResult& result)
{
    auto iterator = std::find_if (project.blocks.begin (), project.blocks.end (), [&] (const BlockRecord& block) {
        return Normalize (block.name) == normalizedBlockName;
    });
    if (iterator != project.blocks.end ()) return *iterator;

    BlockRecord block;
    block.name = normalizedBlockName;
    project.blocks.push_back (std::move (block));
    ++result.createdBlocks;
    return project.blocks.back ();
}

FloorRecord& FindOrCreateFloor (ProjectData& project, BlockRecord& block, const FloorAreaAggregate& aggregate)
{
    auto iterator = std::find_if (block.floors.begin (), block.floors.end (), [&] (const FloorRecord& floor) {
        return (floor.archicadLinked && floor.archicadStoryIndex == aggregate.floorIndex) || floor.name == aggregate.floorName;
    });
    if (iterator == block.floors.end ()) {
        FloorRecord floor;
        floor.name = aggregate.floorName;
        floor.archicadStoryIndex = aggregate.floorIndex;
        floor.archicadLinked = true;
        const auto story = std::find_if (project.archicadStories.begin (), project.archicadStories.end (), [&] (const ArchicadStory& item) {
            return item.index == aggregate.floorIndex;
        });
        if (story != project.archicadStories.end ()) {
            floor.name = story->name;
            floor.archicadFloorId = story->floorId;
            floor.archicadLevel = story->level;
        }
        block.floors.push_back (std::move (floor));
        return block.floors.back ();
    }
    return *iterator;
}

} // namespace

ParsedZoneName ParseZoneName (const std::string& zoneName)
{
    ParsedZoneName result;
    std::stringstream stream (zoneName);
    std::string token;
    bool firstToken = true;

    while (std::getline (stream, token, '|')) {
        token = Trim (std::move (token));
        if (firstToken) {
            firstToken = false;
            if (Normalize (token) != "RH") return result;
            result.ruhsatZone = true;
            continue;
        }

        std::size_t separator = token.find ('=');
        if (separator == std::string::npos) separator = token.find (':');
        if (separator == std::string::npos) continue;

        const std::string key = Normalize (token.substr (0, separator));
        const std::string value = Trim (token.substr (separator + 1));
        if (key == "BLOK" || key == "B") result.blockName = Normalize (value);
        else if (key == "BB" || key == "BAGIMSIZBOLUM") result.unitNumber = value;
        else if (key == "TIP" || key == "T") result.areaType = ParseAreaType (value);
        else if (key == "ODA" || key == "O") {
            try { result.roomCount = std::max (0, std::stoi (value)); } catch (...) { result.roomCount = 0; }
        } else if (key == "MAHAL" || key == "M") result.roomName = value;
        else if (key == "NITELIK" || key == "N") result.quality = value;
    }

    if (result.blockName.empty ()) result.error = "BLOK eksik";
    else if (result.areaType == ZoneAreaType::Unknown) result.error = "TIP gecersiz veya eksik";
    else result.valid = true;
    return result;
}

ZoneSyncResult SyncZonesToProject (ProjectData& project, const std::vector<ZoneObservation>& observations)
{
    ZoneSyncResult result;
    result.scannedZones = observations.size ();
    if (!project.auxiliaryData.is_object ()) project.auxiliaryData = nlohmann::json::object ();
    std::map<std::pair<std::string, std::string>, UnitAggregate> aggregates;
    std::map<std::tuple<std::string, int, std::string>, FloorAreaAggregate> floorAreaAggregates;
    double commonAreaFromZones = 0.0;
    double shelterAreaFromZones = 0.0;

    // Clear only values previously owned by the Archicad zone import. Manual
    // units that have never been linked to zones are left untouched.
    for (BlockRecord& block : project.blocks) {
        for (FloorRecord& floor : block.floors) {
            for (const auto& [key, importedValue] : floor.archicadZoneConstructionAreas) {
                floor.constructionAreas[key] = WithoutPreviousImport (floor.constructionAreas[key], importedValue);
                if (std::abs (floor.constructionAreas[key]) < 1.0e-9) floor.constructionAreas.erase (key);
            }
            floor.emsalArea = WithoutPreviousImport (floor.emsalArea, floor.archicadZoneEmsalArea);
            floor.emsalOutsideArea = WithoutPreviousImport (floor.emsalOutsideArea, floor.archicadZoneEmsalOutsideArea);
            floor.archicadZoneConstructionAreas.clear ();
            floor.archicadZoneEmsalArea = 0.0;
            floor.archicadZoneEmsalOutsideArea = 0.0;
        }
        for (IndependentUnit& unit : block.units) {
            if (!unit.archicadZoneLinked) continue;
            unit.roomCount = 0;
            unit.grossArea = 0.0;
            unit.netArea = 0.0;
            unit.extensionGrossArea = 0.0;
            unit.extensionNetArea = 0.0;
            unit.balconyArea = 0.0;
            unit.archicadZoneCount = 0;
            unit.archicadZoneGuids.clear ();
        }
    }

    const double previousCommonArea = JsonNumber (project.auxiliaryData, "archicadZoneCommonArea");
    project.auxiliaryData["commonArea"] = WithoutPreviousImport (JsonNumber (project.auxiliaryData, "commonArea"), previousCommonArea);
    project.auxiliaryData["archicadZoneCommonArea"] = 0.0;
    nlohmann::json& shelter = project.auxiliaryData["shelter"];
    if (!shelter.is_object ()) shelter = nlohmann::json::object ();
    const double previousShelterArea = JsonNumber (shelter, "archicadZoneProvidedArea");
    shelter["providedArea"] = WithoutPreviousImport (JsonNumber (shelter, "providedArea"), previousShelterArea);
    shelter["archicadZoneProvidedArea"] = 0.0;

    for (const ZoneObservation& observation : observations) {
        ParsedZoneName parsed = ParseZoneName (observation.zoneName);
        if (!parsed.ruhsatZone) {
            ++result.ignoredZones;
            continue;
        }

        if (IsIndependentUnitArea (parsed.areaType) && parsed.unitNumber.empty ()) parsed.unitNumber = Trim (observation.zoneNumber);
        const bool requiresUnitNumber = IsIndependentUnitArea (parsed.areaType);
        const bool requiresFloor = parsed.areaType != ZoneAreaType::Common;
        if (!parsed.valid || (requiresUnitNumber && parsed.unitNumber.empty ()) ||
            (requiresFloor && observation.storyName.empty ()) || observation.area <= 0.0) {
            ++result.invalidZones;
            continue;
        }

        ++result.recognizedZones;
        if (!requiresUnitNumber) {
            if (parsed.areaType == ZoneAreaType::Common) {
                commonAreaFromZones += observation.area;
                continue;
            }

            const auto floorKey = std::make_tuple (parsed.blockName, observation.storyIndex, observation.storyName);
            FloorAreaAggregate& floorAggregate = floorAreaAggregates[floorKey];
            floorAggregate.blockName = parsed.blockName;
            floorAggregate.floorName = observation.storyName;
            floorAggregate.floorIndex = observation.storyIndex;
            switch (parsed.areaType) {
                case ZoneAreaType::Stair: floorAggregate.constructionAreas["merdiven"] += observation.area; break;
                case ZoneAreaType::Hall: floorAggregate.constructionAreas["hol"] += observation.area; break;
                case ZoneAreaType::Shelter:
                    floorAggregate.constructionAreas["siginak"] += observation.area;
                    shelterAreaFromZones += observation.area;
                    break;
                case ZoneAreaType::Emsal: floorAggregate.emsalArea += observation.area; break;
                case ZoneAreaType::EmsalOutside: floorAggregate.emsalOutsideArea += observation.area; break;
                default: break;
            }
            continue;
        }

        const auto key = std::make_pair (parsed.blockName, parsed.unitNumber);
        UnitAggregate& aggregate = aggregates[key];
        aggregate.blockName = parsed.blockName;
        aggregate.unitNumber = parsed.unitNumber;
        const int floorPriority = (parsed.areaType == ZoneAreaType::Net || parsed.areaType == ZoneAreaType::Gross)
            ? 2
            : (parsed.areaType == ZoneAreaType::Balcony ? 1 : 0);
        if (!observation.storyName.empty () && (!aggregate.hasFloor || floorPriority > aggregate.floorPriority || (floorPriority == aggregate.floorPriority && observation.storyIndex < aggregate.floorIndex))) {
            aggregate.floor = observation.storyName;
            aggregate.floorIndex = observation.storyIndex;
            aggregate.floorPriority = floorPriority;
            aggregate.hasFloor = true;
        }
        if (!parsed.quality.empty ()) aggregate.quality = parsed.quality;
        aggregate.roomCount = std::max (aggregate.roomCount, parsed.roomCount);
        ++aggregate.zoneCount;
        if (!observation.guid.empty ()) aggregate.guids.insert (observation.guid);

        switch (parsed.areaType) {
            case ZoneAreaType::Net: aggregate.netArea += observation.area; aggregate.hasNet = true; break;
            case ZoneAreaType::Gross: aggregate.grossArea += observation.area; aggregate.hasGross = true; break;
            case ZoneAreaType::ExtensionNet: aggregate.extensionNetArea += observation.area; aggregate.hasExtensionNet = true; break;
            case ZoneAreaType::ExtensionGross: aggregate.extensionGrossArea += observation.area; aggregate.hasExtensionGross = true; break;
            case ZoneAreaType::Balcony: aggregate.balconyArea += observation.area; aggregate.hasBalcony = true; break;
            case ZoneAreaType::Common:
            case ZoneAreaType::Stair:
            case ZoneAreaType::Hall:
            case ZoneAreaType::Emsal:
            case ZoneAreaType::EmsalOutside:
            case ZoneAreaType::Shelter:
            case ZoneAreaType::Unknown: break;
        }
    }

    project.auxiliaryData["commonArea"] = JsonNumber (project.auxiliaryData, "commonArea") + commonAreaFromZones;
    project.auxiliaryData["archicadZoneCommonArea"] = commonAreaFromZones;
    shelter["providedArea"] = JsonNumber (shelter, "providedArea") + shelterAreaFromZones;
    shelter["archicadZoneProvidedArea"] = shelterAreaFromZones;

    for (const auto& [key, aggregate] : floorAreaAggregates) {
        (void) key;
        BlockRecord& block = FindOrCreateBlock (project, aggregate.blockName, result);
        FloorRecord& floor = FindOrCreateFloor (project, block, aggregate);
        for (const auto& [areaKey, value] : aggregate.constructionAreas) {
            floor.constructionAreas[areaKey] += value;
            floor.archicadZoneConstructionAreas[areaKey] = value;
        }
        floor.emsalArea += aggregate.emsalArea;
        floor.emsalOutsideArea += aggregate.emsalOutsideArea;
        floor.archicadZoneEmsalArea = aggregate.emsalArea;
        floor.archicadZoneEmsalOutsideArea = aggregate.emsalOutsideArea;
        ++result.updatedFloorAreas;
    }

    for (const auto& [key, aggregate] : aggregates) {
        (void) key;
        BlockRecord& block = FindOrCreateBlock (project, aggregate.blockName, result);

        auto unitIterator = std::find_if (block.units.begin (), block.units.end (), [&] (const IndependentUnit& unit) {
            return Trim (unit.number) == aggregate.unitNumber;
        });
        if (unitIterator == block.units.end ()) {
            IndependentUnit unit;
            unit.number = aggregate.unitNumber;
            block.units.push_back (std::move (unit));
            unitIterator = std::prev (block.units.end ());
            ++result.createdUnits;
        } else {
            ++result.updatedUnits;
        }

        IndependentUnit& unit = *unitIterator;
        if (aggregate.hasFloor) unit.floor = aggregate.floor;
        if (!aggregate.quality.empty ()) unit.quality = aggregate.quality;
        if (aggregate.roomCount > 0 || unit.archicadZoneLinked) unit.roomCount = aggregate.roomCount;
        if (aggregate.hasNet || unit.archicadZoneLinked) unit.netArea = aggregate.netArea;
        if (aggregate.hasGross || unit.archicadZoneLinked) unit.grossArea = aggregate.grossArea;
        if (aggregate.hasExtensionNet || unit.archicadZoneLinked) unit.extensionNetArea = aggregate.extensionNetArea;
        if (aggregate.hasExtensionGross || unit.archicadZoneLinked) unit.extensionGrossArea = aggregate.extensionGrossArea;
        if (aggregate.hasBalcony || unit.archicadZoneLinked) unit.balconyArea = aggregate.balconyArea;
        unit.archicadZoneLinked = true;
        unit.archicadZoneCount = aggregate.zoneCount;
        unit.archicadZoneGuids.assign (aggregate.guids.begin (), aggregate.guids.end ());
    }

    SortIndependentUnits (project);
    return result;
}

} // namespace RuhsatHesap
