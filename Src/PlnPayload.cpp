#include "PlnPayload.hpp"

#include <stdexcept>

namespace RuhsatHesap {

std::string SerializeProjectForPln (const ProjectData& project)
{
    nlohmann::json payload = {
        {"format", "ruhsat-hesap-pln"},
        {"storageVersion", PlnStorageVersion},
        {"project", nlohmann::json (project)}
    };
    if (!project.sourceEnvelope.is_null () && !project.sourceEnvelope.empty ())
        payload["sourceEnvelope"] = project.sourceEnvelope;
    return payload.dump ();
}

ProjectData DeserializeProjectFromPln (const std::string& payload)
{
    const nlohmann::json json = nlohmann::json::parse (payload);
    if (!json.is_object () || json.value ("format", std::string {}) != "ruhsat-hesap-pln")
        throw std::runtime_error ("Unsupported Ruhsat Hesap PLN payload.");
    if (!json.contains ("project") || !json.at ("project").is_object ())
        throw std::runtime_error ("Ruhsat Hesap PLN payload has no project object.");

    const int storageVersion = json.value ("storageVersion", 0);
    if (storageVersion < 1 || storageVersion > PlnStorageVersion)
        throw std::runtime_error ("Unsupported Ruhsat Hesap PLN storage version.");

    ProjectData project = json.at ("project").get<ProjectData> ();
    if (json.contains ("sourceEnvelope") && json.at ("sourceEnvelope").is_object ())
        project.sourceEnvelope = json.at ("sourceEnvelope");
    return project;
}

} // namespace RuhsatHesap
