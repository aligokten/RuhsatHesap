#ifndef RUHSAT_HESAP_PROJECT_STORE_HPP
#define RUHSAT_HESAP_PROJECT_STORE_HPP

#include "ProjectData.hpp"

#include <filesystem>
#include <string>

namespace RuhsatHesap {

ProjectData LoadProject (const std::filesystem::path& path);
void SaveProject (const std::filesystem::path& path, const ProjectData& project);
ProjectData DeserializeProjectText (const std::string& text);
std::string SerializeProjectText (const ProjectData& project, bool preserveWebEnvelope = false);

} // namespace RuhsatHesap

#endif
