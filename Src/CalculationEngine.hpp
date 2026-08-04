#ifndef RUHSAT_HESAP_CALCULATION_ENGINE_HPP
#define RUHSAT_HESAP_CALCULATION_ENGINE_HPP

#include "ProjectData.hpp"

namespace RuhsatHesap {

struct CalculationSummary {
    double maxFootprint = 0.0;
    double maxEmsal = 0.0;
    double calculatedEmsal = 0.0;
    double emsalExcess = 0.0;
    double constructionArea = 0.0;
    double retainingWallArea = 0.0;
    double constructionGrandTotal = 0.0;
    int requiredTrees = 0;
    int requiredParkingSpaces = 0;
    int unitCount = 0;
};

CalculationSummary Calculate (const ProjectData& project);

} // namespace RuhsatHesap

#endif
