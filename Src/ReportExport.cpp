#include "ReportExport.hpp"

#include "CalculationEngine.hpp"

#include <algorithm>
#include <array>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <cwchar>
#include <fstream>
#include <iomanip>
#include <map>
#include <set>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#if defined (WINDOWS)
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
// Archicad's precompiled Win32 interface intentionally uses the lean Windows
// header set.  GDI+ also needs the COM stream/property declarations, so load
// them explicitly before gdiplus.h (otherwise IStream/PROPID/DECLSPEC_UUID are
// missing with the Archicad 29 DevKit PCH).
#include <objidl.h>
#include <propidl.h>
#include <gdiplus.h>
#endif

namespace RuhsatHesap {
namespace {

constexpr int StyleDefault = 0;
constexpr int StyleTitle = 1;
constexpr int StyleSection = 2;
constexpr int StyleHeader = 3;
constexpr int StyleText = 4;
constexpr int StyleNumber = 5;
constexpr int StylePercent = 6;
constexpr int StyleTotal = 7;
constexpr int StyleStatus = 8;
constexpr int StyleInput = 9;

struct Cell {
    int column = 1;
    int style = StyleDefault;
    std::string text;
    std::string formula;
    double number = 0.0;
    bool isNumber = false;
};

struct Worksheet {
    std::string name;
    std::map<int, std::vector<Cell>> rows;
    std::vector<std::string> merges;
    std::vector<double> widths;
    int freezeRow = 0;
    bool landscape = true;
};

struct Workbook {
    std::vector<Worksheet> sheets;
};

struct ZipEntry {
    std::string name;
    std::string data;
    std::uint32_t crc = 0;
    std::uint32_t offset = 0;
};

std::string XmlEscape (const std::string& value)
{
    std::string escaped;
    escaped.reserve (value.size () + 16);
    for (char character : value) {
        switch (character) {
            case '&': escaped += "&amp;"; break;
            case '<': escaped += "&lt;"; break;
            case '>': escaped += "&gt;"; break;
            case '\"': escaped += "&quot;"; break;
            case '\'': escaped += "&apos;"; break;
            default: escaped += character; break;
        }
    }
    return escaped;
}

std::string FormatNumber (double value, int precision = 6)
{
    if (std::abs (value) < 0.0000001) value = 0.0;
    std::ostringstream stream;
    stream << std::fixed << std::setprecision (precision) << value;
    std::string result = stream.str ();
    while (result.size () > 1 && result.back () == '0') result.pop_back ();
    if (!result.empty () && result.back () == '.') result.pop_back ();
    return result;
}

[[maybe_unused]] std::string DisplayNumber (double value)
{
    std::ostringstream stream;
    stream << std::fixed << std::setprecision (2) << value;
    std::string result = stream.str ();
    std::replace (result.begin (), result.end (), '.', ',');
    return result;
}

std::string ColumnName (int column)
{
    std::string result;
    while (column > 0) {
        const int remainder = (column - 1) % 26;
        result.insert (result.begin (), static_cast<char> ('A' + remainder));
        column = (column - 1) / 26;
    }
    return result;
}

std::string CellRef (int row, int column)
{
    return ColumnName (column) + std::to_string (row);
}

void AddText (Worksheet& sheet, int row, int column, std::string text, int style = StyleText)
{
    sheet.rows[row].push_back ({column, style, std::move (text), {}, 0.0, false});
}

void AddNumber (Worksheet& sheet, int row, int column, double number, int style = StyleNumber)
{
    sheet.rows[row].push_back ({column, style, {}, {}, number, true});
}

void AddFormula (Worksheet& sheet, int row, int column, std::string formula, double cached, int style = StyleNumber)
{
    sheet.rows[row].push_back ({column, style, {}, std::move (formula), cached, true});
}

void AddMergedTitle (Worksheet& sheet, int row, int firstColumn, int lastColumn, const std::string& title, int style = StyleTitle)
{
    AddText (sheet, row, firstColumn, title, style);
    sheet.merges.push_back (CellRef (row, firstColumn) + ":" + CellRef (row, lastColumn));
}

double SumMap (const std::map<std::string, double>& values)
{
    double total = 0.0;
    for (const auto& entry : values) total += entry.second;
    return total;
}

double FloorUnitGross (const BlockRecord& block, const std::string& floorName)
{
    double total = 0.0;
    for (const IndependentUnit& unit : block.units)
        if (unit.floor == floorName) total += unit.grossArea;
    return total;
}

double ParkingContribution (double grossArea)
{
    if (grossArea <= 0.0) return 0.0;
    if (grossArea < 80.0) return 1.0 / 3.0;
    if (grossArea < 120.0) return 1.0 / 2.0;
    if (grossArea < 180.0) return 1.0;
    return 2.0;
}

std::string FriendlyName (const std::string& key)
{
    static const std::map<std::string, std::string> names = {
        {"merdiven", "Merdiven"}, {"acik_cikma", "Açık Çıkma"}, {"açık_çıkma", "Açık Çıkma"},
        {"sacak", "Saçak"}, {"saçak", "Saçak"}, {"havuz", "Havuz"},
        {"asansor", "Asansör"}, {"asansör", "Asansör"}, {"kat_holu", "Emsale Konu Kat Holü"},
        {"hol", "Toplam Kat Holü"}, {"giris_terasi", "Giriş Terası"}, {"giriş_terası", "Giriş Terası"},
        {"bosluklar", "Boşluklar"}, {"boşluklar", "Boşluklar"}, {"makina_odasi", "Makina Odası"},
        {"enerji_odasi", "Enerji Odası"}, {"su_deposu", "Su Deposu"}, {"siginak", "Sığınak"},
        {"sığınak", "Sığınak"}, {"haberlesme_odasi", "Haberleşme Odası"},
        {"bagimsiz_bolum_brut", "Bağımsız Bölüm Brüt Alanı"}
    };
    const auto iterator = names.find (key);
    if (iterator != names.end ()) return iterator->second;
    std::string result = key;
    std::replace (result.begin (), result.end (), '_', ' ');
    if (!result.empty ()) result.front () = static_cast<char> (std::toupper (static_cast<unsigned char> (result.front ())));
    return result;
}

std::set<std::string> CollectThirtyPercentKeys (const ProjectData& project)
{
    std::set<std::string> keys;
    for (const BlockRecord& block : project.blocks)
        for (const FloorRecord& floor : block.floors)
            for (const auto& entry : floor.thirtyPercentAreas) keys.insert (entry.first);
    return keys;
}

std::set<std::string> CollectConstructionKeys (const ProjectData& project)
{
    std::set<std::string> keys;
    for (const BlockRecord& block : project.blocks)
        for (const FloorRecord& floor : block.floors)
            for (const auto& entry : floor.constructionAreas)
                if (entry.first != "bagimsiz_bolum_brut") keys.insert (entry.first);
    return keys;
}

std::string SheetQuoted (const std::string& name)
{
    std::string escaped;
    for (char character : name) {
        escaped += character;
        if (character == '\'') escaped += '\'';
    }
    return "'" + escaped + "'";
}

Worksheet BuildEmsalSheet (const ProjectData& project)
{
    Worksheet sheet;
    sheet.name = "Emsal Hesabı";
    const std::set<std::string> keys = CollectThirtyPercentKeys (project);
    const int firstDynamic = 3;
    const int percentTotalColumn = firstDynamic + static_cast<int> (keys.size ());
    const int outsideColumn = percentTotalColumn + 1;
    const int emsalColumn = outsideColumn + 1;
    const int constructionColumn = emsalColumn + 1;
    sheet.widths = {13, 20};
    for (std::size_t index = 0; index < keys.size (); ++index) sheet.widths.push_back (15);
    sheet.widths.insert (sheet.widths.end (), {16, 16, 16, 18});
    sheet.freezeRow = 4;
    AddMergedTitle (sheet, 1, 1, constructionColumn, "EMSAL HESAP TABLOSU");
    AddText (sheet, 3, 1, "Blok", StyleHeader);
    AddText (sheet, 3, 2, "Kat", StyleHeader);
    int column = firstDynamic;
    for (const std::string& key : keys) AddText (sheet, 3, column++, FriendlyName (key), StyleHeader);
    AddText (sheet, 3, percentTotalColumn, "%30 Dahil Toplam", StyleHeader);
    AddText (sheet, 3, outsideColumn, "Emsal Dışı", StyleHeader);
    AddText (sheet, 3, emsalColumn, "Emsal Alan", StyleHeader);
    AddText (sheet, 3, constructionColumn, "Toplam İnşaat", StyleHeader);

    int row = 4;
    std::vector<int> blockTotalRows;
    for (const BlockRecord& block : project.blocks) {
        const int blockStart = row;
        for (const FloorRecord& floor : block.floors) {
            AddText (sheet, row, 1, block.name);
            AddText (sheet, row, 2, floor.name);
            column = firstDynamic;
            for (const std::string& key : keys) {
                const auto iterator = floor.thirtyPercentAreas.find (key);
                AddNumber (sheet, row, column++, iterator == floor.thirtyPercentAreas.end () ? 0.0 : iterator->second);
            }
            const double thirtyTotal = SumMap (floor.thirtyPercentAreas);
            const std::string percentRange = keys.empty () ? "0" : "ROUND(SUM(" + CellRef (row, firstDynamic) + ":" + CellRef (row, percentTotalColumn - 1) + "),2)";
            AddFormula (sheet, row, percentTotalColumn, percentRange, thirtyTotal);
            AddNumber (sheet, row, outsideColumn, floor.emsalOutsideArea);
            AddNumber (sheet, row, emsalColumn, floor.emsalArea);
            AddFormula (sheet, row, constructionColumn,
                "ROUND(SUM(" + CellRef (row, percentTotalColumn) + ":" + CellRef (row, emsalColumn) + "),2)",
                thirtyTotal + floor.emsalOutsideArea + floor.emsalArea);
            ++row;
        }
        AddText (sheet, row, 1, block.name + " BLOK TOPLAMI", StyleTotal);
        sheet.merges.push_back (CellRef (row, 1) + ":" + CellRef (row, 2));
        for (int totalColumn = firstDynamic; totalColumn <= constructionColumn; ++totalColumn) {
            const std::string formula = blockStart < row
                ? "ROUND(SUM(" + CellRef (blockStart, totalColumn) + ":" + CellRef (row - 1, totalColumn) + "),2)" : "0";
            AddFormula (sheet, row, totalColumn, formula, 0.0, StyleTotal);
        }
        blockTotalRows.push_back (row++);
    }
    AddText (sheet, row, 1, "GENEL TOPLAM", StyleTotal);
    sheet.merges.push_back (CellRef (row, 1) + ":" + CellRef (row, 2));
    for (int totalColumn = firstDynamic; totalColumn <= constructionColumn; ++totalColumn) {
        std::string formula = "SUM(";
        for (std::size_t index = 0; index < blockTotalRows.size (); ++index) {
            if (index > 0) formula += ",";
            formula += CellRef (blockTotalRows[index], totalColumn);
        }
        formula += "),2)";
        formula.insert (0, "ROUND(");
        AddFormula (sheet, row, totalColumn, blockTotalRows.empty () ? "0" : formula, 0.0, StyleTotal);
    }
    return sheet;
}

Worksheet BuildUnitsSheet (const ProjectData& project)
{
    Worksheet sheet;
    sheet.name = "Bağımsız Bölümler";
    sheet.widths = {10, 11, 18, 16, 9, 15, 15, 15, 16, 14, 15, 14, 14, 15};
    sheet.freezeRow = 4;
    AddMergedTitle (sheet, 1, 1, 14, "BAĞIMSIZ BÖLÜM ALAN TABLOSU");
    const std::array<const char*, 14> headers = {"Blok","BB No","Kat","Nitelik","Oda","BB Brüt","Eklenti Brüt","Toplam Brüt","Genel Brüt","BB Net","Eklenti Net","%20 Balkon","Balkon Alanı","Otopark Payı"};
    for (int column = 1; column <= 14; ++column) AddText (sheet, 3, column, headers[static_cast<std::size_t> (column - 1)], StyleHeader);
    std::size_t totalUnitCount = 0;
    for (const BlockRecord& block : project.blocks) totalUnitCount += block.units.size ();
    const double commonArea = project.auxiliaryData.is_object () && project.auxiliaryData.contains ("commonArea") && project.auxiliaryData.at ("commonArea").is_number ()
        ? project.auxiliaryData.at ("commonArea").get<double> ()
        : 0.0;
    const double commonPerUnit = totalUnitCount == 0 ? 0.0 : commonArea / static_cast<double> (totalUnitCount);
    int row = 4;
    for (const BlockRecord& block : project.blocks) {
        for (const IndependentUnit& unit : block.units) {
            AddText (sheet, row, 1, block.name);
            AddText (sheet, row, 2, unit.number);
            AddText (sheet, row, 3, unit.floor);
            AddText (sheet, row, 4, unit.quality);
            AddNumber (sheet, row, 5, unit.roomCount, StyleNumber);
            AddNumber (sheet, row, 6, unit.grossArea, StyleInput);
            AddNumber (sheet, row, 7, unit.extensionGrossArea, StyleInput);
            AddFormula (sheet, row, 8, "ROUND(" + CellRef (row, 6) + "+" + CellRef (row, 7) + ",2)", unit.grossArea + unit.extensionGrossArea);
            AddFormula (sheet, row, 9, "ROUND(" + CellRef (row, 6) + "+" + FormatNumber (commonPerUnit) + ",2)", unit.grossArea + commonPerUnit);
            AddNumber (sheet, row, 10, unit.netArea, StyleInput);
            AddNumber (sheet, row, 11, unit.extensionNetArea, StyleInput);
            AddFormula (sheet, row, 12, "ROUND(" + CellRef (row, 10) + "/5,2)", unit.netArea / 5.0);
            AddNumber (sheet, row, 13, unit.balconyArea, StyleInput);
            AddFormula (sheet, row, 14, "IF(" + CellRef (row, 6) + "<=0,0,IF(" + CellRef (row, 6) + "<80,1/3,IF(" + CellRef (row, 6) + "<120,1/2,IF(" + CellRef (row, 6) + "<180,1,2))))", ParkingContribution (unit.grossArea));
            ++row;
        }
    }
    AddText (sheet, row, 1, "GENEL TOPLAM", StyleTotal);
    sheet.merges.push_back (CellRef (row, 1) + ":" + CellRef (row, 5));
    for (int column = 6; column <= 14; ++column)
        AddFormula (sheet, row, column, row > 4 ? (column == 14 ? "SUM(" : "ROUND(SUM(") + CellRef (4, column) + ":" + CellRef (row - 1, column) + (column == 14 ? ")" : "),2)") : "0", 0.0, StyleTotal);
    return sheet;
}

Worksheet BuildCondominiumSheet (const ProjectData& project)
{
    Worksheet sheet;
    sheet.name = "Kat İrtifakı";
    sheet.widths = {10,12,12,14,18,16,20,17,17,24};
    sheet.freezeRow = 4;
    AddMergedTitle (sheet, 1, 1, 10, "KAT İRTİFAKI TABLOSU");
    const std::array<const char*, 10> headers = {"Blok","BB No","Değeri","Arsa Payı","Bulunduğu Kat","Niteliği","Eklenti","BB Brüt Alanı","BB Net Alanı","Maliki"};
    for (int column = 1; column <= 10; ++column) AddText (sheet, 3, column, headers[static_cast<std::size_t> (column - 1)], StyleHeader);
    int row = 4;
    for (const BlockRecord& block : project.blocks) {
        for (const IndependentUnit& unit : block.units) {
            AddText (sheet, row, 1, block.name);
            AddText (sheet, row, 2, unit.number);
            AddText (sheet, row, 3, "", StyleInput);
            AddText (sheet, row, 4, unit.landShare, StyleInput);
            AddText (sheet, row, 5, unit.floor, StyleInput);
            AddText (sheet, row, 6, unit.quality, StyleInput);
            AddText (sheet, row, 7, unit.extensionGrossArea > 0.0 ? "Eklenti" : "", StyleInput);
            AddNumber (sheet, row, 8, unit.grossArea);
            AddNumber (sheet, row, 9, unit.netArea);
            AddText (sheet, row, 10, unit.owner, StyleInput);
            ++row;
        }
    }
    AddText (sheet, row, 1, "GENEL TOPLAM", StyleTotal);
    sheet.merges.push_back (CellRef (row, 1) + ":" + CellRef (row, 7));
    AddFormula (sheet, row, 8, row > 4 ? "SUM(H4:H" + std::to_string (row - 1) + ")" : "0", 0.0, StyleTotal);
    AddFormula (sheet, row, 9, row > 4 ? "SUM(I4:I" + std::to_string (row - 1) + ")" : "0", 0.0, StyleTotal);
    return sheet;
}

Worksheet BuildConstructionSheet (const ProjectData& project)
{
    Worksheet sheet;
    sheet.name = "Yapı İnşaat Alanı";
    const std::set<std::string> keys = CollectConstructionKeys (project);
    const int firstDynamic = 4;
    const int totalColumn = firstDynamic + static_cast<int> (keys.size ());
    sheet.widths = {11,20,17};
    for (std::size_t index = 0; index < keys.size (); ++index) sheet.widths.push_back (16);
    sheet.widths.push_back (18);
    sheet.freezeRow = 4;
    AddMergedTitle (sheet, 1, 1, totalColumn, "YAPI İNŞAAT ALANI");
    AddText (sheet, 3, 1, "Blok", StyleHeader);
    AddText (sheet, 3, 2, "Kat", StyleHeader);
    AddText (sheet, 3, 3, "BB Brüt Alanı", StyleHeader);
    int column = firstDynamic;
    for (const std::string& key : keys) AddText (sheet, 3, column++, FriendlyName (key), StyleHeader);
    AddText (sheet, 3, totalColumn, "Kat Yüzölçümü", StyleHeader);
    int row = 4;
    for (const BlockRecord& block : project.blocks) {
        for (const FloorRecord& floor : block.floors) {
            AddText (sheet, row, 1, block.name);
            AddText (sheet, row, 2, floor.name);
            const double unitGross = FloorUnitGross (block, floor.name);
            AddNumber (sheet, row, 3, unitGross);
            column = firstDynamic;
            double extraTotal = 0.0;
            for (const std::string& key : keys) {
                const auto iterator = floor.constructionAreas.find (key);
                const double value = iterator == floor.constructionAreas.end () ? 0.0 : iterator->second;
                extraTotal += value;
                AddNumber (sheet, row, column++, value, StyleInput);
            }
            AddFormula (sheet, row, totalColumn, "ROUND(SUM(C" + std::to_string (row) + ":" + CellRef (row, totalColumn - 1) + "),2)", unitGross + extraTotal);
            ++row;
        }
    }
    AddText (sheet, row, 1, "GENEL TOPLAM", StyleTotal);
    sheet.merges.push_back (CellRef (row, 1) + ":" + CellRef (row, 2));
    for (int sumColumn = 3; sumColumn <= totalColumn; ++sumColumn)
        AddFormula (sheet, row, sumColumn, row > 4 ? "ROUND(SUM(" + CellRef (4, sumColumn) + ":" + CellRef (row - 1, sumColumn) + "),2)" : "0", 0.0, StyleTotal);
    return sheet;
}

Worksheet BuildAreaSheet (const ProjectData& project, const Worksheet& constructionSheet)
{
    Worksheet sheet;
    sheet.name = "İnşaat Alanı";
    sheet.widths = {24};
    for (std::size_t index = 0; index < project.blocks.size (); ++index) sheet.widths.push_back (18);
    sheet.widths.push_back (18);
    const int totalColumn = static_cast<int> (project.blocks.size ()) + 2;
    AddMergedTitle (sheet, 1, 1, totalColumn, "İNŞAAT ALANI HESABI");
    AddText (sheet, 3, 1, "Kat / Alan", StyleHeader);
    for (std::size_t blockIndex = 0; blockIndex < project.blocks.size (); ++blockIndex)
        AddText (sheet, 3, static_cast<int> (blockIndex) + 2, project.blocks[blockIndex].name + " BLOK", StyleHeader);
    AddText (sheet, 3, totalColumn, "TOPLAM", StyleHeader);

    std::vector<std::string> floors;
    for (const BlockRecord& block : project.blocks)
        for (const FloorRecord& floor : block.floors)
            if (std::find (floors.begin (), floors.end (), floor.name) == floors.end ()) floors.push_back (floor.name);
    const int constructionTotalColumn = static_cast<int> (constructionSheet.widths.size ());
    int row = 4;
    int constructionRow = 4;
    std::map<std::pair<std::string, std::string>, int> constructionRows;
    for (const BlockRecord& block : project.blocks)
        for (const FloorRecord& floor : block.floors) constructionRows[{block.name, floor.name}] = constructionRow++;
    for (const std::string& floorName : floors) {
        AddText (sheet, row, 1, floorName);
        double rowTotal = 0.0;
        for (std::size_t blockIndex = 0; blockIndex < project.blocks.size (); ++blockIndex) {
            const BlockRecord& block = project.blocks[blockIndex];
            const auto iterator = constructionRows.find ({block.name, floorName});
            if (iterator == constructionRows.end ()) {
                AddNumber (sheet, row, static_cast<int> (blockIndex) + 2, 0.0);
            } else {
                const FloorRecord& floor = *std::find_if (block.floors.begin (), block.floors.end (), [&floorName] (const FloorRecord& item) { return item.name == floorName; });
                const double value = FloorUnitGross (block, floorName) + [&floor] { double sum = 0.0; for (const auto& entry : floor.constructionAreas) if (entry.first != "bagimsiz_bolum_brut") sum += entry.second; return sum; } ();
                rowTotal += value;
                AddFormula (sheet, row, static_cast<int> (blockIndex) + 2,
                    SheetQuoted (constructionSheet.name) + "!" + CellRef (iterator->second, constructionTotalColumn), value);
            }
        }
        AddFormula (sheet, row, totalColumn, "ROUND(SUM(B" + std::to_string (row) + ":" + CellRef (row, totalColumn - 1) + "),2)", rowTotal);
        ++row;
    }
    AddText (sheet, row, 1, "İstinat Duvarı", StyleSection);
    const double retainingTotal = [&project] { double sum = 0.0; for (const RetainingWall& wall : project.retainingWalls) sum += wall.area; return sum; } ();
    for (std::size_t blockIndex = 0; blockIndex < project.blocks.size (); ++blockIndex) AddText (sheet, row, static_cast<int> (blockIndex) + 2, "—", StyleSection);
    AddFormula (sheet, row, totalColumn, SheetQuoted ("Diğer Hesaplar") + "!B5", retainingTotal, StyleSection);
    ++row;
    AddText (sheet, row, 1, "GENEL TOPLAM", StyleTotal);
    for (int column = 2; column <= totalColumn; ++column)
        AddFormula (sheet, row, column, "ROUND(SUM(" + CellRef (4, column) + ":" + CellRef (row - 1, column) + "),2)", 0.0, StyleTotal);
    return sheet;
}

Worksheet BuildOtherSheet (const ProjectData& project, const CalculationSummary& summary)
{
    Worksheet sheet;
    sheet.name = "Diğer Hesaplar";
    sheet.widths = {34,18,58};
    AddMergedTitle (sheet, 1, 1, 3, "DİĞER RUHSAT HESAPLARI");
    AddText (sheet, 3, 1, "İSTİNAT DUVARI ALAN HESABI", StyleSection);
    AddText (sheet, 4, 1, "İstinat Duvarı", StyleHeader);
    AddText (sheet, 4, 2, "Alan (m²)", StyleHeader);
    int row = 6;
    double retainingTotal = 0.0;
    for (const RetainingWall& wall : project.retainingWalls) {
        AddText (sheet, row, 1, wall.name);
        AddNumber (sheet, row, 2, wall.area, StyleInput);
        retainingTotal += wall.area;
        ++row;
    }
    AddText (sheet, 5, 1, "TOPLAM", StyleTotal);
    AddFormula (sheet, 5, 2, project.retainingWalls.empty () ? "0" : "ROUND(SUM(B6:B" + std::to_string (row - 1) + "),2)", retainingTotal, StyleTotal);

    row += 2;
    AddText (sheet, row, 1, "0.00 VE SUBASMAN KOTLARI", StyleSection);
    ++row;
    AddText (sheet, row, 1, "Blok", StyleHeader); AddText (sheet, row, 2, "0.00 Kotu", StyleHeader); AddText (sheet, row, 3, "Subasman Kotu", StyleHeader);
    for (const BlockRecord& block : project.blocks) {
        ++row; AddText (sheet, row, 1, block.name); AddText (sheet, row, 2, block.zeroLevel, StyleInput); AddText (sheet, row, 3, block.subbasementLevel, StyleInput);
    }
    row += 3;
    AddText (sheet, row, 1, "OTOPARK HESABI", StyleSection);
    AddText (sheet, row + 1, 1, "Gerekli Otopark"); AddNumber (sheet, row + 1, 2, summary.requiredParkingSpaces);
    AddText (sheet, row + 2, 1, "Projede Ayrılan"); AddNumber (sheet, row + 2, 2, project.providedParkingSpaces, StyleInput);
    AddText (sheet, row + 3, 1, "Kontrol"); AddFormula (sheet, row + 3, 2, "IF(B" + std::to_string (row + 2) + ">=B" + std::to_string (row + 1) + ",\"SAĞLANDI\",\"EKSİK\")", 0.0, StyleStatus);
    row += 6;
    AddText (sheet, row, 1, "KAZI–DOLGU HESABI", StyleSection);
    AddMergedTitle (sheet, row + 1, 1, 3, "Harita mühendisinin hazırladığı kazı dolgu belgesine göre işlem yapılacaktır.", StyleText);
    row += 4;
    AddText (sheet, row, 1, "Sığınak Hesabı", StyleSection);
    AddMergedTitle (sheet, row + 1, 1, 3, "Sığınak gerekliliği kullanım türü, bağımsız bölüm/yatak/kişi sayısı ve emsale konu alana göre yönetmelik kapsamında kontrol edilir.", StyleText);
    return sheet;
}

Worksheet BuildSummarySheet (const ProjectData& project, const CalculationSummary& summary, int emsalLastRow, int emsalColumn, int unitLastRow, const Worksheet& areaSheet)
{
    Worksheet sheet;
    sheet.name = "Özet";
    sheet.widths = {28,22,4,31,22,4,20,20};
    AddMergedTitle (sheet, 1, 1, 8, "RUHSAT HESAP — PROJE RAPORU");
    AddMergedTitle (sheet, 3, 1, 2, "PARSEL BİLGİLERİ", StyleSection);
    const std::array<std::pair<const char*, std::string>, 6> parcelRows = {{
        {"Proje", project.parcel.projectName}, {"İl", project.parcel.city}, {"İlçe", project.parcel.district},
        {"Mahalle", project.parcel.neighborhood}, {"Ada", project.parcel.block}, {"Parsel", project.parcel.parcel}
    }};
    int row = 4;
    for (const auto& entry : parcelRows) { AddText (sheet, row, 1, entry.first); AddText (sheet, row, 2, entry.second, StyleInput); ++row; }
    AddText (sheet, 11, 1, "Parsel Alanı (m²)"); AddNumber (sheet, 11, 2, project.parcel.parcelArea, StyleInput);
    AddText (sheet, 12, 1, "TAKS Oranı"); AddNumber (sheet, 12, 2, project.parcel.taksRate, StylePercent);
    AddText (sheet, 13, 1, "KAKS Oranı"); AddNumber (sheet, 13, 2, project.parcel.kaksRate, StylePercent);
    AddText (sheet, 14, 1, "Doğrudan Emsal Hakkı"); AddNumber (sheet, 14, 2, project.parcel.directEmsal, StyleInput);
    AddText (sheet, 15, 1, "Emsal Yöntemi"); AddText (sheet, 15, 2, project.parcel.emsalMethod, StyleInput);
    AddText (sheet, 16, 1, "Yapı Oturum Alanı"); AddNumber (sheet, 16, 2, project.parcel.buildingFootprint, StyleInput);

    AddMergedTitle (sheet, 3, 4, 5, "TAKS / KAKS VE KONTROLLER", StyleSection);
    AddText (sheet, 4, 4, "Azami TAKS Alanı"); AddFormula (sheet, 4, 5, "ROUND(B11*B12,2)", summary.maxFootprint, StyleTotal);
    AddText (sheet, 5, 4, "Azami Emsal Alanı"); AddFormula (sheet, 5, 5, "ROUND(IF(B15=\"direct\",B14,B11*B13),2)", summary.maxEmsal, StyleTotal);
    AddText (sheet, 6, 4, "Hesaplanan Emsal"); AddFormula (sheet, 6, 5, SheetQuoted ("Emsal Hesabı") + "!" + CellRef (emsalLastRow, emsalColumn), summary.calculatedEmsal, StyleTotal);
    AddText (sheet, 7, 4, "Emsal Aşımı"); AddFormula (sheet, 7, 5, "ROUND(MAX(0,E6-E5),2)", summary.emsalExcess, StyleStatus);
    AddText (sheet, 8, 4, "Emsal Kontrolü"); AddFormula (sheet, 8, 5, "IF(E7>0,\"AŞIM: \"&TEXT(E7,\"0.00\")&\" m²\",\"✓ UYGUN\")", 0.0, StyleStatus);
    AddText (sheet, 10, 4, "Gerekli Ağaç"); AddFormula (sheet, 10, 5, "ROUNDUP(MAX(0,(B11-B16)/30),0)", summary.requiredTrees, StyleTotal);
    AddText (sheet, 11, 4, "Gerekli Otopark"); AddFormula (sheet, 11, 5, "ROUNDUP(" + SheetQuoted ("Bağımsız Bölümler") + "!N" + std::to_string (unitLastRow) + ",0)", summary.requiredParkingSpaces, StyleTotal);
    AddText (sheet, 12, 4, "Projede Ayrılan Otopark"); AddNumber (sheet, 12, 5, project.providedParkingSpaces, StyleInput);
    AddText (sheet, 13, 4, "Otopark Kontrolü"); AddFormula (sheet, 13, 5, "IF(E12>=E11,\"✓ SAĞLANDI\",\"EKSİK: \"&(E11-E12))", 0.0, StyleStatus);
    const int areaTotalColumn = static_cast<int> (areaSheet.widths.size ());
    const int areaGeneralTotalRow = areaSheet.rows.empty () ? 5 : areaSheet.rows.rbegin ()->first;
    const int areaLastFloorRow = std::max (4, areaGeneralTotalRow - 2);
    AddText (sheet, 15, 4, "Toplam Yapı İnşaat Alanı"); AddFormula (sheet, 15, 5, "ROUND(SUM(" + SheetQuoted ("İnşaat Alanı") + "!" + CellRef (4, areaTotalColumn) + ":" + CellRef (areaLastFloorRow, areaTotalColumn) + "),2)", summary.constructionArea, StyleTotal);
    AddText (sheet, 16, 4, "İstinat Duvarı"); AddFormula (sheet, 16, 5, SheetQuoted ("Diğer Hesaplar") + "!B5", summary.retainingWallArea, StyleTotal);

    AddMergedTitle (sheet, 19, 1, 8, "Çıktıdaki mavi hücreler proje girdilerini, yeşil toplamlar formül sonuçlarını gösterir. Excel açıldığında formüller yeniden hesaplanır.", StyleText);
    return sheet;
}

Workbook BuildWorkbook (const ProjectData& project)
{
    const CalculationSummary summary = Calculate (project);
    Worksheet emsal = BuildEmsalSheet (project);
    Worksheet units = BuildUnitsSheet (project);
    Worksheet condominium = BuildCondominiumSheet (project);
    Worksheet construction = BuildConstructionSheet (project);
    Worksheet other = BuildOtherSheet (project, summary);
    Worksheet area = BuildAreaSheet (project, construction);
    const int emsalLastRow = emsal.rows.empty () ? 4 : emsal.rows.rbegin ()->first;
    const int unitLastRow = units.rows.empty () ? 4 : units.rows.rbegin ()->first;
    const int emsalColumn = 5 + static_cast<int> (CollectThirtyPercentKeys (project).size ());
    Worksheet summarySheet = BuildSummarySheet (project, summary, emsalLastRow, emsalColumn, unitLastRow, area);
    Workbook workbook;
    workbook.sheets = {std::move (summarySheet), std::move (emsal), std::move (units), std::move (condominium), std::move (construction), std::move (area), std::move (other)};
    return workbook;
}

std::string StylesXml ()
{
    return R"xml(<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<numFmts count="2"><numFmt numFmtId="164" formatCode="[$-041F]#,##0.00"/><numFmt numFmtId="165" formatCode="[$-041F]0.00%"/></numFmts>
<fonts count="5">
<font><sz val="10"/><name val="Aptos"/><family val="2"/></font>
<font><b/><sz val="18"/><color rgb="FFFFFFFF"/><name val="Aptos Display"/></font>
<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Aptos"/></font>
<font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="Aptos"/></font>
<font><b/><sz val="10"/><color rgb="FF137A4A"/><name val="Aptos"/></font>
</fonts>
<fills count="7"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF171B1C"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FF18A05E"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FF263238"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFE8F5EE"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFE8F1F8"/><bgColor indexed="64"/></patternFill></fill></fills>
<borders count="3"><border/><border><left style="thin"><color rgb="FFB8C1C3"/></left><right style="thin"><color rgb="FFB8C1C3"/></right><top style="thin"><color rgb="FFB8C1C3"/></top><bottom style="thin"><color rgb="FFB8C1C3"/></bottom><diagonal/></border><border><left style="medium"><color rgb="FF18A05E"/></left><right style="medium"><color rgb="FF18A05E"/></right><top style="medium"><color rgb="FF18A05E"/></top><bottom style="medium"><color rgb="FF18A05E"/></bottom><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="10">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
<xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
<xf numFmtId="0" fontId="3" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
<xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
<xf numFmtId="165" fontId="0" fillId="6" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
<xf numFmtId="164" fontId="4" fillId="5" borderId="2" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
<xf numFmtId="0" fontId="4" fillId="5" borderId="2" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
<xf numFmtId="164" fontId="0" fillId="6" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
</cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>)xml";
}

std::string WorksheetXml (const Worksheet& sheet)
{
    std::ostringstream xml;
    xml << "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        << "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">";
    if (sheet.freezeRow > 0)
        xml << "<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"><pane ySplit=\"" << sheet.freezeRow - 1 << "\" topLeftCell=\"A" << sheet.freezeRow << "\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>";
    else xml << "<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"/></sheetViews>";
    xml << "<sheetFormatPr defaultRowHeight=\"18\"/>";
    if (!sheet.widths.empty ()) {
        xml << "<cols>";
        for (std::size_t index = 0; index < sheet.widths.size (); ++index)
            xml << "<col min=\"" << index + 1 << "\" max=\"" << index + 1 << "\" width=\"" << sheet.widths[index] << "\" customWidth=\"1\"/>";
        xml << "</cols>";
    }
    xml << "<sheetData>";
    for (const auto& [rowNumber, cells] : sheet.rows) {
        xml << "<row r=\"" << rowNumber << "\"" << (rowNumber == 1 ? " ht=\"30\" customHeight=\"1\"" : "") << ">";
        std::vector<Cell> ordered = cells;
        std::sort (ordered.begin (), ordered.end (), [] (const Cell& left, const Cell& right) { return left.column < right.column; });
        for (const Cell& cell : ordered) {
            const std::string reference = CellRef (rowNumber, cell.column);
            if (!cell.formula.empty ()) {
                xml << "<c r=\"" << reference << "\" s=\"" << cell.style << "\"><f>" << XmlEscape (cell.formula) << "</f><v>" << FormatNumber (cell.number) << "</v></c>";
            } else if (cell.isNumber) {
                xml << "<c r=\"" << reference << "\" s=\"" << cell.style << "\"><v>" << FormatNumber (cell.number) << "</v></c>";
            } else {
                xml << "<c r=\"" << reference << "\" s=\"" << cell.style << "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" << XmlEscape (cell.text) << "</t></is></c>";
            }
        }
        xml << "</row>";
    }
    xml << "</sheetData>";
    if (!sheet.merges.empty ()) {
        xml << "<mergeCells count=\"" << sheet.merges.size () << "\">";
        for (const std::string& merge : sheet.merges) xml << "<mergeCell ref=\"" << merge << "\"/>";
        xml << "</mergeCells>";
    }
    xml << "<pageMargins left=\"0.25\" right=\"0.25\" top=\"0.4\" bottom=\"0.4\" header=\"0.2\" footer=\"0.2\"/>"
        << "<pageSetup orientation=\"" << (sheet.landscape ? "landscape" : "portrait") << "\" fitToWidth=\"1\" fitToHeight=\"0\" paperSize=\"9\"/>"
        << "</worksheet>";
    return xml.str ();
}

std::uint32_t Crc32 (const std::string& data)
{
    std::uint32_t crc = 0xFFFFFFFFu;
    for (unsigned char byte : data) {
        crc ^= byte;
        for (int bit = 0; bit < 8; ++bit) crc = (crc >> 1) ^ (0xEDB88320u & (0u - (crc & 1u)));
    }
    return ~crc;
}

void Write16 (std::ostream& output, std::uint16_t value)
{
    output.put (static_cast<char> (value & 0xFFu));
    output.put (static_cast<char> ((value >> 8) & 0xFFu));
}

void Write32 (std::ostream& output, std::uint32_t value)
{
    Write16 (output, static_cast<std::uint16_t> (value & 0xFFFFu));
    Write16 (output, static_cast<std::uint16_t> ((value >> 16) & 0xFFFFu));
}

void WriteZip (const std::filesystem::path& path, std::vector<ZipEntry> entries)
{
    std::ofstream output (path, std::ios::binary | std::ios::trunc);
    if (!output) throw std::runtime_error ("Excel dosyası oluşturulamadı.");
    for (ZipEntry& entry : entries) {
        entry.crc = Crc32 (entry.data);
        entry.offset = static_cast<std::uint32_t> (output.tellp ());
        Write32 (output, 0x04034B50u); Write16 (output, 20); Write16 (output, 0x0800); Write16 (output, 0);
        Write16 (output, 0); Write16 (output, 0); Write32 (output, entry.crc);
        Write32 (output, static_cast<std::uint32_t> (entry.data.size ())); Write32 (output, static_cast<std::uint32_t> (entry.data.size ()));
        Write16 (output, static_cast<std::uint16_t> (entry.name.size ())); Write16 (output, 0);
        output.write (entry.name.data (), static_cast<std::streamsize> (entry.name.size ()));
        output.write (entry.data.data (), static_cast<std::streamsize> (entry.data.size ()));
    }
    const std::uint32_t centralOffset = static_cast<std::uint32_t> (output.tellp ());
    for (const ZipEntry& entry : entries) {
        Write32 (output, 0x02014B50u); Write16 (output, 20); Write16 (output, 20); Write16 (output, 0x0800); Write16 (output, 0);
        Write16 (output, 0); Write16 (output, 0); Write32 (output, entry.crc);
        Write32 (output, static_cast<std::uint32_t> (entry.data.size ())); Write32 (output, static_cast<std::uint32_t> (entry.data.size ()));
        Write16 (output, static_cast<std::uint16_t> (entry.name.size ())); Write16 (output, 0); Write16 (output, 0); Write16 (output, 0); Write16 (output, 0);
        Write32 (output, 0); Write32 (output, entry.offset);
        output.write (entry.name.data (), static_cast<std::streamsize> (entry.name.size ()));
    }
    const std::uint32_t centralSize = static_cast<std::uint32_t> (output.tellp ()) - centralOffset;
    Write32 (output, 0x06054B50u); Write16 (output, 0); Write16 (output, 0);
    Write16 (output, static_cast<std::uint16_t> (entries.size ())); Write16 (output, static_cast<std::uint16_t> (entries.size ()));
    Write32 (output, centralSize); Write32 (output, centralOffset); Write16 (output, 0);
    if (!output) throw std::runtime_error ("Excel paketi yazılırken hata oluştu.");
}

std::string SafeBaseName (const ProjectData& project)
{
    std::string value = project.parcel.projectName.empty () ? "RuhsatHesap" : project.parcel.projectName;
    for (char& character : value) {
        const unsigned char byte = static_cast<unsigned char> (character);
        if (byte < 128 && !(std::isalnum (byte) || character == '-' || character == '_')) character = '_';
    }
    while (value.find ("__") != std::string::npos) value.replace (value.find ("__"), 2, "_");
    return value + "_Ruhsat_Hesap";
}

std::string BuildContentTypes (std::size_t sheetCount)
{
    std::ostringstream xml;
    xml << "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        << "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        << "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
        << "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
        << "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>"
        << "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>";
    for (std::size_t index = 1; index <= sheetCount; ++index)
        xml << "<Override PartName=\"/xl/worksheets/sheet" << index << ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>";
    xml << "</Types>";
    return xml.str ();
}

std::string BuildWorkbookXml (const Workbook& workbook)
{
    std::ostringstream xml;
    xml << "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><bookViews><workbookView/></bookViews><sheets>";
    for (std::size_t index = 0; index < workbook.sheets.size (); ++index)
        xml << "<sheet name=\"" << XmlEscape (workbook.sheets[index].name) << "\" sheetId=\"" << index + 1 << "\" r:id=\"rId" << index + 1 << "\"/>";
    xml << "</sheets><calcPr calcId=\"191029\" fullCalcOnLoad=\"1\" forceFullCalc=\"1\"/></workbook>";
    return xml.str ();
}

std::string BuildWorkbookRels (std::size_t sheetCount)
{
    std::ostringstream xml;
    xml << "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">";
    for (std::size_t index = 1; index <= sheetCount; ++index)
        xml << "<Relationship Id=\"rId" << index << "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" << index << ".xml\"/>";
    xml << "<Relationship Id=\"rId" << sheetCount + 1 << "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";
    return xml.str ();
}

#if defined (WINDOWS)

constexpr int PngWidth = 4967;
constexpr int PngHeight = 3508;
constexpr float PngScale = static_cast<float> (PngWidth) / 841.0f;

std::wstring Utf8ToWide (const std::string& text)
{
    if (text.empty ()) return {};
    const int length = MultiByteToWideChar (CP_UTF8, 0, text.data (), static_cast<int> (text.size ()), nullptr, 0);
    if (length <= 0) return {};
    std::wstring result (static_cast<std::size_t> (length), L'\0');
    MultiByteToWideChar (CP_UTF8, 0, text.data (), static_cast<int> (text.size ()), result.data (), length);
    return result;
}

Gdiplus::Color PngColor (std::uint32_t rgb)
{
    return Gdiplus::Color (255, static_cast<BYTE> ((rgb >> 16) & 0xFFu), static_cast<BYTE> ((rgb >> 8) & 0xFFu), static_cast<BYTE> (rgb & 0xFFu));
}

class GdiPlusSession {
public:
    GdiPlusSession ()
    {
        Gdiplus::GdiplusStartupInput input;
        if (Gdiplus::GdiplusStartup (&token, &input, nullptr) != Gdiplus::Ok)
            throw std::runtime_error ("PNG çizim sistemi başlatılamadı.");
    }

