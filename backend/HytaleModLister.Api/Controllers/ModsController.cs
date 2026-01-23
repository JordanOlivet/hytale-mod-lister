using HytaleModLister.Api.Models;
using HytaleModLister.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HytaleModLister.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModsController : ControllerBase
{
    private readonly IModRefreshService _refreshService;
    private readonly IModUpdateService _updateService;
    private readonly ISessionService _sessionService;
    private readonly IUrlOverrideService _urlOverrideService;
    private readonly ILogger<ModsController> _logger;

    public ModsController(
        IModRefreshService refreshService,
        IModUpdateService updateService,
        ISessionService sessionService,
        IUrlOverrideService urlOverrideService,
        ILogger<ModsController> logger)
    {
        _refreshService = refreshService;
        _updateService = updateService;
        _sessionService = sessionService;
        _urlOverrideService = urlOverrideService;
        _logger = logger;
    }

    /// <summary>
    /// Get the list of all mods with their information
    /// </summary>
    [HttpGet]
    public ActionResult<ModListResponse> GetMods()
    {
        var mods = _refreshService.GetCurrentMods();

        var response = new ModListResponse
        {
            LastUpdated = _refreshService.LastUpdated,
            TotalCount = mods.Count,
            Mods = mods.Select(m =>
            {
                // Apply URL overrides on-the-fly for immediate display
                var urlOverride = _urlOverrideService.GetOverride(m.Name);
                var curseForgeUrl = urlOverride?.CurseForgeUrl ?? m.CurseForgeUrl;
                var foundVia = urlOverride != null ? "override" : m.FoundVia;

                return new ModDto
                {
                    Name = m.Name,
                    FileName = m.FileName,
                    Version = m.Version,
                    Description = string.IsNullOrEmpty(m.Description) ? null : m.Description,
                    Authors = m.Authors,
                    Website = string.IsNullOrEmpty(m.Website) ? null : m.Website,
                    CurseForgeUrl = curseForgeUrl,
                    LatestCurseForgeVersion = m.LatestCurseForgeVersion,
                    FoundVia = foundVia
                };
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Trigger a manual refresh of the mods list
    /// </summary>
    /// <param name="force">If true, ignores the cache and refreshes all mods</param>
    [HttpPost("refresh")]
    public IActionResult RefreshMods([FromQuery] bool force = false)
    {
        if (_refreshService.IsRefreshing)
        {
            return Accepted(new { message = "Refresh already in progress" });
        }

        // Start refresh in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _refreshService.RefreshModsAsync(force);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual refresh");
            }
        });

        _logger.LogInformation("Manual refresh triggered (force={Force})", force);
        return Accepted(new { message = "Refresh started" });
    }

    /// <summary>
    /// Update a mod to its latest CurseForge version
    /// </summary>
    /// <param name="fileName">The current filename of the mod to update</param>
    /// <param name="authorization">Bearer token for admin authentication</param>
    /// <param name="skipRefresh">If true, skips the mods list refresh after update (for bulk updates)</param>
    [HttpPost("{fileName}/update")]
    public async Task<ActionResult<UpdateModResponse>> UpdateMod(
        string fileName,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] bool skipRefresh = false)
    {
        // 1. Validate the admin session
        var token = ExtractBearerToken(authorization);
        if (string.IsNullOrEmpty(token) || !_sessionService.ValidateSession(token))
        {
            _logger.LogWarning("Unauthorized update attempt for mod: {FileName}", fileName);
            return Unauthorized(new UpdateModResponse(false, "Unauthorized"));
        }

        // 2. Call the update service
        _logger.LogInformation("Admin requested update for mod: {FileName}", fileName);
        var result = await _updateService.UpdateModAsync(fileName, skipRefresh);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get the URL override for a specific mod
    /// </summary>
    /// <param name="modName">The name of the mod</param>
    [HttpGet("{modName}/override")]
    public ActionResult<UrlOverrideResponse> GetUrlOverride(string modName)
    {
        var urlOverride = _urlOverrideService.GetOverride(modName);
        if (urlOverride == null)
        {
            return NotFound(new { message = "No override found for this mod" });
        }

        return Ok(new UrlOverrideResponse
        {
            ModName = modName,
            CurseForgeUrl = urlOverride.CurseForgeUrl,
            CreatedAt = urlOverride.CreatedAt,
            UpdatedAt = urlOverride.UpdatedAt
        });
    }

    /// <summary>
    /// Set or update the URL override for a specific mod
    /// </summary>
    /// <param name="modName">The name of the mod</param>
    /// <param name="request">The override URL request</param>
    /// <param name="authorization">Bearer token for admin authentication</param>
    [HttpPut("{modName}/override")]
    public ActionResult<UrlOverrideResponse> SetUrlOverride(
        string modName,
        [FromBody] SetUrlOverrideRequest request,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        // Validate admin session
        var token = ExtractBearerToken(authorization);
        if (string.IsNullOrEmpty(token) || !_sessionService.ValidateSession(token))
        {
            _logger.LogWarning("Unauthorized URL override attempt for mod: {ModName}", modName);
            return Unauthorized(new { message = "Unauthorized" });
        }

        // Validate URL format
        if (string.IsNullOrWhiteSpace(request.CurseForgeUrl) ||
            !request.CurseForgeUrl.Contains("curseforge.com/hytale/mods/"))
        {
            return BadRequest(new { message = "Invalid CurseForge URL. Must contain 'curseforge.com/hytale/mods/'" });
        }

        _urlOverrideService.SetOverride(modName, request.CurseForgeUrl);
        _logger.LogInformation("Admin set URL override for mod: {ModName} -> {Url}", modName, request.CurseForgeUrl);

        var urlOverride = _urlOverrideService.GetOverride(modName)!;
        return Ok(new UrlOverrideResponse
        {
            ModName = modName,
            CurseForgeUrl = urlOverride.CurseForgeUrl,
            CreatedAt = urlOverride.CreatedAt,
            UpdatedAt = urlOverride.UpdatedAt
        });
    }

    /// <summary>
    /// Delete the URL override for a specific mod
    /// </summary>
    /// <param name="modName">The name of the mod</param>
    /// <param name="authorization">Bearer token for admin authentication</param>
    [HttpDelete("{modName}/override")]
    public IActionResult DeleteUrlOverride(
        string modName,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        // Validate admin session
        var token = ExtractBearerToken(authorization);
        if (string.IsNullOrEmpty(token) || !_sessionService.ValidateSession(token))
        {
            _logger.LogWarning("Unauthorized URL override delete attempt for mod: {ModName}", modName);
            return Unauthorized(new { message = "Unauthorized" });
        }

        _urlOverrideService.DeleteOverride(modName);
        _logger.LogInformation("Admin deleted URL override for mod: {ModName}", modName);

        return Ok(new { message = "Override deleted successfully" });
    }

    private static string? ExtractBearerToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization))
            return null;

        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorization[7..];

        return authorization;
    }
}

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IModRefreshService _refreshService;
    private readonly RefreshSchedulerService _scheduler;

    public StatusController(IModRefreshService refreshService, RefreshSchedulerService scheduler)
    {
        _refreshService = refreshService;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Get the current status of the service
    /// </summary>
    [HttpGet]
    public ActionResult<StatusResponse> GetStatus()
    {
        var response = new StatusResponse
        {
            LastUpdated = _refreshService.LastUpdated,
            ModCount = _refreshService.GetCurrentMods().Count,
            IsRefreshing = _refreshService.IsRefreshing,
            Progress = _refreshService.CurrentProgress,
            NextScheduledRefresh = _scheduler.NextScheduledRefresh
        };

        return Ok(response);
    }
}

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Health check endpoint for Docker/monitoring
    /// </summary>
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
