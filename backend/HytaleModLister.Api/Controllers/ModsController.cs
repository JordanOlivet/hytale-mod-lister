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
    private readonly ILogger<ModsController> _logger;

    public ModsController(
        IModRefreshService refreshService,
        IModUpdateService updateService,
        ISessionService sessionService,
        ILogger<ModsController> logger)
    {
        _refreshService = refreshService;
        _updateService = updateService;
        _sessionService = sessionService;
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
            Mods = mods.Select(m => new ModDto
            {
                Name = m.Name,
                FileName = m.FileName,
                Version = m.Version,
                Description = string.IsNullOrEmpty(m.Description) ? null : m.Description,
                Authors = m.Authors,
                Website = string.IsNullOrEmpty(m.Website) ? null : m.Website,
                CurseForgeUrl = m.CurseForgeUrl,
                LatestCurseForgeVersion = m.LatestCurseForgeVersion,
                FoundVia = m.FoundVia
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
