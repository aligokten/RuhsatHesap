cmake_minimum_required (VERSION 3.19)

include (Tools/CMakeCommon.cmake)

set_property (GLOBAL PROPERTY USE_FOLDERS ON)
set (AC_API_DEVKIT_DIR "$ENV{AC_API_DEVKIT_DIR}" CACHE PATH "API DevKit directory.")

if (NOT DEFINED AC_VERSION)
    message (FATAL_ERROR "AC_VERSION is not set!")
endif ()

message (STATUS "Archicad Version: ${AC_VERSION}")
SetGlobalCompilerDefinitions (${AC_VERSION})

ReadConfigJson ()
set (AC_ADDON_LANGUAGE "${addOnDefaultLanguage}" CACHE STRING "Add-On language code.")

project (${addOnName}-${AC_VERSION})

set (AC_ADDON_FOR_DISTRIBUTION OFF CACHE BOOL "")

# Tools/VersionInfo.rc.in needs a VERSIONINFO language/charset pair. Tools/BuildAddOn.py
# computes and passes these, but a direct "cmake -B Build ..." invocation does not, and
# the generated VersionInfo.rc then contains `BLOCK ""` and `VALUE "Translation", ,` --
# which rc.exe rejects, so the .apx never gets linked. Default to the same values
# BuildAddOn.py uses for the INT language (0409 = US English, 04b0 = 1200 = Unicode).
if (WIN32)
    if (NOT DEFINED AC_WIN_LANGCHARSET OR AC_WIN_LANGCHARSET STREQUAL "")
        set (AC_WIN_LANGCHARSET "040904b0")
    endif ()
    if (NOT DEFINED AC_WIN_LANGUAGEID OR AC_WIN_LANGUAGEID STREQUAL "")
        set (AC_WIN_LANGUAGEID 1033)
    endif ()
    if (NOT DEFINED AC_WIN_CHARSETID OR AC_WIN_CHARSETID STREQUAL "")
        set (AC_WIN_CHARSETID 1200)
    endif ()
endif ()

# Upstream compiles with /W4 /WX. The GitHub workflow only covers Debug and
# RelWithDebInfo, so a Release-only warning can break a local Release build on a
# warning that CI never sees. Keep the strict default, but allow turning it off.
option (AC_ADDON_WARNINGS_AS_ERRORS "Treat compiler warnings as errors (/WX)." ON)

set (AddOnSourcesFolder Src)
set (AddOnResourcesFolder .)

set (PCH_HEADER "Src/ExamplePrecompiledHeader.hpp")

# Visual Studio generators can omit a quoted empty custom-command argument.
# Keep the optional LP_XML converter position populated even when the DevKit
# does not ship that legacy tool; CompileResources.py will ignore a missing
# converter folder after parsing the remaining resource paths correctly.
if (NOT DEFINED LP_XML_CONVERTER_FOLDER OR LP_XML_CONVERTER_FOLDER STREQUAL "")
    set (LP_XML_CONVERTER_FOLDER "${AC_API_DEVKIT_DIR}/Tools/LP_XMLConverter")
endif ()

GenerateAddOnProject (CMakeTarget ${AC_VERSION} "${AC_API_DEVKIT_DIR}" ${AddOnSourcesFolder} ${AddOnResourcesFolder} ${AC_ADDON_LANGUAGE} ${addOnDefaultLanguage} ${PCH_HEADER})

target_include_directories (CMakeTarget PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/ThirdParty/json/single_include"
)

if (WIN32)
    target_link_libraries (CMakeTarget comdlg32 shell32 ole32 gdiplus)
endif ()

# SetCompilerOptions() appended /W4 /WX inside GenerateAddOnProject; a later /WX-
# wins on the command line without disturbing the warning level.
if (MSVC AND NOT AC_ADDON_WARNINGS_AS_ERRORS)
    target_compile_options (CMakeTarget PRIVATE /WX-)
endif ()
