using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public interface IUrlOverrideService
{
    UrlOverride? GetOverride(string modName);
    void SetOverride(string modName, string url);
    void DeleteOverride(string modName);
    Dictionary<string, UrlOverride> GetAllOverrides();
}
