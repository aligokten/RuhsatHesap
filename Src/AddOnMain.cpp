#include "APIEnvir.h"
#include "ACAPinc.h"

#include "CalculationEngine.hpp"
#include "PlnProjectStore.hpp"
#include "ProjectStore.hpp"
#include "ReportExport.hpp"
#include "ResourceIds.hpp"
#include "StorySync.hpp"
#include "ZoneSync.hpp"

#include "DGModule.hpp"
#include "DGBrowser.hpp"
#include "RS.hpp"

#include <algorithm>
#include <filesystem>
#include <functional>
#include <iomanip>
#include <memory>
#include <sstream>
#include <string>
#include <vector>

#if defined (WINDOWS)
#include <commdlg.h>
#include <shellapi.h>
#include <shlobj.h>
#include <windows.h>
#endif

namespace {

constexpr GSResID AddOnInfoID = ID_ADDON_INFO;
constexpr Int32 AddOnNameID = 1;
constexpr Int32 AddOnDescriptionID = 2;
constexpr short AddOnMenuID = ID_ADDON_MENU;
constexpr Int32 AddOnCommandID = 1;

const GS::Guid PaletteGuid { "{A1538638-478D-40B7-92B1-72B323734A64}" };
// ACAPI_RegisterModelessWindow needs a stable, add-on-local Int32 identifier.
// GS::Guid is deliberately kept for Archicad's palette persistence, but the
// DevKit 29 GSRoot headers do not provide GenerateHashValue (GS::Guid).
constexpr Int32 PaletteReferenceId = 0x52554850; // "RUHP"

std::filesystem::path SelectJsonFile (bool save)
{
#if defined (WINDOWS)
    wchar_t fileName[MAX_PATH] = L"";
    OPENFILENAMEW dialog {};
    dialog.lStructSize = sizeof (dialog);
    dialog.hwndOwner = GetActiveWindow ();
    dialog.lpstrFilter = L"Ruhsat Hesap JSON (*.json)\0*.json\0Tüm Dosyalar (*.*)\0*.*\0";
    dialog.lpstrFile = fileName;
    dialog.nMaxFile = MAX_PATH;
    dialog.lpstrDefExt = L"json";
    dialog.Flags = OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST);
    const BOOL accepted = save ? GetSaveFileNameW (&dialog) : GetOpenFileNameW (&dialog);
    return accepted ? std::filesystem::path (fileName) : std::filesystem::path {};
#else
    (void) save;
    return {};
#endif
}

std::filesystem::path SelectReportFolder ()
{
#if defined (WINDOWS)
    BROWSEINFOW dialog {};
    dialog.hwndOwner = GetActiveWindow ();
    dialog.lpszTitle = L"Excel ve pafta tablosunun kaydedileceği klasörü seçin";
    dialog.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE | BIF_USENEWUI;
    PIDLIST_ABSOLUTE itemIdList = SHBrowseForFolderW (&dialog);
    if (itemIdList == nullptr) return {};
    wchar_t folder[MAX_PATH] = L"";
    const BOOL resolved = SHGetPathFromIDListW (itemIdList, folder);
    CoTaskMemFree (itemIdList);
    return resolved ? std::filesystem::path (folder) : std::filesystem::path {};
#else
    return {};
#endif
}

