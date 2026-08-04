#include "CalculationEngine.hpp"

#include <algorithm>
#include <cmath>

namespace RuhsatHesap {

static double SumValues (const std::map<std::string, double>& values)
{
    double total = 0.0;
    for (const auto& [name, value] : values) {
        (void) name;
        total += value;
    }
    return total;
}

static double ParkingContribution (double grossArea)
{
    if (grossArea <= 0.0) return 0.0;
    if (grossArea < 80.0) return 1.0 / 3.0;
    if (grossArea < 120.0) return 1.0 / 2.0;
    if (grossArea < 180.0) return 1.0;
    return 2.0;
}

CalculationSummary Calculate (const ProjectData& project)
{
    CalculationSummary result;
    result.maxFootprint = project.parcel.parcelArea * project.parcel.taksRate;
    result.maxEmsal = project.parcel.emsalMethod == "direct"
        ? project.parcel.directEmsal
        : project.parcel.parcelArea * project.parcel.kaksRate;

    double rawParking = 0.0;
    for (const BlockRecord& block : project.blocks) {
        for (const FloorRecord& floor : block.floors) {
            result.calculatedEmsal += floor.emsalArea;
            result.constructionArea += SumValues (floor.constructionAreas);
            for (const IndependentUnit& unit : block.units) {
                if (unit.floor == floor.name)
                    result.constructionArea += unit.grossArea;
            }
        }
        for (const IndependentUnit& unit : block.units) {
            ++result.unitCount;
            rawParking += ParkingContribution (unit.grossArea);
        }
    }

    for (const RetainingWall& wall : project.retainingWalls)
        result.retainingWallArea += wall.area;

    result.emsalExcess = std::max (0.0, result.calculatedEmsal - result.maxEmsal);
    result.constructionGrandTotal = result.constructionArea + result.retainingWallArea;
    const double gardenArea = std::max (0.0, project.parcel.parcelArea - project.parcel.buildingFootprint);
    result.requiredTrees = project.parcel.parcelArea > 0.0 ? static_cast<int> (std::ceil (gardenArea / 30.0)) : 0;
    result.requiredParkingSpaces = static_cast<int> (std::ceil (rawParking));
    return result;
}

} // namespace RuhsatHesap