    ~GdiPlusSession ()
    {
        if (token != 0) Gdiplus::GdiplusShutdown (token);
    }

    GdiPlusSession (const GdiPlusSession&) = delete;
    GdiPlusSession& operator= (const GdiPlusSession&) = delete;

private:
    ULONG_PTR token = 0;
};

enum class PngTextAlign { Left, Center, Right };

class PngCanvas {
public:
    PngCanvas () : bitmap (PngWidth, PngHeight, PixelFormat32bppARGB), graphics (&bitmap)
    {
        if (bitmap.GetLastStatus () != Gdiplus::Ok)
            throw std::runtime_error ("PNG pafta belleği oluşturulamadı.");
        graphics.SetPageUnit (Gdiplus::UnitPixel);
        graphics.SetSmoothingMode (Gdiplus::SmoothingModeAntiAlias);
        graphics.SetInterpolationMode (Gdiplus::InterpolationModeHighQualityBicubic);
        graphics.SetTextRenderingHint (Gdiplus::TextRenderingHintClearTypeGridFit);
        graphics.Clear (PngColor (0xF2F5F4));
        graphics.ScaleTransform (PngScale, PngScale);
    }

    void FillRect (float x, float y, float width, float height, std::uint32_t color)
    {
        Gdiplus::SolidBrush brush (PngColor (color));
        graphics.FillRectangle (&brush, x, y, width, height);
    }

