#include "CalculationEngine.hpp"
#include "ProjectStore.hpp"
#include "ReportExport.hpp"
#include "PlnPayload.hpp"
#include "StorySync.hpp"
#include "ZoneSync.hpp"

#include <cassert>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <iostream>

int main ()
{
    RuhsatHesap::ProjectData project;
    project.parcel.parcelArea = 1410.18;
    project.parcel.taksRate = 0.40;
    project.parcel.kaksRate = 0.80;
    project.parcel.buildingFootprint = 491.0;
    project.archicadStories = {
        {-1, 101, "Bodrum Kat", -3.20},
        {0, 102, "Zemin Kat", 0.00},
        {1, 103, "1. Kat", 3.20}
    };

    RuhsatHesap::BlockRecord block;
    block.name = "A";
    RuhsatHesap::FloorRecord baseFloor;
    baseFloor.name = "Zemin";
    baseFloor.constructionAreas = {{"bagimsiz_bolum_brut", 211.06}, {"merdiven", 11.06}};
    baseFloor.emsalArea = 210.0;
    block.floors.push_back (std::move (baseFloor));
    RuhsatHesap::IndependentUnit firstUnit;
    firstUnit.number = "1"; firstUnit.floor = "Zemin"; firstUnit.quality = "Mesken"; firstUnit.landShare = "1/10";
    firstUnit.roomCount = 3; firstUnit.grossArea = 79.0; firstUnit.netArea = 60.0;
    block.units.push_back (firstUnit);
    RuhsatHesap::IndependentUnit secondUnit;
    secondUnit.number = "2"; secondUnit.floor = "Zemin"; secondUnit.quality = "Mesken"; secondUnit.landShare = "1/10";
    secondUnit.roomCount = 3; secondUnit.grossArea = 130.0; secondUnit.netArea = 100.0;
    block.units.push_back (secondUnit);
    project.blocks.push_back (block);
    const std::size_t addedFloors = RuhsatHesap::SyncStoriesToBlocks (project);
    assert (addedFloors == 3);
    assert (project.blocks.front ().floors.size () == 4);
    assert (RuhsatHesap::SyncStoriesToBlocks (project) == 0);
    project.archicadStories.back ().name = "Birinci Kat";
    project.archicadStories.back ().level = 3.25;
    assert (RuhsatHesap::SyncStoriesToBlocks (project) == 0);
    const auto renamedFloor = std::find_if (
        project.blocks.front ().floors.begin (),
        project.blocks.front ().floors.end (),
        [] (const RuhsatHesap::FloorRecord& floor) { return floor.archicadFloorId == 103; }
    );
    assert (renamedFloor != project.blocks.front ().floors.end ());
    assert (renamedFloor->name == "Birinci Kat");
    assert (std::abs (renamedFloor->archicadLevel - 3.25) < 0.001);

    const auto parsedLongName = RuhsatHesap::ParseZoneName ("RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN");
    assert (parsedLongName.valid);
    assert (parsedLongName.blockName == "A");
    assert (parsedLongName.unitNumber == "01");
    assert (parsedLongName.areaType == RuhsatHesap::ZoneAreaType::Net);
    assert (parsedLongName.roomCount == 3);
    assert (parsedLongName.quality == "MESKEN");
    const auto parsedCompactName = RuhsatHesap::ParseZoneName ("rh|b:b|bb:12|t:brüt|o:2");
    assert (parsedCompactName.valid);
    assert (parsedCompactName.blockName == "B");
    assert (parsedCompactName.areaType == RuhsatHesap::ZoneAreaType::Gross);
    assert (RuhsatHesap::ParseZoneName ("RH|BLOK=A|TIP=ORTAK").areaType == RuhsatHesap::ZoneAreaType::Common);
    assert (RuhsatHesap::ParseZoneName ("RH|BLOK=A|TIP=MERDIVEN").areaType == RuhsatHesap::ZoneAreaType::Stair);
    assert (RuhsatHesap::ParseZoneName ("RH|BLOK=A|TIP=HOL").areaType == RuhsatHesap::ZoneAreaType::Hall);
    assert (RuhsatHesap::ParseZoneName ("RH|BLOK=A|TIP=EMSAL").areaType == RuhsatHesap::ZoneAreaType::Emsal);
    assert (RuhsatHesap::ParseZoneName ("RH|BLOK=A|TIP=EMSAL_DISI").areaType == RuhsatHesap::ZoneAreaType::EmsalOutside);
    assert (RuhsatHesap::ParseZoneName ("RH|BLOK=A|TIP=SIĞINAK").areaType == RuhsatHesap::ZoneAreaType::Shelter);
    assert (!RuhsatHesap::ParseZoneName ("SALON").ruhsatZone);

    RuhsatHesap::ProjectData extendedZoneProject;
    extendedZoneProject.auxiliaryData["commonArea"] = 4.0;
    extendedZoneProject.auxiliaryData["shelter"]["providedArea"] = 5.0;
    RuhsatHesap::BlockRecord extendedBlock;
    extendedBlock.name = "A";
    RuhsatHesap::FloorRecord extendedFloor;
    extendedFloor.name = "Zemin Kat";
    extendedFloor.archicadLinked = true;
    extendedFloor.archicadStoryIndex = 0;
    extendedFloor.constructionAreas["merdiven"] = 1.0;
    extendedFloor.emsalArea = 2.0;
    extendedFloor.emsalOutsideArea = 3.0;
    extendedBlock.floors.push_back (extendedFloor);
    extendedZoneProject.blocks.push_back (extendedBlock);
    const std::vector<RuhsatHesap::ZoneObservation> extendedZones = {
        {"RH|BLOK=A|TIP=ORTAK", "", "Zemin Kat", 0, 100.0, "extended-1"},
        {"RH|BLOK=A|TIP=MERDIVEN", "", "Zemin Kat", 0, 10.0, "extended-2"},
        {"RH|BLOK=A|TIP=HOL", "", "Zemin Kat", 0, 20.0, "extended-3"},
        {"RH|BLOK=A|TIP=EMSAL", "", "Zemin Kat", 0, 200.0, "extended-4"},
        {"RH|BLOK=A|TIP=EMSAL_DISI", "", "Zemin Kat", 0, 30.0, "extended-5"},
        {"RH|BLOK=A|TIP=SIGINAK", "", "Zemin Kat", 0, 40.0, "extended-6"}
    };
    const auto extendedSync = RuhsatHesap::SyncZonesToProject (extendedZoneProject, extendedZones);
    assert (extendedSync.recognizedZones == 6);
    assert (extendedSync.invalidZones == 0);
    assert (extendedSync.updatedFloorAreas == 1);
    const auto& importedFloor = extendedZoneProject.blocks.front ().floors.front ();
    assert (std::abs (importedFloor.constructionAreas.at ("merdiven") - 11.0) < 0.001);
    assert (std::abs (importedFloor.constructionAreas.at ("hol") - 20.0) < 0.001);
    assert (std::abs (importedFloor.constructionAreas.at ("siginak") - 40.0) < 0.001);
    assert (std::abs (importedFloor.emsalArea - 202.0) < 0.001);
    assert (std::abs (importedFloor.emsalOutsideArea - 33.0) < 0.001);
    assert (std::abs (extendedZoneProject.auxiliaryData["commonArea"].get<double> () - 104.0) < 0.001);
    assert (std::abs (extendedZoneProject.auxiliaryData["shelter"]["providedArea"].get<double> () - 45.0) < 0.001);

    RuhsatHesap::SyncZonesToProject (extendedZoneProject, extendedZones);
    assert (std::abs (extendedZoneProject.blocks.front ().floors.front ().constructionAreas.at ("merdiven") - 11.0) < 0.001);
    assert (std::abs (extendedZoneProject.blocks.front ().floors.front ().emsalArea - 202.0) < 0.001);
    assert (std::abs (extendedZoneProject.auxiliaryData["commonArea"].get<double> () - 104.0) < 0.001);
    RuhsatHesap::SyncZonesToProject (extendedZoneProject, {});
    const auto& clearedExtendedFloor = extendedZoneProject.blocks.front ().floors.front ();
    assert (std::abs (clearedExtendedFloor.constructionAreas.at ("merdiven") - 1.0) < 0.001);
    assert (clearedExtendedFloor.constructionAreas.find ("hol") == clearedExtendedFloor.constructionAreas.end ());
    assert (clearedExtendedFloor.constructionAreas.find ("siginak") == clearedExtendedFloor.constructionAreas.end ());
    assert (std::abs (clearedExtendedFloor.emsalArea - 2.0) < 0.001);
    assert (std::abs (clearedExtendedFloor.emsalOutsideArea - 3.0) < 0.001);
    assert (std::abs (extendedZoneProject.auxiliaryData["commonArea"].get<double> () - 4.0) < 0.001);
    assert (std::abs (extendedZoneProject.auxiliaryData["shelter"]["providedArea"].get<double> () - 5.0) < 0.001);

    RuhsatHesap::BlockRecord naturalOrderBlock;
    naturalOrderBlock.name = "SIRALAMA";
    for (const char* number : {"10", "7", "9", "8", "A10", "A2", "01"}) {
        RuhsatHesap::IndependentUnit unit;
        unit.number = number;
        naturalOrderBlock.units.push_back (std::move (unit));
    }
    RuhsatHesap::SortIndependentUnits (naturalOrderBlock);
    const std::vector<std::string> expectedNaturalOrder = {"01", "7", "8", "9", "10", "A2", "A10"};
    for (std::size_t index = 0; index < expectedNaturalOrder.size (); ++index)
        assert (naturalOrderBlock.units[index].number == expectedNaturalOrder[index]);

    RuhsatHesap::ProjectData zoneOrderProject;
    const std::vector<RuhsatHesap::ZoneObservation> unorderedZones = {
        {"RH|BLOK=A|BB=7|TIP=BRUT", "", "Zemin", 0, 47.0, "order-7"},
        {"RH|BLOK=A|BB=10|TIP=BRUT", "", "Zemin", 0, 50.0, "order-10"},
        {"RH|BLOK=A|BB=8|TIP=BRUT", "", "Zemin", 0, 48.0, "order-8"},
        {"RH|BLOK=A|BB=9|TIP=BRUT", "", "Zemin", 0, 49.0, "order-9"}
    };
    RuhsatHesap::SyncZonesToProject (zoneOrderProject, unorderedZones);
    assert (zoneOrderProject.blocks.size () == 1);
    assert (zoneOrderProject.blocks.front ().units.size () == 4);
    assert (zoneOrderProject.blocks.front ().units[0].number == "7");
    assert (zoneOrderProject.blocks.front ().units[1].number == "8");
    assert (zoneOrderProject.blocks.front ().units[2].number == "9");
    assert (zoneOrderProject.blocks.front ().units[3].number == "10");

    const nlohmann::json unorderedWebEnvelope = {
        {"project", {
            {"data", nlohmann::json::object ()},
            {"blocks", nlohmann::json::array ({
                {{"name", "A"}, {"floors", nlohmann::json::array ()}, {"units", nlohmann::json::array ({
                    {{"no", "10"}}, {{"no", "7"}}, {{"no", "9"}}, {{"no", "8"}}
                })}}
            })}
        }}
    };
    const auto orderedWebProject = RuhsatHesap::DeserializeProjectText (unorderedWebEnvelope.dump ());
    assert (orderedWebProject.blocks.front ().units[0].number == "7");
    assert (orderedWebProject.blocks.front ().units[1].number == "8");
    assert (orderedWebProject.blocks.front ().units[2].number == "9");
    assert (orderedWebProject.blocks.front ().units[3].number == "10");
    const auto orderedWebJson = nlohmann::json::parse (RuhsatHesap::SerializeProjectText (orderedWebProject, true));
    assert (orderedWebJson["project"]["blocks"][0]["units"][0]["no"] == "7");
    assert (orderedWebJson["project"]["blocks"][0]["units"][3]["no"] == "10");

    const std::vector<RuhsatHesap::ZoneObservation> zones = {
        {"RH|BLOK=B|BB=12|TIP=NET|ODA=2|MAHAL=SALON|NITELIK=MESKEN", "", "Zemin Kat", 0, 24.50, "zone-1"},
        {"RH|BLOK=B|BB=12|TIP=NET|ODA=2|MAHAL=ODA", "", "Zemin Kat", 0, 13.25, "zone-2"},
        {"RH|BLOK=B|BB=12|TIP=BRUT|ODA=2", "", "Zemin Kat", 0, 49.00, "zone-3"},
        {"RH|BLOK=B|BB=12|TIP=BALKON", "", "Zemin Kat", 0, 5.00, "zone-4"},
        {"RH|BLOK=B|TIP=EKLENTI_NET", "12", "Bodrum Kat", -1, 4.00, "zone-5"},
        {"BANYO", "", "Zemin Kat", 0, 7.00, "zone-6"}
    };
    const auto zoneSync = RuhsatHesap::SyncZonesToProject (project, zones);
    assert (zoneSync.scannedZones == 6);
    assert (zoneSync.recognizedZones == 5);
    assert (zoneSync.ignoredZones == 1);
    assert (zoneSync.createdBlocks == 1);
    assert (zoneSync.createdUnits == 1);
    const auto& zonedUnit = project.blocks.back ().units.front ();
    assert (zonedUnit.number == "12");
    assert (zonedUnit.floor == "Zemin Kat");
    assert (zonedUnit.roomCount == 2);
    assert (std::abs (zonedUnit.netArea - 37.75) < 0.001);
    assert (std::abs (zonedUnit.grossArea - 49.00) < 0.001);
    assert (std::abs (zonedUnit.extensionNetArea - 4.00) < 0.001);
    assert (std::abs (zonedUnit.balconyArea - 5.00) < 0.001);
    assert (zonedUnit.archicadZoneCount == 5);
    const auto repeatedZoneSync = RuhsatHesap::SyncZonesToProject (project, zones);
    assert (repeatedZoneSync.updatedUnits == 1);
    assert (std::abs (project.blocks.back ().units.front ().netArea - 37.75) < 0.001);
    auto clearedZoneProject = project;
    RuhsatHesap::SyncZonesToProject (clearedZoneProject, {});
    assert (clearedZoneProject.blocks.back ().units.front ().archicadZoneLinked);
    assert (clearedZoneProject.blocks.back ().units.front ().archicadZoneCount == 0);
    assert (clearedZoneProject.blocks.back ().units.front ().netArea == 0.0);

    project.sourceEnvelope = {
        {"project", {{"data", {{"projectName", "PLN Kayit Testi"}}}}}
    };
    project.auxiliaryData["shelter"]["useType"] = "residential";
    project.auxiliaryData["constructionKeys"] = {"merdiven", "asansor", "hol"};
    const std::string plnPayload = RuhsatHesap::SerializeProjectForPln (project);
    const auto plnRoundTrip = RuhsatHesap::DeserializeProjectFromPln (plnPayload);
    assert (plnRoundTrip.schemaVersion == project.schemaVersion);
    assert (plnRoundTrip.blocks.size () == project.blocks.size ());
    assert (plnRoundTrip.blocks.back ().units.front ().archicadZoneGuids.size () == 5);
    assert (plnRoundTrip.sourceEnvelope["project"]["data"]["projectName"] == "PLN Kayit Testi");
    assert (plnRoundTrip.auxiliaryData["shelter"]["useType"] == "residential");
    assert (plnRoundTrip.auxiliaryData["constructionKeys"].size () == 3);
    bool invalidPlnPayloadRejected = false;
    try { (void) RuhsatHesap::DeserializeProjectFromPln ("{\"format\":\"other\"}"); }
    catch (const std::exception&) { invalidPlnPayloadRejected = true; }
    assert (invalidPlnPayloadRejected);
    project.retainingWalls.push_back ({"İstinat Duvarı 1", 52.50});

    const auto result = RuhsatHesap::Calculate (project);
    assert (std::abs (result.maxFootprint - 564.072) < 0.001);
    assert (std::abs (result.maxEmsal - 1128.144) < 0.001);
    assert (result.requiredTrees == 31);
    assert (result.requiredParkingSpaces == 2);
    assert (std::abs (result.constructionGrandTotal - 483.62) < 0.001);

    const auto reportFolder = std::filesystem::temp_directory_path () / "ruhsat-hesap-report-test";
    const auto reportPaths = RuhsatHesap::WriteProjectReports (reportFolder, project);
    assert (std::filesystem::exists (reportPaths.excelPath));
    assert (std::filesystem::file_size (reportPaths.excelPath) > 10000);
    assert (std::filesystem::exists (reportPaths.sheetPngPath));
    assert (std::filesystem::file_size (reportPaths.sheetPngPath) > 5000);
    {
        std::ifstream excel (reportPaths.excelPath, std::ios::binary);
        const std::string content ((std::istreambuf_iterator<char> (excel)), std::istreambuf_iterator<char> ());
        assert (content.size () > 4 && content[0] == 'P' && content[1] == 'K');
        assert (content.find ("[$-041F]#,##0.00") != std::string::npos);
    }
    {
        std::ifstream png (reportPaths.sheetPngPath, std::ios::binary);
        unsigned char signature[8] = {};
        png.read (reinterpret_cast<char*> (signature), 8);
        const unsigned char expected[8] = {0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A};
        assert (std::equal (std::begin (signature), std::end (signature), std::begin (expected)));
    }
    std::filesystem::remove_all (reportFolder);

    const auto testFile = std::filesystem::temp_directory_path () / "ruhsat-hesap-core-test.json";
    RuhsatHesap::SaveProject (testFile, project);
    const auto loaded = RuhsatHesap::LoadProject (testFile);
    assert (loaded.blocks.size () == 2);
    assert (loaded.archicadStories.size () == 3);
    assert (loaded.archicadStories.front ().name == "Bodrum Kat");
    assert (loaded.blocks.back ().units.front ().archicadZoneLinked);
    assert (loaded.blocks.back ().units.front ().archicadZoneGuids.size () == 5);
    assert (loaded.auxiliaryData["shelter"]["useType"] == "residential");
    std::filesystem::remove (testFile);

    const auto webEnvelopeFile = std::filesystem::temp_directory_path () / "ruhsat-hesap-web-envelope-test.json";
    const nlohmann::json webEnvelope = {
        {"project", {
            {"data", {{"projectName", "Web Panel Projesi"}}},
            {"blocks", nlohmann::json::array ({
                {{"name", "A"}, {"floors", nlohmann::json::array ()}, {"units", nlohmann::json::array ()}}
            })}
        }}
    };
    {
        std::ofstream output (webEnvelopeFile);
        output << webEnvelope.dump (2);
    }
    auto webProject = RuhsatHesap::LoadProject (webEnvelopeFile);
    webProject.archicadStories = project.archicadStories;
    assert (RuhsatHesap::SyncStoriesToBlocks (webProject) == 3);
    RuhsatHesap::SaveProject (webEnvelopeFile, webProject);
    nlohmann::json savedEnvelope;
    {
        std::ifstream input (webEnvelopeFile);
        input >> savedEnvelope;
    }
    assert (savedEnvelope["project"]["data"]["archicadStories"].size () == 3);
    assert (savedEnvelope["project"]["blocks"][0]["floors"].size () == 3);
    webProject.blocks.front ().units.push_back (zonedUnit);
    RuhsatHesap::SaveProject (webEnvelopeFile, webProject);
    {
        std::ifstream input (webEnvelopeFile);
        input >> savedEnvelope;
    }
    assert (savedEnvelope["project"]["blocks"][0]["units"][0]["no"] == "12");
    assert (savedEnvelope["project"]["blocks"][0]["units"][0]["archicadZoneLinked"] == true);
    std::filesystem::remove (webEnvelopeFile);
    std::cout << "Ruhsat Hesap core tests passed.\n";
}
