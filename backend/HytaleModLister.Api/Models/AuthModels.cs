namespace HytaleModLister.Api.Models;

public record LoginRequest
{
    public required string Password { get; init; }
}

public record LoginResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public record VerifyResponse
{
    public required bool Valid { get; init; }
}