class RuhsatHesapPalette final :
    public DG::Palette,
    public DG::PanelObserver
{
public:
    enum ItemIds {
        PaletteId = ID_ADDON_PALETTE,
        BrowserId = 1
    };

    RuhsatHesapPalette () :
        DG::Palette (ACAPI_GetOwnResModule (), PaletteId, ACAPI_GetOwnResModule (), PaletteGuid),
        browser (GetReference (), BrowserId)
    {
        SetTitle (ADDON_NAME " " ADDON_VERSION);
        browser.SetContextMenuMode (DG::BrowserBase::ContextMenuMode::Disabled);
        Attach (*this);
        BeginEventProcessing ();
        InitBrowserControl ();
    }

    ~RuhsatHesapPalette () override
    {
        EndEventProcessing ();
        Detach (*this);
    }

    void ShowPalette ()
    {
        if (!IsVisible ()) {
            LoadProjectFromPlnOnce ();
            RefreshZonesFromArchicad (false);
            Show ();
            PushProjectToBrowser ();
        }
    }

    void HidePalette ()
    {
        if (IsVisible ()) Hide ();
    }

    void TogglePalette ()
    {
        IsVisible () ? HidePalette () : ShowPalette ();
    }

    void DisablePaletteItems ()
    {
        browser.Disable ();
    }

    void EnablePaletteItems ()
    {
        browser.Enable ();
    }

    bool PersistProjectToPln (bool updateStatus)
    {
        const RuhsatHesap::PlnSaveResult result = RuhsatHesap::SaveProjectToPln (project);
        lastPlnStoreError = result.error;
        if (result.success) {
            plnDataFound = true;
            if (updateStatus) {
                SetStatus (result.created
                    ? "Proje verisi PLN içine oluşturuldu. Archicad dosyasını kaydedin."
                    : "PLN içindeki proje verisi güncellendi. Archicad dosyasını kaydedin.");
            }
            return true;
        }

        if (updateStatus)
            SetStatus ("PLN kayıt işlemi başarısız: " + result.error);
        return false;
    }

    bool RefreshStoriesFromArchicad (bool userInitiated, bool createDefaultBlock = true)
    {
        API_StoryInfo storyInfo {};
        const GSErrCode error = ACAPI_ProjectSetting_GetStorySettings (&storyInfo);
        if (error != NoError) {
            if (userInitiated || project.archicadStories.empty ())
                SetStatus ("Açık Archicad projesi bulunamadı veya kat bilgileri okunamadı.");
            return false;
        }

        std::vector<RuhsatHesap::ArchicadStory> stories;
        const Int32 storyCount = static_cast<Int32> (storyInfo.lastStory - storyInfo.firstStory + 1);
        if (storyInfo.data != nullptr && storyCount > 0) {
            stories.reserve (static_cast<std::size_t> (storyCount));
            for (Int32 offset = 0; offset < storyCount; ++offset) {
                const API_StoryType& apiStory = (*storyInfo.data)[offset];
                const GS::UniString unicodeName (apiStory.uName);
                std::string storyName (unicodeName.ToCStr (0, MaxUSize, CC_UTF8).Get ());
                if (storyName.empty ()) storyName = "Kat " + std::to_string (apiStory.index);
                stories.push_back ({apiStory.index, apiStory.floorId, std::move (storyName), apiStory.level});
            }
        }
        BMKillHandle (reinterpret_cast<GSHandle*> (&storyInfo.data));

        project.archicadStories = std::move (stories);
        const std::size_t addedFloorCount = RuhsatHesap::SyncStoriesToBlocks (project, createDefaultBlock);

        std::ostringstream message;
        message << project.archicadStories.size () << " kat okundu; "
                << project.blocks.size () << " blok eşleştirildi";
        if (addedFloorCount > 0) message << "; " << addedFloorCount << " eksik kat eklendi";
        message << ".";
        SetStatus (message.str ());
        PushProjectToBrowser ();
        return true;
    }

    bool RefreshZonesFromArchicad (bool userInitiated)
    {
        RefreshStoriesFromArchicad (false, false);

        GS::Array<API_Guid> zoneGuids;
        const GSErrCode listError = ACAPI_Element_GetElemList (API_ZoneID, &zoneGuids, APIFilt_FromFloorplan);
        if (listError != NoError) {
            if (userInitiated) SetStatus ("Archicad zonları okunamadı.");
            return false;
        }

        std::vector<RuhsatHesap::ZoneObservation> observations;
        observations.reserve (static_cast<std::size_t> (zoneGuids.GetSize ()));
        for (const API_Guid& zoneGuid : zoneGuids) {
            API_Element element {};
            element.header.guid = zoneGuid;
            if (ACAPI_Element_Get (&element) != NoError) continue;

            const GS::UniString unicodeName (element.zone.roomName);
            const GS::UniString unicodeNumber (element.zone.roomNoStr);
            const std::string zoneName (unicodeName.ToCStr (0, MaxUSize, CC_UTF8).Get ());
            const std::string zoneNumber (unicodeNumber.ToCStr (0, MaxUSize, CC_UTF8).Get ());

            API_ElementQuantity quantity {};
            API_Quantities quantities {};
            API_QuantitiesMask quantityMask {};
            API_QuantityPar quantityParameters {};
            ACAPI_ELEMENT_QUANTITY_MASK_CLEAR (quantityMask);
            ACAPI_ELEMENT_QUANTITY_MASK_SET (quantityMask, zone, netarea);
            quantities.elements = &quantity;
            quantityParameters.minOpeningSize = 0.0;

            double area = 0.0;
            if (ACAPI_Element_GetQuantities (zoneGuid, &quantityParameters, &quantities, &quantityMask) == NoError)
                area = quantity.zone.netarea;

            std::string storyName = "Kat " + std::to_string (element.header.floorInd);
            const auto storyIterator = std::find_if (
                project.archicadStories.begin (),
                project.archicadStories.end (),
                [&element] (const RuhsatHesap::ArchicadStory& story) { return story.index == element.header.floorInd; }
            );
            if (storyIterator != project.archicadStories.end ()) storyName = storyIterator->name;

            observations.push_back ({
                zoneName,
                zoneNumber,
                storyName,
                element.header.floorInd,
                area,
                APIGuidToString (zoneGuid).ToCStr ().Get ()
            });
        }

        const RuhsatHesap::ZoneSyncResult sync = RuhsatHesap::SyncZonesToProject (project, observations);
        RuhsatHesap::SyncStoriesToBlocks (project, false);

        const bool shouldPersist = userInitiated || plnDataFound || sync.recognizedZones > 0;
        const bool persisted = shouldPersist && PersistProjectToPln (false);

        std::ostringstream message;
        message << sync.scannedZones << " zon tarandı; "
                << sync.recognizedZones << " RH zonu aktarıldı; "
                << sync.createdUnits << " bağımsız bölüm eklendi; "
                << sync.updatedUnits << " bağımsız bölüm güncellendi";
        if (sync.updatedFloorAreas > 0) message << "; " << sync.updatedFloorAreas << " blok/kat alanı güncellendi";
        if (sync.invalidZones > 0) message << "; " << sync.invalidZones << " hatalı RH adı/alan";
        message << ".";
        if (persisted) message << " PLN proje verisi güncellendi.";
        else if (shouldPersist && !lastPlnStoreError.empty ()) message << " PLN kayıt hatası: " << lastPlnStoreError;
        SetStatus (message.str ());
        PushProjectToBrowser ();
        return true;
    }

private:
    // Guids of the zones inside the current Archicad selection. Non-zone
    // elements in a mixed selection are skipped rather than rejected, so the
    // panel form still works when a zone is picked together with its walls.
    std::vector<API_Guid> GetSelectedZoneGuids () const
    {
        std::vector<API_Guid> zoneGuids;
        API_SelectionInfo selectionInfo {};
        GS::Array<API_Neig> selectedNeigs;
        const GSErrCode selectionError = ACAPI_Selection_Get (&selectionInfo, &selectedNeigs, false);
        BMKillHandle (reinterpret_cast<GSHandle*> (&selectionInfo.marquee.coords));
        if (selectionError != NoError) return zoneGuids;

        for (const API_Neig& neig : selectedNeigs) {
            API_Element element {};
            element.header.guid = neig.guid;
            if (ACAPI_Element_Get (&element) != NoError) continue;
            if (element.header.type.typeID != API_ZoneID) continue;
            zoneGuids.push_back (neig.guid);
        }
        return zoneGuids;
    }

    std::string SelectedZonesJson () const
    {
        nlohmann::json zones = nlohmann::json::array ();
        for (const API_Guid& zoneGuid : GetSelectedZoneGuids ()) {
            API_Element element {};
            element.header.guid = zoneGuid;
            if (ACAPI_Element_Get (&element) != NoError) continue;
            const GS::UniString unicodeName (element.zone.roomName);
            const GS::UniString unicodeNumber (element.zone.roomNoStr);
            zones.push_back ({
                {"guid", std::string (APIGuidToString (zoneGuid).ToCStr ().Get ())},
                {"name", std::string (unicodeName.ToCStr (0, MaxUSize, CC_UTF8).Get ())},
                {"number", std::string (unicodeNumber.ToCStr (0, MaxUSize, CC_UTF8).Get ())}
            });
        }
        const nlohmann::json response = {{"ok", true}, {"count", zones.size ()}, {"zones", zones}};
        return response.dump ();
    }

    // Writes the panel form's generated RH code into the Zone Name of every
    // selected zone, then re-runs the zone import so the tables update without
    // a separate "Zonları Aktar" click.
    bool ApplyZoneNameToSelection (const std::string& payload)
    {
        std::string zoneName;
        try {
            const nlohmann::json parsed = nlohmann::json::parse (payload);
            if (parsed.is_object () && parsed.contains ("name") && parsed.at ("name").is_string ())
                zoneName = parsed.at ("name").get<std::string> ();
        } catch (const std::exception& exception) {
            SetStatus ("Zon adı verisi okunamadı: " + std::string (exception.what ()));
            return false;
        }
        if (zoneName.empty ()) {
            SetStatus ("Zon adı boş olamaz.");
            return false;
        }

        const std::vector<API_Guid> zoneGuids = GetSelectedZoneGuids ();
        if (zoneGuids.empty ()) {
            SetStatus ("Önce Archicad'de en az bir zon seçin.");
            return false;
        }

        GS::UniString unicodeName (zoneName.c_str (), CC_UTF8);
        if (unicodeName.GetLength () >= API_UniLongNameLen)
            unicodeName = unicodeName.GetSubstring (0, API_UniLongNameLen - 1);

        std::size_t applied = 0;
        const GSErrCode commandError = ACAPI_CallUndoableCommand (GS::UniString ("Ruhsat Hesap zon adı", CC_UTF8), [&] () -> GSErrCode {
            for (const API_Guid& zoneGuid : zoneGuids) {
                API_Element element {};
                element.header.guid = zoneGuid;
                if (ACAPI_Element_Get (&element) != NoError) continue;

                API_Element mask {};
                ACAPI_ELEMENT_MASK_CLEAR (mask);
                ACAPI_ELEMENT_MASK_SET (mask, API_ZoneType, roomName);
                GS::ucscpy (element.zone.roomName, unicodeName.ToUStr ().Get ());
                if (ACAPI_Element_Change (&element, &mask, nullptr, 0, true) == NoError) ++applied;
            }
            return NoError;
        });

        if (applied == 0) {
            SetStatus (commandError == NoError
                ? "Zon adı yazılamadı. Zonlar kilitli veya rezerve edilmiş olabilir."
                : "Zon adı yazılamadı; Archicad komutu reddetti.");
            return false;
        }

        RefreshZonesFromArchicad (false);
        SetStatus (std::to_string (applied) + " zona \"" + zoneName + "\" adı yazıldı. " + lastStatusMessage);
        return true;
    }

    static GS::UniString LoadEmbeddedHtml ()
    {
        GSHandle data = RSLoadResource ('DATA', ACAPI_GetOwnResModule (), ID_WEB_APP_DATA);
        if (data == nullptr) return GS::UniString ("<html><body>Ruhsat Hesap panel kaynağı bulunamadı.</body></html>", CC_UTF8);
        const GSSize size = BMhGetSize (data);
        const std::string html (*data, *data + size);
        BMhKill (&data);
        return GS::UniString (html.c_str (), CC_UTF8);
    }

    static std::string GetStringFromJavaScript (const GS::Ref<JS::Base>& variable)
    {
        const GS::Ref<JS::Value> value = GS::DynamicCast<JS::Value> (variable);
        if (value == nullptr || value->GetType () != JS::Value::STRING) return {};
        return value->GetString ().ToCStr (0, MaxUSize, CC_UTF8).Get ();
    }

    static GS::Ref<JS::Base> ToJavaScriptString (const std::string& value)
    {
        return new JS::Value (GS::UniString (value.c_str (), CC_UTF8));
    }

    void InitBrowserControl ()
    {
        browser.LoadHTML (LoadEmbeddedHtml ());
        RegisterNativeBridge ();
    }

    void RegisterNativeBridge ()
    {
        JS::Object* native = new JS::Object ("RuhsatNative");

        native->AddItem (new JS::Function ("GetProjectData", [this] (GS::Ref<JS::Base>) {
            return ToJavaScriptString (MakeSnapshot (true, lastStatusMessage));
        }));

        native->AddItem (new JS::Function ("SaveProjectData", [this] (GS::Ref<JS::Base> argument) {
            try {
                RuhsatHesap::ProjectData updated = RuhsatHesap::DeserializeProjectText (GetStringFromJavaScript (argument));
                updated.sourceEnvelope = project.sourceEnvelope;
                if (updated.archicadStories.empty ()) updated.archicadStories = project.archicadStories;
                project = std::move (updated);
                RuhsatHesap::SyncStoriesToBlocks (project, false);
                const bool saved = PersistProjectToPln (false);
                SetStatus (saved ? "Panel değişiklikleri PLN proje verisine kaydedildi." : "Panel verisi güncellendi; PLN kaydı tamamlanamadı.");
                return ToJavaScriptString (MakeSnapshot (saved, lastStatusMessage));
            } catch (const std::exception& exception) {
                SetStatus ("Panel verisi okunamadı: " + std::string (exception.what ()));
                return ToJavaScriptString (MakeSnapshot (false, lastStatusMessage));
            }
        }));

        native->AddItem (new JS::Function ("ReadStories", [this] (GS::Ref<JS::Base>) {
            const bool success = RefreshStoriesFromArchicad (true);
            if (success) PersistProjectToPln (false);
            return ToJavaScriptString (MakeSnapshot (success, lastStatusMessage));
        }));

        native->AddItem (new JS::Function ("ReadZones", [this] (GS::Ref<JS::Base>) {
            const bool success = RefreshZonesFromArchicad (true);
            return ToJavaScriptString (MakeSnapshot (success, lastStatusMessage));
        }));

        native->AddItem (new JS::Function ("GetSelectedZones", [this] (GS::Ref<JS::Base>) {
            return ToJavaScriptString (SelectedZonesJson ());
        }));

        native->AddItem (new JS::Function ("ApplyZoneName", [this] (GS::Ref<JS::Base> argument) {
            const bool success = ApplyZoneNameToSelection (GetStringFromJavaScript (argument));
            return ToJavaScriptString (MakeSnapshot (success, lastStatusMessage));
        }));

        native->AddItem (new JS::Function ("SaveToPln", [this] (GS::Ref<JS::Base>) {
            const bool success = PersistProjectToPln (true);
            return ToJavaScriptString (MakeSnapshot (success, lastStatusMessage));
        }));

        native->AddItem (new JS::Function ("OpenJson", [this] (GS::Ref<JS::Base>) {
            const bool success = OpenJson ();
            return ToJavaScriptString (MakeSnapshot (success, lastStatusMessage));
        }));

        native->AddItem (new JS::Function ("SaveJson", [this] (GS::Ref<JS::Base>) {
            const bool success = SaveJson ();
            return ToJavaScriptString (MakeSnapshot (success, lastStatusMessage));
        }));

        native->AddItem (new JS::Function ("ExportReports", [this] (GS::Ref<JS::Base>) {
            const bool success = ExportReports ();
            return ToJavaScriptString (MakeSnapshot (success, lastStatusMessage));
        }));

        browser.RegisterAsynchJSObject (native);
    }

    std::string MakeSnapshot (bool ok, const std::string& message) const
    {
        const RuhsatHesap::CalculationSummary summary = RuhsatHesap::Calculate (project);
        const nlohmann::json response = {
            {"ok", ok},
            {"message", message},
            {"project", project},
            {"summary", {
                {"maxFootprint", summary.maxFootprint},
                {"maxEmsal", summary.maxEmsal},
                {"calculatedEmsal", summary.calculatedEmsal},
                {"emsalExcess", summary.emsalExcess},
                {"constructionArea", summary.constructionArea},
                {"retainingWallArea", summary.retainingWallArea},
                {"constructionGrandTotal", summary.constructionGrandTotal},
                {"requiredTrees", summary.requiredTrees},
                {"requiredParkingSpaces", summary.requiredParkingSpaces},
                {"unitCount", summary.unitCount}
            }}
        };
        return response.dump ();
    }

    void PushProjectToBrowser ()
    {
        if (!IsVisible ()) return;
        const std::string snapshot = MakeSnapshot (true, lastStatusMessage);
        const std::string script = "window.RuhsatApp && window.RuhsatApp.receiveNativeSnapshot(" +
            nlohmann::json (snapshot).dump () + ");";
        browser.ExecuteJS (GS::UniString (script.c_str (), CC_UTF8));
    }

    void SetStatus (const std::string& message)
    {
        lastStatusMessage = message;
        if (!IsVisible ()) return;
        const std::string script = "window.RuhsatApp && window.RuhsatApp.setNativeStatus(" +
            nlohmann::json (message).dump () + ");";
        browser.ExecuteJS (GS::UniString (script.c_str (), CC_UTF8));
    }

    void PanelResized (const DG::PanelResizeEvent& event) override
    {
        BeginMoveResizeItems ();
        browser.Resize (event.GetHorizontalChange (), event.GetVerticalChange ());
        EndMoveResizeItems ();
    }

    void PanelCloseRequested (const DG::PanelCloseRequestEvent& event, bool* accepted) override
    {
        if (event.GetSource () != this) return;
        HidePalette ();
        if (accepted != nullptr) *accepted = false;
    }

    bool OpenJson ()
    {
        const std::filesystem::path selected = SelectJsonFile (false);
        if (selected.empty ()) return false;

        try {
            project = RuhsatHesap::LoadProject (selected);
            currentPath = selected;
            RefreshZonesFromArchicad (false);
            PersistProjectToPln (false);
            SetStatus ("JSON projesi açıldı ve PLN proje verisi güncellendi.");
            PushProjectToBrowser ();
            return true;
        } catch (const std::exception& exception) {
            SetStatus ("JSON dosyası okunamadı: " + std::string (exception.what ()));
            return false;
        }
    }

    void LoadProjectFromPlnOnce ()
    {
        if (plnLoadAttempted) return;
        plnLoadAttempted = true;

        RuhsatHesap::ProjectData storedProject;
        const RuhsatHesap::PlnLoadResult result = RuhsatHesap::LoadProjectFromPln (storedProject);
        if (result.success && result.found) {
            project = std::move (storedProject);
            plnDataFound = true;
            SetStatus ("PLN içindeki Ruhsat Hesap proje verisi yüklendi.");
        } else if (!result.success) {
            lastPlnStoreError = result.error;
            SetStatus ("PLN proje verisi okunamadı: " + result.error);
        }
    }

    bool SaveJson ()
    {
        std::filesystem::path selected = currentPath;
        if (selected.empty ()) selected = SelectJsonFile (true);
        if (selected.empty ()) return false;

        try {
            RuhsatHesap::SaveProject (selected, project);
            currentPath = selected;
            SetStatus ("Proje JSON kaydı başarıyla kaydedildi.");
            return true;
        } catch (const std::exception& exception) {
            SetStatus ("JSON proje kaydı oluşturulamadı: " + std::string (exception.what ()));
            return false;
        }
    }

    bool ExportReports ()
    {
        RefreshZonesFromArchicad (false);
        PersistProjectToPln (false);
        const std::filesystem::path folder = SelectReportFolder ();
        if (folder.empty ()) return false;

        try {
            const RuhsatHesap::ReportExportPaths paths = RuhsatHesap::WriteProjectReports (folder, project);
            std::ostringstream message;
            message << "Excel ve pafta üretildi: " << paths.excelPath.filename ().string ()
                    << " + " << paths.sheetPngPath.filename ().string ();
            SetStatus (message.str ());
#if defined (WINDOWS)
            ShellExecuteW (nullptr, L"open", folder.c_str (), nullptr, nullptr, SW_SHOWNORMAL);
#endif
            return true;
        } catch (const std::exception& exception) {
            SetStatus ("Rapor çıktıları oluşturulamadı: " + std::string (exception.what ()));
            return false;
        }
    }

    DG::Browser browser;
    RuhsatHesap::ProjectData project;
    std::filesystem::path currentPath;
    bool plnLoadAttempted = false;
    bool plnDataFound = false;
    std::string lastPlnStoreError;
    std::string lastStatusMessage = "Ruhsat Hesap web paneli Archicad bağlantısını bekliyor.";
};

