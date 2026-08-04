#ifndef RUHSAT_HESAP_REPORT_EXPORT_HPP
#define RUHSAT_HESAP_REPORT_EXPORT_HPP

#include "ProjectData.hpp"

#include <filesystem>

namespace RuhsatHesap {

struct ReportExportPaths {
    std::filesystem::path excelPath;
    std::filesystem::path sheetPngPath;
};

void WriteExcelReport (const std::filesystem::path& path, const ProjectData& project);
void WriteSheetPng (const std::filesystem::path& path, const ProjectData& project);
ReportExportPaths WriteProjectReports (const std::filesystem::path& folder, const ProjectData& project);

} // namespace RuhsatHesap

#endif