    void DrawRect (float x, float y, float width, float height, std::uint32_t color, float lineWidth)
    {
        Gdiplus::Pen pen (PngColor (color), lineWidth);
        graphics.DrawRectangle (&pen, x, y, width, height);
    }

    void DrawLine (float x1, float y1, float x2, float y2, std::uint32_t color, float lineWidth)
    {
        Gdiplus::Pen pen (PngColor (color), lineWidth);
        graphics.DrawLine (&pen, x1, y1, x2, y2);
    }

    void DrawText (float x, float y, float width, float height, const std::string& text, float size,
                   std::uint32_t color, bool bold = false, PngTextAlign align = PngTextAlign::Left)
    {
        const std::wstring wide = Utf8ToWide (text);
        const INT style = bold ? Gdiplus::FontStyleBold : Gdiplus::FontStyleRegular;
        Gdiplus::Font font (L"Segoe UI", size, style, Gdiplus::UnitPixel);
        Gdiplus::SolidBrush brush (PngColor (color));
        Gdiplus::StringFormat format;
        format.SetLineAlignment (Gdiplus::StringAlignmentCenter);
        format.SetTrimming (Gdiplus::StringTrimmingEllipsisCharacter);
        format.SetFormatFlags (Gdiplus::StringFormatFlagsNoWrap);
        if (align == PngTextAlign::Center) format.SetAlignment (Gdiplus::StringAlignmentCenter);
        else if (align == PngTextAlign::Right) format.SetAlignment (Gdiplus::StringAlignmentFar);
        else format.SetAlignment (Gdiplus::StringAlignmentNear);
        const Gdiplus::RectF bounds (x, y, width, height);
        graphics.DrawString (wide.c_str (), static_cast<INT> (wide.size ()), &font, bounds, &format, &brush);
    }

