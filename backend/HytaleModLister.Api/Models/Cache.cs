namespace HytaleModLister.Api.Models;

public class ModCache
{
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, CachedMod> Mods { get; set; } = [];
}

public class CachedMod
{
    public string? CurseForgeUrl { get; set; }
    public bool NotFound { get; set; }
    public DateTime CachedAt { get; set; }
}
