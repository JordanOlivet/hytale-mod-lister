namespace HytaleModLister.Api.Models;

public record StatusResponse
{
    public DateTime? LastUpdated { get; init; }
    public int ModCount { get; init; }
    public bool IsRefreshing { get; init; }
    public RefreshProgress? Progress { get; init; }
    public DateTime? NextScheduledRefresh { get; init; }
}

public record RefreshProgress
{
    public int Processed { get; init; }
    public int Total { get; init; }
    public string? CurrentMod { get; init; }
}