    void Save (const std::filesystem::path& path)
    {
        UINT encoderCount = 0;
        UINT encoderBytes = 0;
        Gdiplus::GetImageEncodersSize (&encoderCount, &encoderBytes);
        if (encoderBytes == 0) throw std::runtime_error ("PNG kodlayıcısı bulunamadı.");
        std::vector<BYTE> storage (encoderBytes);
        auto* encoders = reinterpret_cast<Gdiplus::ImageCodecInfo*> (storage.data ());
        Gdiplus::GetImageEncoders (encoderCount, encoderBytes, encoders);
        const CLSID* pngEncoder = nullptr;
        for (UINT index = 0; index < encoderCount; ++index) {
            if (std::wcscmp (encoders[index].MimeType, L"image/png") == 0) {
                pngEncoder = &encoders[index].Clsid;
                break;
            }
        }
        if (pngEncoder == nullptr) throw std::runtime_error ("Windows PNG kodlayıcısı bulunamadı.");
        const std::wstring outputPath = path.wstring ();
        if (bitmap.Save (outputPath.c_str (), pngEncoder, nullptr) != Gdiplus::Ok)
            throw std::runtime_error ("Pafta PNG dosyası kaydedilemedi.");
    }

private:
    Gdiplus::Bitmap bitmap;
    Gdiplus::Graphics graphics;
};

void PngCard (PngCanvas& canvas, float x, float y, float width, float height, const std::string& title)
{
    canvas.FillRect (x, y, width, height, 0xFFFFFF);
    canvas.DrawRect (x, y, width, height, 0x839095, 0.45f);
    canvas.FillRect (x, y, width, 9.0f, 0x171B1C);
    canvas.DrawText (x, y, width, 9.0f, title, 4.0f, 0x18A05E, true, PngTextAlign::Center);
}

void PngKeyValue (PngCanvas& canvas, float x, float y, float width, const std::string& key, const std::string& value)
{
    canvas.DrawLine (x, y + 7.5f, x + width, y + 7.5f, 0xD8DEDF, 0.25f);
    canvas.DrawText (x + 2.0f, y, width * 0.52f, 7.0f, key, 3.0f, 0x4C585C, true);
    canvas.DrawText (x + width * 0.48f, y, width * 0.50f, 7.0f, value, 3.1f, 0x171B1C, true, PngTextAlign::Right);
}

void PngSimpleTable (PngCanvas& canvas, float x, float y, float width, const std::vector<std::string>& headers,
                     const std::vector<std::vector<std::string>>& rows, const std::vector<double>& ratios, int maxRows)
{
    constexpr float rowHeight = 6.4f;
    float cursor = x;
    for (std::size_t column = 0; column < headers.size (); ++column) {
        const float cellWidth = width * static_cast<float> (ratios[column]);
        canvas.FillRect (cursor, y, cellWidth, rowHeight, 0x263238);
        canvas.DrawRect (cursor, y, cellWidth, rowHeight, 0xFFFFFF, 0.18f);
        canvas.DrawText (cursor + 0.8f, y, cellWidth - 1.6f, rowHeight, headers[column], 2.45f, 0xFFFFFF, true, PngTextAlign::Center);
        cursor += cellWidth;
    }
    const int count = std::min (static_cast<int> (rows.size ()), maxRows);
    for (int row = 0; row < count; ++row) {
        cursor = x;
        for (std::size_t column = 0; column < headers.size (); ++column) {
            const float cellWidth = width * static_cast<float> (ratios[column]);
            const float cellY = y + static_cast<float> (row + 1) * rowHeight;
            canvas.FillRect (cursor, cellY, cellWidth, rowHeight, row % 2 == 0 ? 0xFFFFFF : 0xF3F6F5);
            canvas.DrawRect (cursor, cellY, cellWidth, rowHeight, 0xB8C1C3, 0.18f);
            const std::string value = column < rows[static_cast<std::size_t> (row)].size () ? rows[static_cast<std::size_t> (row)][column] : "";
            canvas.DrawText (cursor + 0.8f, cellY, cellWidth - 1.6f, rowHeight, value, 2.4f, 0x171B1C, column == 0, PngTextAlign::Center);
            cursor += cellWidth;
        }
    }
}

#else

void AppendBigEndian32 (std::string& output, std::uint32_t value)
{
    output.push_back (static_cast<char> ((value >> 24) & 0xFFu));
    output.push_back (static_cast<char> ((value >> 16) & 0xFFu));
    output.push_back (static_cast<char> ((value >> 8) & 0xFFu));
    output.push_back (static_cast<char> (value & 0xFFu));
}

std::uint32_t Adler32 (const std::string& data)
{
    std::uint32_t first = 1;
    std::uint32_t second = 0;
    for (unsigned char byte : data) {
        first = (first + byte) % 65521u;
        second = (second + first) % 65521u;
    }
    return (second << 16) | first;
}

void AppendPngChunk (std::string& png, const char* type, const std::string& payload)
{
    AppendBigEndian32 (png, static_cast<std::uint32_t> (payload.size ()));
    const std::string crcData = std::string (type, 4) + payload;
    png.append (crcData);
    AppendBigEndian32 (png, Crc32 (crcData));
}

std::string StoredZlib (const std::string& raw)
{
    std::string result;
    result.push_back (static_cast<char> (0x78));
    result.push_back (static_cast<char> (0x01));
    std::size_t offset = 0;
    while (offset < raw.size ()) {
        const std::size_t remaining = raw.size () - offset;
        const std::uint16_t length = static_cast<std::uint16_t> (std::min<std::size_t> (remaining, 65535));
        const bool finalBlock = offset + length == raw.size ();
        result.push_back (static_cast<char> (finalBlock ? 1 : 0));
        result.push_back (static_cast<char> (length & 0xFFu));
        result.push_back (static_cast<char> ((length >> 8) & 0xFFu));
        const std::uint16_t inverted = static_cast<std::uint16_t> (~length);
        result.push_back (static_cast<char> (inverted & 0xFFu));
        result.push_back (static_cast<char> ((inverted >> 8) & 0xFFu));
        result.append (raw.data () + offset, length);
        offset += length;
    }
    AppendBigEndian32 (result, Adler32 (raw));
    return result;
}

void WritePortableTestPng (const std::filesystem::path& path)
{
    constexpr std::uint32_t width = 64;
    constexpr std::uint32_t height = 64;
    std::string raw;
    raw.reserve (height * (1 + width * 3));
    for (std::uint32_t y = 0; y < height; ++y) {
        raw.push_back (0);
        for (std::uint32_t x = 0; x < width; ++x) {
            const bool accent = x < 8 || y < 8;
            raw.push_back (static_cast<char> (accent ? 24 : 255));
            raw.push_back (static_cast<char> (accent ? 160 : 255));
            raw.push_back (static_cast<char> (accent ? 94 : 255));
        }
    }
    std::string png ("\x89PNG\r\n\x1A\n", 8);
    std::string header;
    AppendBigEndian32 (header, width);
    AppendBigEndian32 (header, height);
    header.push_back (static_cast<char> (8));
    header.push_back (static_cast<char> (2));
    header.push_back (0);
    header.push_back (0);
    header.push_back (0);
    AppendPngChunk (png, "IHDR", header);
    AppendPngChunk (png, "IDAT", StoredZlib (raw));
    AppendPngChunk (png, "IEND", {});
    std::ofstream output (path, std::ios::binary | std::ios::trunc);
    if (!output) throw std::runtime_error ("Pafta PNG test dosyası oluşturulamadı.");
    output.write (png.data (), static_cast<std::streamsize> (png.size ()));
    if (!output) throw std::runtime_error ("Pafta PNG test dosyası yazılamadı.");
}

#endif

} // namespace

