namespace HytaleModLister.Api.Models;

public class ManifestJson
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Website { get; set; }
    public List<ManifestAuthor>? Authors { get; set; }
}

public class ManifestAuthor
{
    public string? Name { get; set; }
}
