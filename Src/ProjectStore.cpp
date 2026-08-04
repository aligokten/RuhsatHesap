#include "ProjectStore.hpp"

#include <fstream>
#include <algorithm>
#include <stdexcept>

namespace RuhsatHesap {

static std::string Text (const nlohmann::json& object, const char* key)
{
    if (!object.contains (key) || object.at (key).is_null ()) return {};
    if (object.at (key).is_string ()) return object.at (key).get<std::string> ();
    return object.at (key).dump ();
}

static double Number (const nlohmann::json& object, const char* key)
{
    if (!object.contains (key) || object.at (key).is_null ()) return 0.0;
    const auto& value = object.at (key);
    if (value.is_number ()) return value.get<double> ();
    if (!value.is_string ()) return 0.0;
    std::string text = value.get<std::string> ();
    if (text.find (',') != std::string::npos) {
        text.erase (std::remove (text.begin (), text.end (), '.'), text.end ());
        std::replace (text.begin (), text.end (), ',', '.');
    }
    try { return std::stod (text); } catch (...) { return 0.0; }
}

static ProjectData LoadWebPanelProject (const nlohmann::json& envelope)
{
    ProjectData result;
    result.sourceEnvelope = envelope;
    const nlohmann::json& project = envelope.at ("project");
    const nlohmann::json& data = project.value ("data", nlohmann::json::object ());
    result.parcel.projectName = Text (data, "projectName");
    result.parcel.city = Text (data, "city");
    result.parcel.district = Text (data, "district");
    result.parcel.neighborhood = Text (data, "neighborhood");
    result.parcel.block = Text (data, "block");
    result.parcel.parcel = Text (data, "parcel");
    result.parcel.parcelArea = Number (data, "parcelArea");
    result.parcel.taksRate = Number (data, "taksRate");
    result.parcel.kaksRate = Number (data, "kaksRate");
    result.parcel.directEmsal = Number (data, "directEmsal");
    result.parcel.buildingFootprint = Number (data, "buildingFootprint");
    result.parcel.emsalMethod = Text (data, "emsalMethod");
    result.providedParkingSpaces = static_cast<int> (Number (data, "providedParkingSpaces"));
    if (data.contains ("auxiliaryData") && data.at ("auxiliaryData").is_object ())
        result.auxiliaryData = data.at ("auxiliaryData");
    if (data.contains ("archicadStories") && data.at ("archicadStories").is_array ())
        result.archicadStories = data.at ("archicadStories").get<std::vector<ArchicadStory>> ();
    const nlohmann::json webBlocks = project.value ("blocks", nlohmann::json::array ());
    for (const auto& blockJson : webBlocks) {
        BlockRecord block;
        block.name = Text (blockJson, "name");
        block.zeroLevel = Text (blockJson, "zeroLevel");
        block.subbasementLevel = Text (blockJson, "subbasementLevel");
        const nlohmann::json webFloors = blockJson.value ("floors", nlohmann::json::array ());
        for (const auto& floorJson : webFloors) {
            FloorRecord floor;
            floor.name = Text (floorJson, "name");
            floor.emsalOutsideArea = Number (floorJson, "emsalDisi");
            floor.emsalArea = Number (floorJson, "emsal");
            floor.archicadZoneEmsalOutsideArea = Number (floorJson, "archicadZoneEmsalOutsideArea");
            floor.archicadZoneEmsalArea = Number (floorJson, "archicadZoneEmsalArea");
            floor.archicadLinked = floorJson.contains ("archicadStoryIndex") || floorJson.contains ("archicadFloorId");
            floor.archicadStoryIndex = static_cast<int> (Number (floorJson, "archicadStoryIndex"));
            floor.archicadFloorId = static_cast<int> (Number (floorJson, "archicadFloorId"));
            floor.archicadLevel = Number (floorJson, "archicadLevel");
            const nlohmann::json exemptAreas = floorJson.value ("exempt30", nlohmann::json::object ());
            for (const auto& [key, value] : exemptAreas.items ())
                floor.thirtyPercentAreas[key] = value.is_string () ? Number (nlohmann::json {{"v",value}}, "v") : value.get<double> ();
            const nlohmann::json constructionAreas = floorJson.value ("constructionValues", nlohmann::json::object ());
            for (const auto& [key, value] : constructionAreas.items ())
                floor.constructionAreas[key] = value.is_string () ? Number (nlohmann::json {{"v",value}}, "v") : value.get<double> ();
            const nlohmann::json zoneConstructionAreas = floorJson.value ("archicadZoneConstructionAreas", nlohmann::json::object ());
            for (const auto& [key, value] : zoneConstructionAreas.items ())
                floor.archicadZoneConstructionAreas[key] = value.is_string () ? Number (nlohmann::json {{"v",value}}, "v") : value.get<double> ();
            block.floors.push_back (std::move (floor));
        }
        const nlohmann::json webUnits = blockJson.value ("units", nlohmann::json::array ());
        for (const auto& unitJson : webUnits) {
            IndependentUnit unit;
            unit.number = Text (unitJson, "no");
            unit.floor = Text (unitJson, "floor");
            unit.quality = Text (unitJson, "quality");
            unit.owner = Text (unitJson, "owner");
            unit.landShare = Text (unitJson, "landShare");
            unit.roomCount = static_cast<int> (Number (unitJson, "roomCount"));
            unit.grossArea = Number (unitJson, "grossArea");
            unit.netArea = Number (unitJson, "netArea");
            unit.extensionGrossArea = Number (unitJson, "extensionGrossArea");
            unit.extensionNetArea = Number (unitJson, "extensionNetArea");
            unit.balconyArea = Number (unitJson, "balconyArea");
            unit.archicadZoneLinked = unitJson.value ("archicadZoneLinked", false);
            unit.archicadZoneCount = static_cast<int> (Number (unitJson, "archicadZoneCount"));
            if (unitJson.contains ("archicadZoneGuids") && unitJson.at ("archicadZoneGuids").is_array ())
                unit.archicadZoneGuids = unitJson.at ("archicadZoneGuids").get<std::vector<std::string>> ();
            block.units.push_back (std::move (unit));
        }
        SortIndependentUnits (block);
        result.blocks.push_back (std::move (block));
    }
    const nlohmann::json webWalls = project.value ("retainingWalls", nlohmann::json::array ());
    for (const auto& wallJson : webWalls)
        result.retainingWalls.push_back ({Text (wallJson, "name"), Number (wallJson, "area")});
    return result;
}

static nlohmann::json MergeProjectIntoWebEnvelope (const ProjectData& project)
{
    nlohmann::json envelope = project.sourceEnvelope;
    nlohmann::json& webProject = envelope["project"];
    webProject["data"]["archicadStories"] = project.archicadStories;
    webProject["data"]["auxiliaryData"] = project.auxiliaryData;

    nlohmann::json& webBlocks = webProject["blocks"];
    if (!webBlocks.is_array ()) webBlocks = nlohmann::json::array ();

    for (const BlockRecord& block : project.blocks) {
        auto blockIterator = std::find_if (
            webBlocks.begin (),
            webBlocks.end (),
            [&block] (const nlohmann::json& item) { return Text (item, "name") == block.name; }
        );

        if (blockIterator == webBlocks.end ()) {
            webBlocks.push_back ({
                {"name", block.name},
                {"zeroLevel", block.zeroLevel},
                {"subbasementLevel", block.subbasementLevel},
                {"floors", nlohmann::json::array ()},
                {"units", nlohmann::json::array ()}
            });
            blockIterator = std::prev (webBlocks.end ());
        }

        nlohmann::json& webFloors = (*blockIterator)["floors"];
        if (!webFloors.is_array ()) webFloors = nlohmann::json::array ();
        for (const FloorRecord& floor : block.floors) {
            auto floorIterator = std::find_if (
                webFloors.begin (),
                webFloors.end (),
                [&floor] (const nlohmann::json& item) {
                    if (!floor.archicadLinked) return false;
                    if (floor.archicadFloorId != 0 && static_cast<int> (Number (item, "archicadFloorId")) == floor.archicadFloorId) return true;
                    return item.contains ("archicadStoryIndex") && static_cast<int> (Number (item, "archicadStoryIndex")) == floor.archicadStoryIndex;
                }
            );

            if (floorIterator == webFloors.end ()) {
                floorIterator = std::find_if (
                    webFloors.begin (),
                    webFloors.end (),
                    [&floor] (const nlohmann::json& item) { return Text (item, "name") == floor.name; }
                );
            }

            if (floorIterator == webFloors.end ()) {
                webFloors.push_back ({
                    {"name", floor.name},
                    {"exempt30", floor.thirtyPercentAreas},
                    {"constructionValues", floor.constructionAreas},
                    {"emsalDisi", floor.emsalOutsideArea},
                    {"emsal", floor.emsalArea}
                });
                floorIterator = std::prev (webFloors.end ());
            }

            if (floor.archicadLinked) {
                (*floorIterator)["name"] = floor.name;
                (*floorIterator)["archicadStoryIndex"] = floor.archicadStoryIndex;
                (*floorIterator)["archicadFloorId"] = floor.archicadFloorId;
                (*floorIterator)["archicadLevel"] = floor.archicadLevel;
            }
            (*floorIterator)["exempt30"] = floor.thirtyPercentAreas;
            (*floorIterator)["constructionValues"] = floor.constructionAreas;
            (*floorIterator)["emsalDisi"] = floor.emsalOutsideArea;
            (*floorIterator)["emsal"] = floor.emsalArea;
            if (!floor.archicadZoneConstructionAreas.empty ())
                (*floorIterator)["archicadZoneConstructionAreas"] = floor.archicadZoneConstructionAreas;
            else
                floorIterator->erase ("archicadZoneConstructionAreas");
            if (floor.archicadZoneEmsalOutsideArea != 0.0)
                (*floorIterator)["archicadZoneEmsalOutsideArea"] = floor.archicadZoneEmsalOutsideArea;
            else
                floorIterator->erase ("archicadZoneEmsalOutsideArea");
            if (floor.archicadZoneEmsalArea != 0.0)
                (*floorIterator)["archicadZoneEmsalArea"] = floor.archicadZoneEmsalArea;
            else
                floorIterator->erase ("archicadZoneEmsalArea");
        }

        nlohmann::json& webUnits = (*blockIterator)["units"];
        if (!webUnits.is_array ()) webUnits = nlohmann::json::array ();
        for (const IndependentUnit& unit : block.units) {
            auto unitIterator = std::find_if (
                webUnits.begin (),
                webUnits.end (),
                [&unit] (const nlohmann::json& item) { return Text (item, "no") == unit.number; }
            );
            if (unitIterator == webUnits.end ()) {
                webUnits.push_back ({{"no", unit.number}});
                unitIterator = std::prev (webUnits.end ());
            }

            (*unitIterator)["floor"] = unit.floor;
            (*unitIterator)["quality"] = unit.quality;
            (*unitIterator)["owner"] = unit.owner;
            (*unitIterator)["landShare"] = unit.landShare;
            (*unitIterator)["roomCount"] = unit.roomCount;
            (*unitIterator)["grossArea"] = unit.grossArea;
            (*unitIterator)["netArea"] = unit.netArea;
            (*unitIterator)["extensionGrossArea"] = unit.extensionGrossArea;
            (*unitIterator)["extensionNetArea"] = unit.extensionNetArea;
            (*unitIterator)["balconyArea"] = unit.balconyArea;
            if (unit.archicadZoneLinked) {
                (*unitIterator)["archicadZoneLinked"] = true;
                (*unitIterator)["archicadZoneCount"] = unit.archicadZoneCount;
                (*unitIterator)["archicadZoneGuids"] = unit.archicadZoneGuids;
            }
        }
        std::stable_sort (webUnits.begin (), webUnits.end (), [] (const nlohmann::json& left, const nlohmann::json& right) {
            return NaturalUnitNumberLess (Text (left, "no"), Text (right, "no"));
        });
    }

    return envelope;
}

ProjectData LoadProject (const std::filesystem::path& path)
{
    std::ifstream input (path);
    if (!input) throw std::runtime_error ("JSON project file could not be opened.");
    const std::string text ((std::istreambuf_iterator<char> (input)), std::istreambuf_iterator<char> ());
    return DeserializeProjectText (text);
}

ProjectData DeserializeProjectText (const std::string& text)
{
    const nlohmann::json json = nlohmann::json::parse (text);
    if (json.contains ("project") && json.at ("project").is_object ())
        return LoadWebPanelProject (json);
    ProjectData project = json.get<ProjectData> ();
    project.sourceEnvelope = json;
    return project;
}

void SaveProject (const std::filesystem::path& path, const ProjectData& project)
{
    std::ofstream output (path);
    if (!output) throw std::runtime_error ("JSON project file could not be saved.");
    output << SerializeProjectText (project, true);
}

std::string SerializeProjectText (const ProjectData& project, bool preserveWebEnvelope)
{
    ProjectData orderedProject = project;
    SortIndependentUnits (orderedProject);
    const bool hasWebEnvelope = orderedProject.sourceEnvelope.contains ("project") && orderedProject.sourceEnvelope.at ("project").is_object ();
    return (preserveWebEnvelope && hasWebEnvelope ? MergeProjectIntoWebEnvelope (orderedProject) : nlohmann::json (orderedProject)).dump (2);
}

} // namespace RuhsatHesap
