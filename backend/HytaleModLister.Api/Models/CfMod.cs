namespace HytaleModLister.Api.Models;

public class CfMod
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Url { get; set; } = "";
    public List<string> Authors { get; set; } = [];
    public string? LatestVersion { get; set; }
}

public record MatchResult(string Url, string MatchType, string? LatestVersion);

// CurseForge API response models
public class CfResponse
{
    public List<CfModData>? Data { get; set; }
}

public class CfModData
{
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public CfLinks? Links { get; set; }
    public List<CfAuthor>? Authors { get; set; }
    public List<CfFile>? LatestFiles { get; set; }
}

public class CfFile
{
    public int Id { get; set; }
    public string? DisplayName { get; set; }
    public string? FileName { get; set; }
    public DateTime? FileDate { get; set; }
}

public class CfLinks
{
    public string? WebsiteUrl { get; set; }
}

public class CfAuthor
{
    public string? Name { get; set; }
}
