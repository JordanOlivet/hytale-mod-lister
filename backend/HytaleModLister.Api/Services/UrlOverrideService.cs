using System.Text.Json;
using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public class UrlOverrideService : IUrlOverrideService
{
    private readonly ILogger<UrlOverrideService> _logger;
    private readonly IConfiguration _configuration;
    private readonly object _lock = new();
    private UrlOverrideStore _store = new();
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public UrlOverrideService(ILogger<UrlOverrideService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private string OverridesPath => _configuration["OverridesPath"] ?? "/app/data/url_overrides.json";

    private UrlOverrideStore LoadStore()
    {
        lock (_lock)
        {
            if (_loaded) return _store;

            if (File.Exists(OverridesPath))
            {
                try
                {
                    var json = File.ReadAllText(OverridesPath);
                    _store = JsonSerializer.Deserialize<UrlOverrideStore>(json, JsonOpts) ?? new UrlOverrideStore();
                    _logger.LogInformation("URL overrides loaded with {Count} entries", _store.Overrides.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading URL overrides, starting fresh");
                    _store = new UrlOverrideStore();
                }
            }

            _loaded = true;
            return _store;
        }
    }

    private void SaveStore()
    {
        lock (_lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(OverridesPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _store.LastUpdated = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(_store, JsonOpts);
                File.WriteAllText(OverridesPath, json);
                _logger.LogInformation("URL overrides saved with {Count} entries", _store.Overrides.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving URL overrides");
            }
        }
    }

    public UrlOverride? GetOverride(string modName)
    {
        var store = LoadStore();
        return store.Overrides.GetValueOrDefault(modName);
    }

    public void SetOverride(string modName, string url)
    {
        lock (_lock)
        {
            var store = LoadStore();
            var existing = store.Overrides.GetValueOrDefault(modName);

            if (existing != null)
            {
                existing.CurseForgeUrl = url;
                existing.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Updated URL override for mod: {ModName}", modName);
            }
            else
            {
                store.Overrides[modName] = new UrlOverride
                {
                    CurseForgeUrl = url,
                    CreatedAt = DateTime.UtcNow
                };
                _logger.LogInformation("Created URL override for mod: {ModName}", modName);
            }

            SaveStore();
        }
    }

    public void DeleteOverride(string modName)
    {
        lock (_lock)
        {
            var store = LoadStore();
            if (store.Overrides.Remove(modName))
            {
                SaveStore();
                _logger.LogInformation("Deleted URL override for mod: {ModName}", modName);
            }
        }
    }

    public Dictionary<string, UrlOverride> GetAllOverrides()
    {
        var store = LoadStore();
        return store.Overrides;
    }
}
