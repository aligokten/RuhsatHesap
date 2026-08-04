#include "APIEnvir.h"
#include "ACAPinc.h"

#include "PlnPayload.hpp"
#include "PlnProjectStore.hpp"

#include <cstring>
#include <exception>
#include <stdexcept>
#include <string>

namespace RuhsatHesap {

namespace {

std::string ErrorText (const char* operation, GSErrCode error)
{
    return std::string (operation) + " (Archicad hata kodu: " + std::to_string (error) + ")";
}

GSErrCode FindProjectObject (API_Guid& objectGuid, bool& isUnique)
{
    objectGuid = APINULLGuid;
    isUnique = true;
    const GS::UniString objectName (PlnProjectObjectName, CC_UTF8);

    GSErrCode error = ACAPI_AddOnObject_GetUniqueObjectGuidFromName (objectName, &objectGuid);
    if (error != NoError) return error;
    if (objectGuid != APINULLGuid) return NoError;

    // Compatibility fallback for projects written by an early/general-object
    // build. New records are always created as Unique Add-On Objects.
    isUnique = false;
    return ACAPI_AddOnObject_GetObjectGuidFromName (objectName, &objectGuid);
}

GSHandle MakeContentHandle (const std::string& payload)
{
    if (payload.empty ()) return nullptr;
    GSHandle content = BMAllocateHandle (static_cast<GSSize> (payload.size ()), ALLOCATE_CLEAR, 0);
    if (content == nullptr || *content == nullptr) {
        if (content != nullptr) BMKillHandle (&content);
        return nullptr;
    }
    std::memcpy (*content, payload.data (), payload.size ());
    return content;
}

GSErrCode ModifyWithTeamworkReservation (const API_Guid& objectGuid, const GSHandle& content)
{
    GSErrCode error = ACAPI_AddOnObject_ModifyObject (objectGuid, nullptr, &content);
    if (error != APIERR_NOTMINE) return error;

    GS::Array<API_Guid> objects;
    objects.Push (objectGuid);
    GS::HashTable<API_Guid, short> conflicts;
    error = ACAPI_AddOnObject_ReserveObjects (objects, &conflicts);
    if (error != NoError || !conflicts.IsEmpty ()) return error != NoError ? error : APIERR_NOTMINE;

    error = ACAPI_AddOnObject_ModifyObject (objectGuid, nullptr, &content);
    const GSErrCode releaseError = ACAPI_AddOnObject_ReleaseObjects (objects);
    return error != NoError ? error : releaseError;
}

} // namespace

PlnLoadResult LoadProjectFromPln (ProjectData& project)
{
    PlnLoadResult result;
    API_Guid objectGuid = APINULLGuid;
    bool isUnique = true;
    const GSErrCode findError = FindProjectObject (objectGuid, isUnique);
    (void) isUnique;
    if (findError != NoError) {
        result.error = ErrorText ("PLN proje verisi aranamadı", findError);
        return result;
    }
    if (objectGuid == APINULLGuid) {
        result.success = true;
        return result;
    }

    GS::UniString objectName;
    GSHandle content = nullptr;
    const GSErrCode readError = ACAPI_AddOnObject_GetObjectContent (objectGuid, &objectName, &content);
    if (readError != NoError) {
        if (content != nullptr) BMKillHandle (&content);
        result.error = ErrorText ("PLN proje verisi okunamadı", readError);
        return result;
    }

    try {
        const GSSize contentSize = BMGetHandleSize (content);
        if (content == nullptr || *content == nullptr || contentSize == 0)
            throw std::runtime_error ("PLN proje verisi boş.");
        project = DeserializeProjectFromPln (std::string (*content, *content + contentSize));
        result.success = true;
        result.found = true;
    } catch (const std::exception& exception) {
        result.error = std::string ("PLN proje verisi çözümlenemedi: ") + exception.what ();
    }

    BMKillHandle (&content);
    return result;
}

PlnSaveResult SaveProjectToPln (const ProjectData& project)
{
    PlnSaveResult result;
    std::string payload;
    try {
        payload = SerializeProjectForPln (project);
    } catch (const std::exception& exception) {
        result.error = std::string ("PLN proje verisi oluşturulamadı: ") + exception.what ();
        return result;
    }
    GSHandle content = MakeContentHandle (payload);
    if (content == nullptr) {
        result.error = "PLN proje verisi için bellek ayrılamadı.";
        return result;
    }

    API_Guid objectGuid = APINULLGuid;
    bool isUnique = true;
    GSErrCode error = FindProjectObject (objectGuid, isUnique);
    (void) isUnique;
    if (error != NoError) {
        BMKillHandle (&content);
        result.error = ErrorText ("PLN proje verisi aranamadı", error);
        return result;
    }

    if (objectGuid == APINULLGuid) {
        const GS::UniString objectName (PlnProjectObjectName, CC_UTF8);
        error = ACAPI_AddOnObject_CreateUniqueObject (objectName, &objectGuid);
        if (error == NoError) {
            result.created = true;
            error = ACAPI_AddOnObject_ModifyObject (objectGuid, nullptr, &content);
            if (error != NoError) ACAPI_AddOnObject_DeleteObject (objectGuid);
        }
    } else {
        error = ModifyWithTeamworkReservation (objectGuid, content);
    }

    BMKillHandle (&content);
    if (error != NoError) {
        result.error = ErrorText ("PLN proje verisi kaydedilemedi", error);
        return result;
    }

    result.success = true;
    return result;
}

} // namespace RuhsatHesap
