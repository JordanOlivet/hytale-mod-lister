using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public interface ICacheService
{
    ModCache LoadCache();
    void SaveCache(ModCache cache);
    CachedMod? GetCachedMod(string modName);
    void CacheMod(string modName, string? url, bool notFound = false);
    bool IsCacheValid(CachedMod cached);
    DateTime? GetLastUpdated();
}
