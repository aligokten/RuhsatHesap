#ifndef RUHSAT_HESAP_PLN_PROJECT_STORE_HPP
#define RUHSAT_HESAP_PLN_PROJECT_STORE_HPP

#include "ProjectData.hpp"

#include <string>

namespace RuhsatHesap {

// Stable name of the single project-level Add-On Object stored in PLN/PLA.
constexpr const char* PlnProjectObjectName = "RuhsatHesap.ProjectData.v1";

struct PlnLoadResult {
    bool success = false;
    bool found = false;
    std::string error;
};

struct PlnSaveResult {
    bool success = false;
    bool created = false;
    std::string error;
};

PlnLoadResult LoadProjectFromPln (ProjectData& project);
PlnSaveResult SaveProjectToPln (const ProjectData& project);

} // namespace RuhsatHesap

#endif