void WriteExcelReport (const std::filesystem::path& path, const ProjectData& project)
{
    const Workbook workbook = BuildWorkbook (project);
    std::vector<ZipEntry> entries;
    entries.push_back ({"[Content_Types].xml", BuildContentTypes (workbook.sheets.size ())});
    entries.push_back ({"_rels/.rels", R"xml(<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/></Relationships>)xml"});
    entries.push_back ({"docProps/core.xml", R"xml(<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:creator>Ruhsat Hesap Archicad Add-On</dc:creator><dc:title>Mimari Ruhsat Hesap Raporu</dc:title></cp:coreProperties>)xml"});
    entries.push_back ({"docProps/app.xml", R"xml(<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"><Application>Ruhsat Hesap</Application><AppVersion>0.6.0</AppVersion></Properties>)xml"});
    entries.push_back ({"xl/workbook.xml", BuildWorkbookXml (workbook)});
    entries.push_back ({"xl/_rels/workbook.xml.rels", BuildWorkbookRels (workbook.sheets.size ())});
    entries.push_back ({"xl/styles.xml", StylesXml ()});
    for (std::size_t index = 0; index < workbook.sheets.size (); ++index)
        entries.push_back ({"xl/worksheets/sheet" + std::to_string (index + 1) + ".xml", WorksheetXml (workbook.sheets[index])});
    WriteZip (path, std::move (entries));
}

void WriteSheetPng (const std::filesystem::path& path, const ProjectData& project)
{
#if defined (WINDOWS)
    GdiPlusSession session;
    PngCanvas canvas;
    const CalculationSummary summary = Calculate (project);
    canvas.FillRect (12.0f, 12.0f, 817.0f, 570.0f, 0xFFFFFF);
    canvas.DrawRect (12.0f, 12.0f, 817.0f, 570.0f, 0x171B1C, 0.8f);
    canvas.FillRect (12.0f, 12.0f, 817.0f, 24.0f, 0x171B1C);
    canvas.DrawText (28.0f, 12.0f, 560.0f, 24.0f, "RUHSAT HESAP — MİMARİ PROJE HESAP PAFTASI", 8.0f, 0xFFFFFF, true);
    canvas.DrawText (600.0f, 12.0f, 213.0f, 24.0f, project.parcel.projectName.empty () ? "Adsız Proje" : project.parcel.projectName, 5.0f, 0x18A05E, true, PngTextAlign::Right);

    PngCard (canvas, 24.0f, 48.0f, 250.0f, 104.0f, "PARSEL / TAKS–KAKS");
    const std::vector<std::pair<std::string, std::string>> parcelValues = {
        {"İl / İlçe", project.parcel.city + " / " + project.parcel.district},
        {"Mahalle", project.parcel.neighborhood}, {"Ada / Parsel", project.parcel.block + " / " + project.parcel.parcel},
        {"Parsel Alanı", DisplayNumber (project.parcel.parcelArea) + " m²"},
        {"Azami TAKS", DisplayNumber (summary.maxFootprint) + " m²"}, {"Azami Emsal", DisplayNumber (summary.maxEmsal) + " m²"},
        {"Hesaplanan Emsal", DisplayNumber (summary.calculatedEmsal) + " m²"},
        {"Emsal Kontrolü", summary.emsalExcess > 0.0 ? "AŞIM " + DisplayNumber (summary.emsalExcess) + " m²" : "✓ UYGUN"},
        {"Gerekli Ağaç", std::to_string (summary.requiredTrees) + " adet"},
        {"Otopark", std::to_string (project.providedParkingSpaces) + " / " + std::to_string (summary.requiredParkingSpaces) + " adet"}
    };
    float keyY = 59.0f;
    for (const auto& entry : parcelValues) { PngKeyValue (canvas, 28.0f, keyY, 242.0f, entry.first, entry.second); keyY += 8.2f; }

    PngCard (canvas, 286.0f, 48.0f, 531.0f, 152.0f, "EMSAL HESAP TABLOSU");
    std::vector<std::vector<std::string>> emsalRows;
    for (const BlockRecord& block : project.blocks)
        for (const FloorRecord& floor : block.floors)
            emsalRows.push_back ({block.name, floor.name, DisplayNumber (SumMap (floor.thirtyPercentAreas)), DisplayNumber (floor.emsalOutsideArea), DisplayNumber (floor.emsalArea), DisplayNumber (SumMap (floor.thirtyPercentAreas) + floor.emsalOutsideArea + floor.emsalArea)});
    PngSimpleTable (canvas, 294.0f, 62.0f, 515.0f, {"Blok","Kat","%30","Emsal Dışı","Emsal","Toplam"}, emsalRows, {0.09,0.25,0.14,0.17,0.17,0.18}, 19);

    PngCard (canvas, 24.0f, 164.0f, 250.0f, 150.0f, "İNŞAAT ALANI HESABI");
    std::vector<std::vector<std::string>> constructionRows;
    for (const BlockRecord& block : project.blocks)
        for (const FloorRecord& floor : block.floors) {
            double extras = 0.0;
            for (const auto& entry : floor.constructionAreas) if (entry.first != "bagimsiz_bolum_brut") extras += entry.second;
            const double gross = FloorUnitGross (block, floor.name);
            constructionRows.push_back ({block.name, floor.name, DisplayNumber (gross), DisplayNumber (extras), DisplayNumber (gross + extras)});
        }
    PngSimpleTable (canvas, 32.0f, 178.0f, 234.0f, {"Blok","Kat","BB Brüt","Diğer","Toplam"}, constructionRows, {0.12,0.28,0.20,0.20,0.20}, 18);

    PngCard (canvas, 286.0f, 212.0f, 531.0f, 220.0f, "BAĞIMSIZ BÖLÜM ALAN / KAT İRTİFAKI");
    std::vector<std::vector<std::string>> unitRows;
    for (const BlockRecord& block : project.blocks)
        for (const IndependentUnit& unit : block.units)
            unitRows.push_back ({block.name, unit.number, unit.floor, unit.quality, std::to_string (unit.roomCount), DisplayNumber (unit.grossArea), DisplayNumber (unit.netArea), unit.landShare, unit.owner});
    PngSimpleTable (canvas, 294.0f, 226.0f, 515.0f, {"Blok","BB","Kat","Nitelik","Oda","Brüt","Net","Arsa Payı","Maliki"}, unitRows, {0.07,0.07,0.14,0.14,0.07,0.11,0.11,0.12,0.17}, 29);

    PngCard (canvas, 24.0f, 326.0f, 250.0f, 106.0f, "İSTİNAT / KOT / NOTLAR");
    keyY = 338.0f;
    for (const RetainingWall& wall : project.retainingWalls) { PngKeyValue (canvas, 28.0f, keyY, 242.0f, wall.name, DisplayNumber (wall.area) + " m²"); keyY += 8.2f; }
    for (const BlockRecord& block : project.blocks) {
        PngKeyValue (canvas, 28.0f, keyY, 242.0f, block.name + " Blok 0.00 / Subasman", block.zeroLevel + " / " + block.subbasementLevel); keyY += 8.2f;
    }
    canvas.DrawText (28.0f, 416.0f, 242.0f, 12.0f, "Kazı–dolgu: Harita mühendisinin hazırladığı belgeye göre işlem yapılacaktır.", 2.7f, 0x4C585C);

    PngCard (canvas, 24.0f, 444.0f, 793.0f, 112.0f, "GENEL TOPLAMLAR VE UYGUNLUK");
    const std::array<std::pair<std::string, std::string>, 6> totals = {{
        {"Toplam Yapı İnşaat Alanı", DisplayNumber (summary.constructionArea) + " m²"},
        {"İstinat Duvarı", DisplayNumber (summary.retainingWallArea) + " m²"},
        {"Genel İnşaat Alanı", DisplayNumber (summary.constructionGrandTotal) + " m²"},
        {"Bağımsız Bölüm", std::to_string (summary.unitCount) + " adet"},
        {"Emsal", summary.emsalExcess > 0.0 ? "AŞIM VAR" : "✓ UYGUN"},
        {"Otopark", project.providedParkingSpaces >= summary.requiredParkingSpaces ? "✓ SAĞLANDI" : "EKSİK " + std::to_string (summary.requiredParkingSpaces - project.providedParkingSpaces)}
    }};
    for (std::size_t index = 0; index < totals.size (); ++index) {
        const float cardX = 34.0f + static_cast<float> (index % 3) * 258.0f;
        const float cardY = 463.0f + static_cast<float> (index / 3) * 38.0f;
        canvas.FillRect (cardX, cardY, 238.0f, 30.0f, 0xE8F5EE);
        canvas.DrawRect (cardX, cardY, 238.0f, 30.0f, 0x18A05E, 0.4f);
        canvas.DrawText (cardX + 8.0f, cardY + 3.0f, 222.0f, 11.0f, totals[index].first, 3.2f, 0x4C585C, true);
        canvas.DrawText (cardX + 8.0f, cardY + 12.0f, 222.0f, 14.0f, totals[index].second, 5.0f, 0x137A4A, true, PngTextAlign::Right);
    }
    canvas.DrawText (300.0f, 565.0f, 517.0f, 12.0f, "Ruhsat Hesap Add-On • Excel ve pafta aynı PLN proje verisinden üretilmiştir.", 2.8f, 0x4C585C, false, PngTextAlign::Right);
    canvas.Save (path);
#else
    (void) project;
    WritePortableTestPng (path);
#endif
}

ReportExportPaths WriteProjectReports (const std::filesystem::path& folder, const ProjectData& project)
{
    std::filesystem::create_directories (folder);
    const std::string baseName = SafeBaseName (project);
    ReportExportPaths paths {folder / (baseName + ".xlsx"), folder / (baseName + "_Pafta.png")};
    ProjectData orderedProject = project;
    SortIndependentUnits (orderedProject);
    WriteExcelReport (paths.excelPath, orderedProject);
    WriteSheetPng (paths.sheetPngPath, orderedProject);
    return paths;
}

} // namespace RuhsatHesap
