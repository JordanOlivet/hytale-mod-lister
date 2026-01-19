using System.Net.Http.Json;
using System.Text.Json;
using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public class CurseForgeService : ICurseForgeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CurseForgeService> _logger;
    private readonly IConfiguration _configuration;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private const string ApiBaseUrl = "https://api.curseforge.com/v1";

    public CurseForgeService(HttpClient httpClient, ILogger<CurseForgeService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        var apiKey = _configuration["CurseForge:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _logger.LogInformation("CurseForge API key configured");
        }
    }

    private int GameId => _configuration.GetValue("CurseForge:GameId", 70216);

    public async Task<List<CfMod>> SearchModsAsync(string searchTerm)
    {
        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={GameId}&searchFilter={Uri.EscapeDataString(searchTerm)}&pageSize=50";
            var response = await _httpClient.GetFromJsonAsync<CfResponse>(url, JsonOpts);
            return ParseCfMods(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching mods for term: {SearchTerm}", searchTerm);
            return [];
        }
    }

    public async Task<List<CfMod>> GetModsBatchAsync(int offset, int pageSize = 50)
    {
        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={GameId}&pageSize={pageSize}&index={offset}";
            var response = await _httpClient.GetFromJsonAsync<CfResponse>(url, JsonOpts);
            return ParseCfMods(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching mods batch at offset {Offset}", offset);
            return [];
        }
    }

    private List<CfMod> ParseCfMods(CfResponse? response)
    {
        if (response?.Data == null) return [];

        return response.Data
            .Where(m => m.Links?.WebsiteUrl?.Contains("/mods/") == true)
            .Select(m => new CfMod
            {
                Name = m.Name ?? "",
                Slug = m.Slug ?? "",
                Url = m.Links!.WebsiteUrl!,
                Authors = m.Authors?.Select(a => a.Name ?? "").ToList() ?? []
            }).ToList();
    }
}
