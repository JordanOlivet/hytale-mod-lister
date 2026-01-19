namespace HytaleModLister.Api.Models;

public class ModInfo
{
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string Website { get; set; } = "";
    public List<string> Authors { get; set; } = [];
    public string? CurseForgeUrl { get; set; }
    public string? FoundVia { get; set; }
}

public record ModDto
{
    public required string Name { get; init; }
    public required string FileName { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public required List<string> Authors { get; init; }
    public string? Website { get; init; }
    public string? CurseForgeUrl { get; init; }
    public string? FoundVia { get; init; }
}

public record ModListResponse
{
    public DateTime? LastUpdated { get; init; }
    public int TotalCount { get; init; }
    public required List<ModDto> Mods { get; init; }
}
