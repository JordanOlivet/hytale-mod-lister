namespace HytaleModLister.Api.Models;

public class UrlOverrideStore
{
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, UrlOverride> Overrides { get; set; } = [];
}

public class UrlOverride
{
    public string CurseForgeUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record SetUrlOverrideRequest
{
    public required string CurseForgeUrl { get; init; }
}

public record UrlOverrideResponse
{
    public required string ModName { get; init; }
    public required string CurseForgeUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
