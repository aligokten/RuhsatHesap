#include "ReportExport.hpp"

#include <filesystem>
#include <iostream>

int main (int argumentCount, char** arguments)
{
    if (argumentCount != 2) return 2;
    RuhsatHesap::ProjectData project;
    project.parcel.projectName = "Çınar Apartmanı";
    project.parcel.city = "İstanbul";
    project.parcel.district = "Kadıköy";
    project.parcel.neighborhood = "Koşuyolu";
    project.parcel.block = "2170";
    project.parcel.parcel = "14";
    project.parcel.parcelArea = 1410.18;
    project.parcel.taksRate = 0.40;
    project.parcel.kaksRate = 0.80;
    project.parcel.buildingFootprint = 491.00;
    project.providedParkingSpaces = 12;

    for (const std::string blockName : {"A", "B"}) {
        RuhsatHesap::BlockRecord block;
        block.name = blockName;
        block.zeroLevel = "+114,50";
        block.subbasementLevel = "+115,35";
        block.floors = {
            {"Bodrum Kat", {{"merdiven",11.35},{"sacak",4.28}}, {{"merdiven",11.35},{"asansor",4.49},{"siginak",57.08},{"enerji_odasi",9.49},{"hol",21.45},{"su_deposu",17.28}}, 57.08, 104.81},
            {"Zemin Kat", {{"merdiven",11.06},{"kat_holu",17.04},{"sacak",1.85}}, {{"merdiven",11.06},{"asansor",4.49},{"hol",17.04}}, 0.0, 211.06},
            {"1. Kat", {{"merdiven",11.06},{"kat_holu",17.03}}, {{"merdiven",11.06},{"asansor",4.49},{"hol",17.03}}, 0.0, 211.06},
            {"Çatı Katı", {{"merdiven",13.73}}, {{"merdiven",13.73},{"asansor",5.59},{"hol",9.82}}, 0.0, 0.0}
        };
        for (int index = 1; index <= 6; ++index) {
            RuhsatHesap::IndependentUnit unit;
            unit.number = std::to_string (index);
            unit.floor = index <= 2 ? "Zemin Kat" : (index <= 4 ? "1. Kat" : "Bodrum Kat");
            unit.quality = "Mesken";
            unit.owner = "Malik " + std::to_string (index);
            unit.landShare = "1/12";
            unit.roomCount = index % 3 + 1;
            unit.grossArea = index % 2 == 0 ? 83.78 : 66.27;
            unit.netArea = index % 2 == 0 ? 47.48 : 33.29;
            unit.balconyArea = index % 2 == 0 ? 4.80 : 5.00;
            block.units.push_back (unit);
        }
        project.blocks.push_back (block);
    }
    project.retainingWalls = {{"İstinat Duvarı 1", 312.40}, {"İstinat Duvarı 2", 340.42}};

    const auto paths = RuhsatHesap::WriteProjectReports (std::filesystem::path (arguments[1]), project);
    std::cout << paths.excelPath.string () << '\n' << paths.sheetPngPath.string () << '\n';
    return 0;
}
