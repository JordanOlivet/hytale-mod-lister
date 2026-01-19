namespace HytaleModLister.Api.Models;

public class CfMod
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Url { get; set; } = "";
    public List<string> Authors { get; set; } = [];
}

public record MatchResult(string Url, string MatchType);

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
}

public class CfLinks
{
    public string? WebsiteUrl { get; set; }
}

public class CfAuthor
{
    public string? Name { get; set; }
}