std::unique_ptr<RuhsatHesapPalette> palette;

RuhsatHesapPalette& GetPalette ()
{
    if (palette == nullptr) palette = std::make_unique<RuhsatHesapPalette> ();
    return *palette;
}

bool IsPaletteVisible ()
{
    return palette != nullptr && palette->IsVisible ();
}

GSErrCode ProjectEventHandler (API_NotifyEventID notificationId, Int32 parameter)
{
    constexpr Int32 FloorsEdited = 1;
    if (notificationId == APINotify_ChangeProjectDB && parameter == FloorsEdited && IsPaletteVisible ()) {
        palette->RefreshStoriesFromArchicad (false);
        palette->PersistProjectToPln (false);
    }
    return NoError;
}

GSErrCode PaletteControlCallBack (Int32 referenceId, API_PaletteMessageID messageId, GS::IntPtr param)
{
    static bool restoreAfterTemporaryHide = false;

    if (referenceId != PaletteReferenceId) return NoError;

    switch (messageId) {
        case APIPalMsg_OpenPalette:
            restoreAfterTemporaryHide = true;
            GetPalette ().ShowPalette ();
            break;

        case APIPalMsg_ClosePalette:
            restoreAfterTemporaryHide = false;
            if (palette != nullptr) palette->HidePalette ();
            break;

        case APIPalMsg_HidePalette_Begin: {
            const bool projectWasClosed = param != 0 && *reinterpret_cast<const bool*> (param);
            restoreAfterTemporaryHide = IsPaletteVisible ();
            if (projectWasClosed) {
                restoreAfterTemporaryHide = false;
                palette.reset ();
            } else if (palette != nullptr) {
                palette->HidePalette ();
            }
            break;
        }

        case APIPalMsg_HidePalette_End:
            if (restoreAfterTemporaryHide) GetPalette ().ShowPalette ();
            break;

        case APIPalMsg_DisableItems_Begin:
            if (palette != nullptr) palette->DisablePaletteItems ();
            break;

        case APIPalMsg_DisableItems_End:
            if (palette != nullptr) palette->EnablePaletteItems ();
            break;

        case APIPalMsg_IsPaletteVisible:
            if (param != 0) *reinterpret_cast<bool*> (param) = IsPaletteVisible ();
            break;

        case APIPalMsg_GetPaletteDeactivationMethod:
            if (param != 0) {
                *reinterpret_cast<API_PaletteDeactivationMethod*> (param) = APIPaletteDeactivationMethod_Default;
            }
            break;

        default:
            break;
    }

    return NoError;
}

