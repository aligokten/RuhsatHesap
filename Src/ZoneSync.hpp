#ifndef RUHSAT_HESAP_ZONE_SYNC_HPP
#define RUHSAT_HESAP_ZONE_SYNC_HPP

#include "ProjectData.hpp"

#include <string>
#include <vector>

namespace RuhsatHesap {

enum class ZoneAreaType {
    Net,
    Gross,
    ExtensionNet,
    ExtensionGross,
    Balcony,
    Common,
    Stair,
    Hall,
    Emsal,
    EmsalOutside,
    Shelter,
    Unknown
};

struct ParsedZoneName {
    bool ruhsatZone = false;
    bool valid = false;
    std::string blockName;
    std::string unitNumber;
    std::string roomName;
    std::string quality;
    ZoneAreaType areaType = ZoneAreaType::Unknown;
    int roomCount = 0;
    std::string error;
};

struct ZoneObservation {
    std::string zoneName;
    std::string zoneNumber;
    std::string storyName;
    int storyIndex = 0;
    double area = 0.0;
    std::string guid;
};

struct ZoneSyncResult {
    std::size_t scannedZones = 0;
    std::size_t recognizedZones = 0;
    std::size_t ignoredZones = 0;
    std::size_t invalidZones = 0;
    std::size_t createdBlocks = 0;
    std::size_t createdUnits = 0;
    std::size_t updatedUnits = 0;
    std::size_t updatedFloorAreas = 0;
};

// Recommended long form:
// RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN
// Compact aliases B, T, O, M and N are also accepted. If BB is omitted,
// Archicad's Zone Number is used as the independent-unit number.
ParsedZoneName ParseZoneName (const std::string& zoneName);

// Rebuilds values sourced from RH zones. Re-running with the same observations
// is idempotent: areas are recalculated, not added to previous imports.
ZoneSyncResult SyncZonesToProject (ProjectData& project, const std::vector<ZoneObservation>& observations);

} // namespace RuhsatHesap

#endif
