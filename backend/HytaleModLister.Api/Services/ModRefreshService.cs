using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public class ModRefreshService : IModRefreshService
{
    private readonly IModExtractorService _extractor;
    private readonly ICurseForgeService _curseForge;
    private readonly IModMatcherService _matcher;
    private readonly ICacheService _cache;
    private readonly IUrlOverrideService _urlOverride;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ModRefreshService> _logger;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private List<ModInfo> _currentMods = [];
    private RefreshProgress? _progress;
    private DateTime? _lastUpdated;

    public bool IsRefreshing { get; private set; }
    public RefreshProgress? CurrentProgress => _progress;
    public DateTime? LastUpdated => _lastUpdated ?? _cache.GetLastUpdated();

    public ModRefreshService(
        IModExtractorService extractor,
        ICurseForgeService curseForge,
        IModMatcherService matcher,
        ICacheService cache,
        IUrlOverrideService urlOverride,
        IConfiguration configuration,
        ILogger<ModRefreshService> logger)
    {
        _extractor = extractor;
        _curseForge = curseForge;
        _matcher = matcher;
        _cache = cache;
        _urlOverride = urlOverride;
        _configuration = configuration;
        _logger = logger;
    }

    private string ModsPath => _configuration["ModsPath"] ?? "/app/mods";
    private int RateLimitMs => _configuration.GetValue("CurseForge:RateLimitMs", 350);

    public List<ModInfo> GetCurrentMods() => _currentMods;

    public async Task<List<ModInfo>> RefreshModsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!await _refreshLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Refresh already in progress, skipping");
            return _currentMods;
        }

        try
        {
            IsRefreshing = true;
            _logger.LogInformation("Starting mod refresh (force={Force})", forceRefresh);

            // Extract mods from files
            var mods = _extractor.ExtractMods(ModsPath);
            _logger.LogInformation("Extracted {Count} mods from files", mods.Count);

            // Apply URL overrides first (highest priority)
            foreach (var mod in mods)
            {
                var urlOverride = _urlOverride.GetOverride(mod.Name);
                if (urlOverride != null)
                {
                    mod.CurseForgeUrl = urlOverride.CurseForgeUrl;
                    mod.FoundVia = "override";
                    _logger.LogInformation("Applied URL override for mod: {ModName}", mod.Name);
                }
            }

            // Find mods that need URL lookup (excluding those with overrides)
            var toFind = mods.Where(m => string.IsNullOrEmpty(m.CurseForgeUrl)).ToList();

            // Check cache first (unless force refresh)
            if (!forceRefresh)
            {
                foreach (var mod in toFind.ToList())
                {
                    var cached = _cache.GetCachedMod(mod.Name);
                    if (cached != null && _cache.IsCacheValid(cached) && !string.IsNullOrEmpty(cached.CurseForgeUrl))
                    {
                        mod.CurseForgeUrl = cached.CurseForgeUrl;
                        mod.LatestCurseForgeVersion = cached.LatestVersion;
                        mod.FoundVia = "cache";
                        toFind.Remove(mod);
                    }
                }
                _logger.LogInformation("{Count} mods found in cache, {Remaining} to search",
                    mods.Count - toFind.Count - mods.Count(m => m.FoundVia == "manifest"), toFind.Count);
            }

            if (toFind.Count > 0)
            {
                await SearchModsViaApiAsync(mods, toFind, cancellationToken);
            }

            _currentMods = mods;
            _lastUpdated = DateTime.UtcNow;
            _logger.LogInformation("Refresh completed: {Total} mods, {Found} with URLs",
                mods.Count, mods.Count(m => !string.IsNullOrEmpty(m.CurseForgeUrl)));

            return mods;
        }
        finally
        {
            IsRefreshing = false;
            _progress = null;
            _refreshLock.Release();
        }
    }

    private async Task SearchModsViaApiAsync(List<ModInfo> allMods, List<ModInfo> toFind, CancellationToken cancellationToken)
    {
        _progress = new RefreshProgress { Total = toFind.Count, Processed = 0 };

        // Strategy 1: Search by author
        var authors = toFind.SelectMany(m => m.Authors).Distinct().Where(a => a != "Unknown").ToList();
        _logger.LogInformation("Strategy 1: Searching by {Count} authors", authors.Count);

        foreach (var author in authors)
        {
            if (toFind.Count == 0 || cancellationToken.IsCancellationRequested) break;

            var authorMods = await _curseForge.SearchModsAsync(author);
            var authorLocalMods = toFind.Where(m => m.Authors.Contains(author)).ToList();

            foreach (var mod in authorLocalMods)
            {
                _progress = _progress with { CurrentMod = mod.Name };
                var match = _matcher.FindBestMatch(mod.Name, authorMods.Where(cf => cf.Authors.Contains(author)));
                if (match != null)
                {
                    mod.CurseForgeUrl = match.Url;
                    mod.LatestCurseForgeVersion = match.LatestVersion;
                    mod.FoundVia = match.MatchType;
                    _cache.CacheMod(mod.Name, match.Url, match.LatestVersion);
                    toFind.Remove(mod);
                    _progress = _progress with { Processed = _progress.Processed + 1 };
                    _logger.LogInformation("Found {Mod} via author search: {Url} (v{Version})", mod.Name, match.Url, match.LatestVersion ?? "unknown");
                }
            }

            await Task.Delay(RateLimitMs, cancellationToken);
        }

        if (toFind.Count == 0) return;

        // Strategy 2: Global batch search
        _logger.LogInformation("Strategy 2: Global batch search for {Count} remaining mods", toFind.Count);

        int offset = 0;
        const int batchSize = 50;

        while (toFind.Count > 0 && offset < 10000)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var cfMods = await _curseForge.GetModsBatchAsync(offset, batchSize);
            if (cfMods.Count == 0) break;

            foreach (var mod in toFind.ToList())
            {
                _progress = _progress with { CurrentMod = mod.Name };
                var match = _matcher.FindBestMatch(mod.Name, cfMods);
                if (match != null)
                {
                    mod.CurseForgeUrl = match.Url;
                    mod.LatestCurseForgeVersion = match.LatestVersion;
                    mod.FoundVia = match.MatchType;
                    _cache.CacheMod(mod.Name, match.Url, match.LatestVersion);
                    toFind.Remove(mod);
                    _progress = _progress with { Processed = _progress.Processed + 1 };
                    _logger.LogInformation("Found {Mod} via batch search: {Url} (v{Version})", mod.Name, match.Url, match.LatestVersion ?? "unknown");
                }
            }

            offset += batchSize;
            await Task.Delay(RateLimitMs, cancellationToken);
        }

        // Mark unfound mods in cache
        foreach (var mod in toFind)
        {
            _cache.CacheMod(mod.Name, null, notFound: true);
            _logger.LogWarning("Mod not found: {Name}", mod.Name);
        }
    }
}
