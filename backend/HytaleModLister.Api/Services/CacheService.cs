using System.Text.Json;
using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public class CacheService : ICacheService
{
    private readonly ILogger<CacheService> _logger;
    private readonly IConfiguration _configuration;
    private readonly object _lock = new();
    private ModCache _cache = new();
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public CacheService(ILogger<CacheService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private string CachePath => _configuration["CachePath"] ?? "/app/data/mods_cache.json";
    private int CacheValidityDays => _configuration.GetValue("Cache:ValidityDays", 7);

    public ModCache LoadCache()
    {
        lock (_lock)
        {
            if (_loaded) return _cache;

            if (File.Exists(CachePath))
            {
                try
                {
                    var json = File.ReadAllText(CachePath);
                    _cache = JsonSerializer.Deserialize<ModCache>(json, JsonOpts) ?? new ModCache();
                    _logger.LogInformation("Cache loaded with {Count} mods", _cache.Mods.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading cache, starting fresh");
                    _cache = new ModCache();
                }
            }

            _loaded = true;
            return _cache;
        }
    }

    public void SaveCache(ModCache cache)
    {
        lock (_lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(CachePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                cache.LastUpdated = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(cache, JsonOpts);
                File.WriteAllText(CachePath, json);
                _cache = cache;
                _logger.LogInformation("Cache saved with {Count} mods", cache.Mods.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cache");
            }
        }
    }

    public CachedMod? GetCachedMod(string modName)
    {
        var cache = LoadCache();
        return cache.Mods.GetValueOrDefault(modName);
    }

    public void CacheMod(string modName, string? url, bool notFound = false)
    {
        lock (_lock)
        {
            var cache = LoadCache();
            cache.Mods[modName] = new CachedMod
            {
                CurseForgeUrl = url,
                NotFound = notFound,
                CachedAt = DateTime.UtcNow
            };
            SaveCache(cache);
        }
    }

    public bool IsCacheValid(CachedMod cached)
    {
        return cached.CachedAt > DateTime.UtcNow.AddDays(-CacheValidityDays);
    }

    public DateTime? GetLastUpdated()
    {
        var cache = LoadCache();
        return cache.LastUpdated == default ? null : cache.LastUpdated;
    }
}
