using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public interface IModRefreshService
{
    Task<List<ModInfo>> RefreshModsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
    List<ModInfo> GetCurrentMods();
    bool IsRefreshing { get; }
    RefreshProgress? CurrentProgress { get; }
    DateTime? LastUpdated { get; }
}
