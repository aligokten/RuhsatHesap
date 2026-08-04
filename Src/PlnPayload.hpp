#ifndef RUHSAT_HESAP_PLN_PAYLOAD_HPP
#define RUHSAT_HESAP_PLN_PAYLOAD_HPP

#include "ProjectData.hpp"

#include <string>

namespace RuhsatHesap {

constexpr int PlnStorageVersion = 1;

// Serializes every project field into a versioned UTF-8 JSON payload suitable
// for storing as opaque Add-On Object data in the Archicad project database.
std::string SerializeProjectForPln (const ProjectData& project);

// Throws std::runtime_error when the payload is malformed or belongs to an
// unsupported storage format.
ProjectData DeserializeProjectFromPln (const std::string& payload);

} // namespace RuhsatHesap

#endif
