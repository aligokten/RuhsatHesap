#include "StorySync.hpp"

#include <algorithm>

namespace RuhsatHesap {

std::size_t SyncStoriesToBlocks (ProjectData& project, bool createDefaultBlock)
{
    if (project.archicadStories.empty ()) return 0;

    if (project.blocks.empty () && createDefaultBlock) {
        BlockRecord defaultBlock;
        defaultBlock.name = "A";
        project.blocks.push_back (std::move (defaultBlock));
    }

    std::size_t addedFloorCount = 0;
    for (BlockRecord& block : project.blocks) {
        for (const ArchicadStory& story : project.archicadStories) {
            auto floorIterator = std::find_if (
                block.floors.begin (),
                block.floors.end (),
                [&story] (const FloorRecord& floor) {
                    if (!floor.archicadLinked) return false;
                    if (story.floorId != 0 && floor.archicadFloorId == story.floorId) return true;
                    return floor.archicadStoryIndex == story.index;
                }
            );

            if (floorIterator == block.floors.end ()) {
                floorIterator = std::find_if (
                    block.floors.begin (),
                    block.floors.end (),
                    [&story] (const FloorRecord& floor) { return floor.name == story.name; }
                );
            }

            if (floorIterator == block.floors.end ()) {
                FloorRecord floor;
                floor.name = story.name;
                floor.archicadLinked = true;
                floor.archicadStoryIndex = story.index;
                floor.archicadFloorId = story.floorId;
                floor.archicadLevel = story.level;
                block.floors.push_back (std::move (floor));
                ++addedFloorCount;
            } else {
                floorIterator->name = story.name;
                floorIterator->archicadLinked = true;
                floorIterator->archicadStoryIndex = story.index;
                floorIterator->archicadFloorId = story.floorId;
                floorIterator->archicadLevel = story.level;
            }
        }
    }

    return addedFloorCount;
}

} // namespace RuhsatHesap
