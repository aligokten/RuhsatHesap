#ifndef RUHSAT_HESAP_STORY_SYNC_HPP
#define RUHSAT_HESAP_STORY_SYNC_HPP

#include "ProjectData.hpp"

#include <cstddef>

namespace RuhsatHesap {

// Adds only missing Archicad stories to every block. Existing floor records and
// all manually entered area values remain untouched.
std::size_t SyncStoriesToBlocks (ProjectData& project, bool createDefaultBlock = true);

} // namespace RuhsatHesap

#endif
