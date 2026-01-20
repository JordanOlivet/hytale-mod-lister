namespace HytaleModLister.Api.Services;

public interface ISessionService
{
    string CreateSession();
    bool ValidateSession(string token);
    void InvalidateSession(string token);
}
