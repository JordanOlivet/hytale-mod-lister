using System.Security.Cryptography;
using System.Text;
using HytaleModLister.Api.Models;
using HytaleModLister.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HytaleModLister.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ISessionService sessionService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _sessionService = sessionService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Login with admin password
    /// </summary>
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        var adminPassword = _configuration["ADMIN_PASSWORD"]
            ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

        if (string.IsNullOrEmpty(adminPassword))
        {
            _logger.LogError("ADMIN_PASSWORD is not configured");
            return StatusCode(500, new { error = "Admin password not configured" });
        }

        // Use constant-time comparison to prevent timing attacks
        var passwordBytes = Encoding.UTF8.GetBytes(request.Password);
        var adminPasswordBytes = Encoding.UTF8.GetBytes(adminPassword);

        if (!CryptographicOperations.FixedTimeEquals(passwordBytes, adminPasswordBytes))
        {
            _logger.LogWarning("Failed login attempt");
            return Unauthorized(new { error = "Invalid password" });
        }

        var token = _sessionService.CreateSession();
        var expiresAt = DateTime.UtcNow.AddHours(24);

        _logger.LogInformation("Admin logged in successfully");

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt
        });
    }

    /// <summary>
    /// Verify if a token is valid
    /// </summary>
    [HttpPost("verify")]
    public ActionResult<VerifyResponse> Verify([FromHeader(Name = "Authorization")] string? authorization)
    {
        var token = ExtractToken(authorization);
        var valid = !string.IsNullOrEmpty(token) && _sessionService.ValidateSession(token);

        return Ok(new VerifyResponse { Valid = valid });
    }

    /// <summary>
    /// Logout and invalidate the token
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout([FromHeader(Name = "Authorization")] string? authorization)
    {
        var token = ExtractToken(authorization);

        if (!string.IsNullOrEmpty(token))
        {
            _sessionService.InvalidateSession(token);
            _logger.LogInformation("Admin logged out");
        }

        return Ok(new { message = "Logged out" });
    }

    private static string? ExtractToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization))
            return null;

        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorization[7..];

        return authorization;
    }
}
