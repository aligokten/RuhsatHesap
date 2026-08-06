#include "ProjectData.hpp"

#include <algorithm>
#include <cctype>

namespace RuhsatHesap {

namespace {

std::pair<std::size_t, std::size_t> TrimmedRange (const std::string& value)
{
    std::size_t begin = 0;
    while (begin < value.size () && std::isspace (static_cast<unsigned char> (value[begin])) != 0) ++begin;
    std::size_t end = value.size ();
    while (end > begin && std::isspace (static_cast<unsigned char> (value[end - 1])) != 0) --end;
    return {begin, end};
}

} // namespace

bool NaturalUnitNumberLess (const std::string& left, const std::string& right)
{
    const auto [leftBegin, leftEnd] = TrimmedRange (left);
    const auto [rightBegin, rightEnd] = TrimmedRange (right);
    std::size_t leftIndex = leftBegin;
    std::size_t rightIndex = rightBegin;
    int leadingZeroTieBreak = 0;

    while (leftIndex < leftEnd && rightIndex < rightEnd) {
        const bool leftDigit = std::isdigit (static_cast<unsigned char> (left[leftIndex])) != 0;
        const bool rightDigit = std::isdigit (static_cast<unsigned char> (right[rightIndex])) != 0;

        if (leftDigit && rightDigit) {
            std::size_t leftRunEnd = leftIndex;
            std::size_t rightRunEnd = rightIndex;
            while (leftRunEnd < leftEnd && std::isdigit (static_cast<unsigned char> (left[leftRunEnd])) != 0) ++leftRunEnd;
            while (rightRunEnd < rightEnd && std::isdigit (static_cast<unsigned char> (right[rightRunEnd])) != 0) ++rightRunEnd;

            std::size_t leftSignificant = leftIndex;
            std::size_t rightSignificant = rightIndex;
            while (leftSignificant < leftRunEnd && left[leftSignificant] == '0') ++leftSignificant;
            while (rightSignificant < rightRunEnd && right[rightSignificant] == '0') ++rightSignificant;

            const std::size_t leftDigits = leftRunEnd - leftSignificant;
            const std::size_t rightDigits = rightRunEnd - rightSignificant;
            if (leftDigits != rightDigits) return leftDigits < rightDigits;
            for (std::size_t offset = 0; offset < leftDigits; ++offset) {
                if (left[leftSignificant + offset] != right[rightSignificant + offset])
                    return left[leftSignificant + offset] < right[rightSignificant + offset];
            }

            if (leadingZeroTieBreak == 0 && (leftRunEnd - leftIndex) != (rightRunEnd - rightIndex))
                leadingZeroTieBreak = (leftRunEnd - leftIndex) < (rightRunEnd - rightIndex) ? -1 : 1;
            leftIndex = leftRunEnd;
            rightIndex = rightRunEnd;
            continue;
        }

        if (leftDigit != rightDigit) return leftDigit;
        const unsigned char leftCharacter = static_cast<unsigned char> (std::toupper (static_cast<unsigned char> (left[leftIndex])));
        const unsigned char rightCharacter = static_cast<unsigned char> (std::toupper (static_cast<unsigned char> (right[rightIndex])));
        if (leftCharacter != rightCharacter) return leftCharacter < rightCharacter;
        ++leftIndex;
        ++rightIndex;
    }

    if (leftIndex != leftEnd || rightIndex != rightEnd) return leftIndex == leftEnd;
    if (leadingZeroTieBreak != 0) return leadingZeroTieBreak < 0;
    return left < right;
}

void SortIndependentUnits (BlockRecord& block)
{
    std::stable_sort (block.units.begin (), block.units.end (), [] (const IndependentUnit& left, const IndependentUnit& right) {
        return NaturalUnitNumberLess (left.number, right.number);
    });
}

void SortIndependentUnits (ProjectData& project)
{
    for (BlockRecord& block : project.blocks) SortIndependentUnits (block);
}

template<class T>
static void ReadOptional (const nlohmann::json& json, const char* key, T& value)
{
    if (json.contains (key) && !json.at (key).is_null ())
        json.at (key).get_to (value);
}

void to_json (nlohmann::json& j, const ParcelInfo& v) { j = {{"projectName",v.projectName},{"city",v.city},{"district",v.district},{"neighborhood",v.neighborhood},{"block",v.block},{"parcel",v.parcel},{"parcelArea",v.parcelArea},{"taksRate",v.taksRate},{"kaksRate",v.kaksRate},{"directEmsal",v.directEmsal},{"buildingFootprint",v.buildingFootprint},{"emsalMethod",v.emsalMethod}}; }
void from_json (const nlohmann::json& j, ParcelInfo& v) { ReadOptional(j,"projectName",v.projectName); ReadOptional(j,"city",v.city); ReadOptional(j,"district",v.district); ReadOptional(j,"neighborhood",v.neighborhood); ReadOptional(j,"block",v.block); ReadOptional(j,"parcel",v.parcel); ReadOptional(j,"parcelArea",v.parcelArea); ReadOptional(j,"taksRate",v.taksRate); ReadOptional(j,"kaksRate",v.kaksRate); ReadOptional(j,"directEmsal",v.directEmsal); ReadOptional(j,"buildingFootprint",v.buildingFootprint); ReadOptional(j,"emsalMethod",v.emsalMethod); }
void to_json (nlohmann::json& j, const IndependentUnit& v)
{
    j={{"number",v.number},{"floor",v.floor},{"quality",v.quality},{"owner",v.owner},{"landShare",v.landShare},{"roomCount",v.roomCount},{"grossArea",v.grossArea},{"netArea",v.netArea},{"extensionGrossArea",v.extensionGrossArea},{"extensionNetArea",v.extensionNetArea},{"balconyArea",v.balconyArea}};
    if (v.archicadZoneLinked) {
        j["archicadZoneLinked"] = true;
        j["archicadZoneCount"] = v.archicadZoneCount;
        j["archicadZoneGuids"] = v.archicadZoneGuids;
    }
}
void from_json (const nlohmann::json& j, IndependentUnit& v)
{
    ReadOptional(j,"number",v.number); ReadOptional(j,"floor",v.floor); ReadOptional(j,"quality",v.quality); ReadOptional(j,"owner",v.owner); ReadOptional(j,"landShare",v.landShare); ReadOptional(j,"roomCount",v.roomCount); ReadOptional(j,"grossArea",v.grossArea); ReadOptional(j,"netArea",v.netArea); ReadOptional(j,"extensionGrossArea",v.extensionGrossArea); ReadOptional(j,"extensionNetArea",v.extensionNetArea); ReadOptional(j,"balconyArea",v.balconyArea); ReadOptional(j,"archicadZoneLinked",v.archicadZoneLinked); ReadOptional(j,"archicadZoneCount",v.archicadZoneCount); ReadOptional(j,"archicadZoneGuids",v.archicadZoneGuids);
}
void to_json (nlohmann::json& j, const FloorRecord& v)
{
    j={{"name",v.name},{"thirtyPercentAreas",v.thirtyPercentAreas},{"constructionAreas",v.constructionAreas},{"emsalOutsideArea",v.emsalOutsideArea},{"emsalArea",v.emsalArea}};
    if (!v.archicadZoneConstructionAreas.empty ()) j["archicadZoneConstructionAreas"] = v.archicadZoneConstructionAreas;
    if (!v.archicadZoneThirtyPercentAreas.empty ()) j["archicadZoneThirtyPercentAreas"] = v.archicadZoneThirtyPercentAreas;
    if (v.archicadZoneEmsalOutsideArea != 0.0) j["archicadZoneEmsalOutsideArea"] = v.archicadZoneEmsalOutsideArea;
    if (v.archicadZoneEmsalArea != 0.0) j["archicadZoneEmsalArea"] = v.archicadZoneEmsalArea;
    if (v.archicadLinked) {
        j["archicadStoryIndex"] = v.archicadStoryIndex;
        j["archicadFloorId"] = v.archicadFloorId;
        j["archicadLevel"] = v.archicadLevel;
    }
}
void from_json (const nlohmann::json& j, FloorRecord& v)
{
    ReadOptional(j,"name",v.name); ReadOptional(j,"thirtyPercentAreas",v.thirtyPercentAreas); ReadOptional(j,"constructionAreas",v.constructionAreas); ReadOptional(j,"emsalOutsideArea",v.emsalOutsideArea); ReadOptional(j,"emsalArea",v.emsalArea);
    ReadOptional(j,"archicadZoneConstructionAreas",v.archicadZoneConstructionAreas); ReadOptional(j,"archicadZoneThirtyPercentAreas",v.archicadZoneThirtyPercentAreas); ReadOptional(j,"archicadZoneEmsalOutsideArea",v.archicadZoneEmsalOutsideArea); ReadOptional(j,"archicadZoneEmsalArea",v.archicadZoneEmsalArea);
    v.archicadLinked = j.contains ("archicadStoryIndex") || j.contains ("archicadFloorId");
    ReadOptional(j,"archicadStoryIndex",v.archicadStoryIndex); ReadOptional(j,"archicadFloorId",v.archicadFloorId); ReadOptional(j,"archicadLevel",v.archicadLevel);
}
void to_json (nlohmann::json& j, const ArchicadStory& v) { j={{"index",v.index},{"floorId",v.floorId},{"name",v.name},{"level",v.level}}; }
void from_json (const nlohmann::json& j, ArchicadStory& v) { ReadOptional(j,"index",v.index); ReadOptional(j,"floorId",v.floorId); ReadOptional(j,"name",v.name); ReadOptional(j,"level",v.level); }
void to_json (nlohmann::json& j, const BlockRecord& v) { j={{"name",v.name},{"zeroLevel",v.zeroLevel},{"subbasementLevel",v.subbasementLevel},{"floors",v.floors},{"units",v.units}}; }
void from_json (const nlohmann::json& j, BlockRecord& v) { ReadOptional(j,"name",v.name); ReadOptional(j,"zeroLevel",v.zeroLevel); ReadOptional(j,"subbasementLevel",v.subbasementLevel); ReadOptional(j,"floors",v.floors); ReadOptional(j,"units",v.units); SortIndependentUnits(v); }
void to_json (nlohmann::json& j, const RetainingWall& v) { j={{"name",v.name},{"area",v.area}}; }
void from_json (const nlohmann::json& j, RetainingWall& v) { ReadOptional(j,"name",v.name); ReadOptional(j,"area",v.area); }
void to_json (nlohmann::json& j, const ProjectData& v) { j={{"format","ruhsat-hesap-archicad"},{"schemaVersion",v.schemaVersion},{"parcel",v.parcel},{"archicadStories",v.archicadStories},{"blocks",v.blocks},{"retainingWalls",v.retainingWalls},{"providedParkingSpaces",v.providedParkingSpaces},{"auxiliaryData",v.auxiliaryData}}; }
void from_json (const nlohmann::json& j, ProjectData& v) { ReadOptional(j,"schemaVersion",v.schemaVersion); ReadOptional(j,"parcel",v.parcel); ReadOptional(j,"archicadStories",v.archicadStories); ReadOptional(j,"blocks",v.blocks); ReadOptional(j,"retainingWalls",v.retainingWalls); ReadOptional(j,"providedParkingSpaces",v.providedParkingSpaces); ReadOptional(j,"auxiliaryData",v.auxiliaryData); SortIndependentUnits(v); }

} // namespace RuhsatHesap