GSErrCode MenuCommandHandler (const API_MenuParams* menuParams)
{
    if (menuParams->menuItemRef.menuResID == AddOnMenuID && menuParams->menuItemRef.itemIndex == AddOnCommandID) {
        GetPalette ().TogglePalette ();
    }
    return NoError;
}

} // namespace

API_AddonType CheckEnvironment (API_EnvirParams* environment)
{
    RSGetIndString (&environment->addOnInfo.name, AddOnInfoID, AddOnNameID, ACAPI_GetOwnResModule ());
    RSGetIndString (&environment->addOnInfo.description, AddOnInfoID, AddOnDescriptionID, ACAPI_GetOwnResModule ());
    return APIAddon_Preload;
}

GSErrCode RegisterInterface (void)
{
    return ACAPI_MenuItem_RegisterMenu (AddOnMenuID, 0, MenuCode_Tools, MenuFlag_Default);
}

GSErrCode Initialize (void)
{
    GSErrCode error = ACAPI_MenuItem_InstallMenuHandler (AddOnMenuID, MenuCommandHandler);
    if (error != NoError) return error;

    constexpr GSFlags PaletteFlags =
        API_PalEnabled_FloorPlan |
        API_PalEnabled_Section |
        API_PalEnabled_Elevation |
        API_PalEnabled_InteriorElevation |
        API_PalEnabled_3D |
        API_PalEnabled_Detail |
        API_PalEnabled_Worksheet |
        API_PalEnabled_Layout |
        API_PalEnabled_DocumentFrom3D |
        API_PalEnabled_ModelCompare;

    error = ACAPI_RegisterModelessWindow (
        PaletteReferenceId,
        PaletteControlCallBack,
        PaletteFlags,
        GSGuid2APIGuid (PaletteGuid)
    );
    if (error != NoError) return error;

    error = ACAPI_ProjectOperation_CatchProjectEvent (APINotify_ChangeProjectDB, ProjectEventHandler);
    if (error != NoError) ACAPI_UnregisterModelessWindow (PaletteReferenceId);
    return error;
}

GSErrCode FreeData (void)
{
    ACAPI_ProjectOperation_CatchProjectEvent (APINotify_ChangeProjectDB, nullptr);
    const GSErrCode error = ACAPI_UnregisterModelessWindow (PaletteReferenceId);
    palette.reset ();
    return error;
}
