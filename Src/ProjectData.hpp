#ifndef RUHSAT_HESAP_PROJECT_DATA_HPP
#define RUHSAT_HESAP_PROJECT_DATA_HPP

#include <map>
#include <string>
#include <vector>

#include <nlohmann/json.hpp>

namespace RuhsatHesap {

struct ParcelInfo {
    std::string projectName;
    std::string city;
    std::string district;
    std::string neighborhood;
    std::string block;
    std::string parcel;
    double parcelArea = 0.0;
    double taksRate = 0.0;
    double kaksRate = 0.0;
    double directEmsal = 0.0;
    double buildingFootprint = 0.0;
    std::string emsalMethod = "kaks";
};

struct IndependentUnit {
    std::string number;
    std::string floor;
    std::string quality;
    std::string owner;
    std::string landShare;
    int roomCount = 0;
    double grossArea = 0.0;
    double netArea = 0.0;
    double extensionGrossArea = 0.0;
    double extensionNetArea = 0.0;
    double balconyArea = 0.0;
    bool archicadZoneLinked = false;
    int archicadZoneCount = 0;
    std::vector<std::string> archicadZoneGuids;
};

struct FloorRecord {
    std::string name;
    std::map<std::string, double> thirtyPercentAreas;
    std::map<std::string, double> constructionAreas;
    double emsalOutsideArea = 0.0;
    double emsalArea = 0.0;
    // Values imported from Archicad zones are tracked separately so a
    // repeated import can replace only its own contribution and preserve
    // any manual adjustment made in the panel.
    std::map<std::string, double> archicadZoneConstructionAreas;
    std::map<std::string, double> archicadZoneThirtyPercentAreas;
    double archicadZoneEmsalOutsideArea = 0.0;
    double archicadZoneEmsalArea = 0.0;
    bool archicadLinked = false;
    int archicadStoryIndex = 0;
    int archicadFloorId = 0;
    double archicadLevel = 0.0;
};

struct ArchicadStory {
    int index = 0;
    int floorId = 0;
    std::string name;
    double level = 0.0;
};

struct BlockRecord {
    std::string name;
    std::string zeroLevel;
    std::string subbasementLevel;
    std::vector<FloorRecord> floors;
    std::vector<IndependentUnit> units;
};

struct RetainingWall {
    std::string name;
    double area = 0.0;
};

struct ProjectData {
    int schemaVersion = 5;
    ParcelInfo parcel;
    std::vector<ArchicadStory> archicadStories;
    std::vector<BlockRecord> blocks;
    std::vector<RetainingWall> retainingWalls;
    int providedParkingSpaces = 0;
    nlohmann::json auxiliaryData = nlohmann::json::object ();
    nlohmann::json sourceEnvelope;
};

void to_json (nlohmann::json& json, const ParcelInfo& value);
void from_json (const nlohmann::json& json, ParcelInfo& value);
void to_json (nlohmann::json& json, const IndependentUnit& value);
void from_json (const nlohmann::json& json, IndependentUnit& value);
void to_json (nlohmann::json& json, const FloorRecord& value);
void from_json (const nlohmann::json& json, FloorRecord& value);
void to_json (nlohmann::json& json, const ArchicadStory& value);
void from_json (const nlohmann::json& json, ArchicadStory& value);
void to_json (nlohmann::json& json, const BlockRecord& value);
void from_json (const nlohmann::json& json, BlockRecord& value);
void to_json (nlohmann::json& json, const RetainingWall& value);
void from_json (const nlohmann::json& json, RetainingWall& value);
void to_json (nlohmann::json& json, const ProjectData& value);
void from_json (const nlohmann::json& json, ProjectData& value);

// Compares independent-unit identifiers in human/natural order.  Numeric
// runs are compared by value, so 7, 8, 9, 10 is preferred over the lexical
// 10, 7, 8, 9 order.  Alphanumeric identifiers such as A2/A10 are supported.
bool NaturalUnitNumberLess (const std::string& left, const std::string& right);
void SortIndependentUnits (BlockRecord& block);
void SortIndependentUnits (ProjectData& project);

} // namespace RuhsatHesap

#endif
