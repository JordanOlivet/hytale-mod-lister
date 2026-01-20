using System.Collections.Concurrent;

namespace HytaleModLister.Api.Services;

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, DateTime> _sessions = new();
    private readonly TimeSpan _sessionDuration = TimeSpan.FromHours(24);
    private readonly ILogger<SessionService> _logger;
    private readonly Timer _cleanupTimer;

    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
        // Cleanup expired sessions every hour
        _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public string CreateSession()
    {
        var token = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.Add(_sessionDuration);
        _sessions[token] = expiresAt;
        _logger.LogInformation("Created new session, expires at {ExpiresAt}", expiresAt);
        return token;
    }

    public bool ValidateSession(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        if (_sessions.TryGetValue(token, out var expiresAt))
        {
            if (DateTime.UtcNow < expiresAt)
            {
                return true;
            }
            // Session expired, remove it
            _sessions.TryRemove(token, out _);
        }
        return false;
    }

    public void InvalidateSession(string token)
    {
        if (_sessions.TryRemove(token, out _))
        {
            _logger.LogInformation("Session invalidated");
        }
    }

    public DateTime GetSessionExpiration()
    {
        return DateTime.UtcNow.Add(_sessionDuration);
    }

    private void CleanupExpiredSessions(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredCount = 0;

        foreach (var kvp in _sessions)
        {
            if (now >= kvp.Value)
            {
                if (_sessions.TryRemove(kvp.Key, out _))
                {
                    expiredCount++;
                }
            }
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired sessions", expiredCount);
        }
    }
}
