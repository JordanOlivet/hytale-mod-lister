using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public interface ICurseForgeService
{
    Task<List<CfMod>> SearchModsAsync(string searchTerm);
    Task<List<CfMod>> GetModsBatchAsync(int offset, int pageSize = 50);
}
