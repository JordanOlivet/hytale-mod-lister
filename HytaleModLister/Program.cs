using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Fastenshtein;

namespace HytaleModLister;

class Program
{
    const int HytaleGameId = 70216;
    const int BatchSize = 50;
    const int CacheValidityDays = 7;
    const string ApiBaseUrl = "https://api.curseforge.com/v1";

    static readonly HttpClient Http = new();
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    static string ScriptDir = "";
    static string? ApiKey;
    static bool DryRun, ForceRefresh;
    static ModCache Cache = new();

    static async Task<int> Main(string[] args)
    {
        ScriptDir = AppContext.BaseDirectory;
        var modsPath = Path.Combine(ScriptDir, "mods");

#if DEBUG
        // En debug, remonter au dossier parent si on est dans bin/Debug/...
        if (!Directory.Exists(modsPath))
        {
            ScriptDir = Path.GetFullPath(Path.Combine(ScriptDir, "..", "..", "..", ".."));
            modsPath = Path.Combine(ScriptDir, "mods");
        }
#endif

        if (!ParseArgs(args)) return 1;

        LoadApiKey();
        if (ApiKey != null) LoadCache();

        if (!Directory.Exists(modsPath))
        {
            Log("ERROR", $"Dossier mods introuvable: {modsPath}", ConsoleColor.Red);
            return 1;
        }

        var mods = ExtractMods(modsPath);
        Log("OK", $"{mods.Count} mod(s) valide(s) extraits");

        if (ApiKey != null && !DryRun)
            await SearchModsViaApi(mods);

        GenerateMarkdown(mods);
        SaveCache();
        Log("OK", "Termine !");
        return 0;
    }

    static bool ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--help":
                    Console.WriteLine("""
                        Usage: HytaleModLister [OPTIONS]

                        OPTIONS:
                            --help           Affiche cette aide
                            --dry-run        Mode test (n'ecrit pas de fichier)
                            --force-refresh  Ignore le cache

                        CONFIGURATION:
                            Placer la cle API CurseForge dans .api_key
                        """);
                    return false;
                case "--dry-run": DryRun = true; Log("INFO", "Mode dry-run active"); break;
                case "--force-refresh": ForceRefresh = true; Log("INFO", "Force refresh active"); break;
                default: Log("ERROR", $"Option inconnue: {arg}", ConsoleColor.Red); return false;
            }
        }
        return true;
    }

    static void LoadApiKey()
    {
        var keyFile = Path.Combine(ScriptDir, ".api_key");
        if (File.Exists(keyFile))
        {
            ApiKey = File.ReadAllText(keyFile).Trim();
            if (!string.IsNullOrEmpty(ApiKey))
            {
                Http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
                Log("OK", "Cle API chargee depuis .api_key");
                return;
            }
        }
        Log("WARN", "Cle API non trouvee, utilisation des URLs du manifest uniquement", ConsoleColor.Yellow);
    }

    static void LoadCache()
    {
        var cacheFile = Path.Combine(ScriptDir, ".mods_cache.json");
        if (File.Exists(cacheFile) && !ForceRefresh)
        {
            try
            {
                Cache = JsonSerializer.Deserialize<ModCache>(File.ReadAllText(cacheFile), JsonOpts) ?? new();
            }
            catch { Cache = new(); }
        }
    }

    static void SaveCache()
    {
        if (ApiKey == null) return;
        Cache.LastUpdated = DateTime.UtcNow;
        var cacheFile = Path.Combine(ScriptDir, ".mods_cache.json");
        File.WriteAllText(cacheFile, JsonSerializer.Serialize(Cache, new JsonSerializerOptions { WriteIndented = true }));
    }

    static List<ModInfo> ExtractMods(string modsDir)
    {
        var mods = new List<ModInfo>();
        var files = Directory.GetFiles(modsDir, "*.*")
            .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            try
            {
                using var zip = ZipFile.OpenRead(file);
                var manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry == null) continue;

                using var stream = manifestEntry.Open();
                var manifest = JsonSerializer.Deserialize<ManifestJson>(stream, JsonOpts);
                if (manifest == null) continue;

                var mod = new ModInfo
                {
                    Name = manifest.Name ?? Path.GetFileNameWithoutExtension(file),
                    Version = manifest.Version ?? "N/A",
                    Description = manifest.Description ?? "",
                    Website = manifest.Website ?? "",
                    Authors = manifest.Authors?.Select(a => a.Name).Where(n => n != null).Cast<string>().ToList() ?? []
                };

                // URL CurseForge directe dans le manifest
                if (!string.IsNullOrEmpty(mod.Website) &&
                    (mod.Website.Contains("curseforge.com/hytale/mods/") ||
                     mod.Website.Contains("legacy.curseforge.com/hytale/mods/")))
                {
                    mod.CurseForgeUrl = mod.Website;
                    mod.FoundVia = "manifest";
                }

                mods.Add(mod);
            }
            catch (Exception ex)
            {
                Log("WARN", $"Erreur lecture {Path.GetFileName(file)}: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        return mods.OrderBy(m => m.Name).ToList();
    }

    static async Task SearchModsViaApi(List<ModInfo> mods)
    {
        var toFind = mods.Where(m => string.IsNullOrEmpty(m.CurseForgeUrl)).ToList();

        // Check cache first
        foreach (var mod in toFind.ToList())
        {
            if (Cache.Mods.TryGetValue(mod.Name, out var cached) &&
                cached.CachedAt > DateTime.UtcNow.AddDays(-CacheValidityDays) &&
                !string.IsNullOrEmpty(cached.CurseForgeUrl))
            {
                mod.CurseForgeUrl = cached.CurseForgeUrl;
                mod.FoundVia = "cache";
                Log("OK", $"  {mod.Name} (cache) -> {mod.CurseForgeUrl}");
                toFind.Remove(mod);
            }
        }

        if (toFind.Count == 0)
        {
            Log("OK", "Tous les mods ont deja une URL CurseForge !");
            return;
        }

        Log("API", $"Mods a rechercher: {toFind.Count}", ConsoleColor.Cyan);

        // Strategie 1: Recherche par auteur
        var authors = toFind.SelectMany(m => m.Authors).Distinct().Where(a => a != "Unknown").ToList();
        Log("API", $"Strategie 1: Recherche par auteur ({authors.Count} auteurs)", ConsoleColor.Cyan);

        foreach (var author in authors)
        {
            if (toFind.Count == 0) break;

            var authorMods = await FetchModsBySearch(author);
            var authorLocalMods = toFind.Where(m => m.Authors.Contains(author)).ToList();

            foreach (var mod in authorLocalMods)
            {
                var match = FindBestMatch(mod.Name, authorMods.Where(cf => cf.Authors.Contains(author)));
                if (match != null)
                {
                    mod.CurseForgeUrl = match.Url;
                    mod.FoundVia = match.MatchType;
                    Log("OK", $"  {mod.Name} -> {match.Url} ({match.MatchType})");
                    CacheMod(mod.Name, match.Url);
                    toFind.Remove(mod);
                }
            }
        }

        Log("INFO", $"Apres recherche par auteur: {mods.Count(m => !string.IsNullOrEmpty(m.CurseForgeUrl))} trouves, {toFind.Count} restants");

        if (toFind.Count == 0) return;

        // Strategie 2: Recherche globale par batches
        Log("API", $"Strategie 2: Recherche globale par batches", ConsoleColor.Cyan);

        int offset = 0;
        while (toFind.Count > 0 && offset < 10000)
        {
            var cfMods = await FetchModsBatch(offset);
            if (cfMods.Count == 0) break;

            foreach (var mod in toFind.ToList())
            {
                var match = FindBestMatch(mod.Name, cfMods);
                if (match != null)
                {
                    mod.CurseForgeUrl = match.Url;
                    mod.FoundVia = match.MatchType;
                    Log("OK", $"  {mod.Name} -> {match.Url} ({match.MatchType})");
                    CacheMod(mod.Name, match.Url);
                    toFind.Remove(mod);
                }
            }

            offset += BatchSize;
            await Task.Delay(350); // Rate limiting
        }

        if (toFind.Count > 0)
        {
            Log("WARN", $"Mods non trouves ({toFind.Count}): {string.Join(", ", toFind.Select(m => m.Name))}", ConsoleColor.Yellow);
            foreach (var mod in toFind)
                Cache.Mods[mod.Name] = new CachedMod { NotFound = true, CachedAt = DateTime.UtcNow };
        }
    }

    static async Task<List<CfMod>> FetchModsBySearch(string searchTerm)
    {
        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={HytaleGameId}&searchFilter={Uri.EscapeDataString(searchTerm)}&pageSize={BatchSize}";
            var response = await Http.GetFromJsonAsync<CfResponse>(url, JsonOpts);
            return ParseCfMods(response);
        }
        catch { return []; }
    }

    static async Task<List<CfMod>> FetchModsBatch(int offset)
    {
        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={HytaleGameId}&pageSize={BatchSize}&index={offset}";
            var response = await Http.GetFromJsonAsync<CfResponse>(url, JsonOpts);
            return ParseCfMods(response);
        }
        catch { return []; }
    }

    static List<CfMod> ParseCfMods(CfResponse? response)
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

    static MatchResult? FindBestMatch(string localName, IEnumerable<CfMod> cfMods)
    {
        var normalizedLocal = Normalize(localName);
        var localSlug = Slugify(localName);

        foreach (var cf in cfMods)
        {
            var normalizedCf = Normalize(cf.Name);

            // Match exact normalise
            if (normalizedCf == normalizedLocal)
                return new MatchResult(cf.Url, "exact");

            // Match slug
            if (cf.Slug == localSlug)
                return new MatchResult(cf.Url, "slug");

            // Substring (min 5 chars)
            if (normalizedCf.Length >= 5 && normalizedLocal.Length >= 5)
            {
                if (normalizedCf.Contains(normalizedLocal) || normalizedLocal.Contains(normalizedCf))
                    return new MatchResult(cf.Url, "substring");
            }
        }

        // Fuzzy matching Levenshtein (seulement si assez de candidats)
        MatchResult? bestFuzzy = null;
        int bestSimilarity = 59; // Seuil 60%

        foreach (var cf in cfMods)
        {
            var normalizedCf = Normalize(cf.Name);
            if (normalizedCf.Length < 5 || normalizedLocal.Length < 5) continue;

            var similarity = LevenshteinSimilarity(normalizedLocal, normalizedCf);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestFuzzy = new MatchResult(cf.Url, $"fuzzy:{similarity}%");
            }
        }

        return bestFuzzy;
    }

    static string Normalize(string text) =>
        Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]", "");

    static string Slugify(string text) =>
        Regex.Replace(Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]", "-"), "-+", "-").Trim('-');

    static int LevenshteinSimilarity(string s1, string s2)
    {
        int maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 100;
        int distance = Levenshtein.Distance(s1, s2);
        return (int)((1.0 - (double)distance / maxLen) * 100);
    }

    static void CacheMod(string name, string url)
    {
        Cache.Mods[name] = new CachedMod
        {
            CurseForgeUrl = url,
            CachedAt = DateTime.UtcNow
        };
    }

    static void GenerateMarkdown(List<ModInfo> mods)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Liste des Mods Hytale");
        sb.AppendLine();
        sb.AppendLine($"**Date de generation :** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Nombre total de mods :** {mods.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var mod in mods)
        {
            sb.AppendLine($"## {mod.Name}");
            sb.AppendLine($"**Version :** {mod.Version}");
            sb.AppendLine($"**Auteur(s) :** {(mod.Authors.Count > 0 ? string.Join(", ", mod.Authors) : "Unknown")}");
            if (!string.IsNullOrEmpty(mod.Description))
                sb.AppendLine($"**Description :** {mod.Description}");

            if (!string.IsNullOrEmpty(mod.CurseForgeUrl))
            {
                var icon = mod.FoundVia == "manifest" ? "[lien]" : "[API]";
                sb.AppendLine($"**URL :** [{mod.CurseForgeUrl}]({mod.CurseForgeUrl}) {icon}");
            }
            else if (!string.IsNullOrEmpty(mod.Website))
            {
                sb.AppendLine("**URL :** Non trouvee via CurseForge");
                sb.AppendLine($"**Site alternatif :** [{mod.Website}]({mod.Website})");
            }
            else
            {
                sb.AppendLine("**URL :** Non trouvee");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine("*Legende :*");
        sb.AppendLine("[lien] = URL trouvee dans le manifest");
        sb.AppendLine("[API] = URL trouvee via l'API CurseForge");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Genere automatiquement par HytaleModLister*");

        if (DryRun)
        {
            Console.WriteLine(sb);
        }
        else
        {
            var outputFile = Path.Combine(ScriptDir, "mods_list.md");
            File.WriteAllText(outputFile, sb.ToString());
            Log("OK", $"Fichier genere: {outputFile}");
        }
    }

    static void Log(string level, string message, ConsoleColor color = ConsoleColor.Green)
    {
        var colors = new Dictionary<string, ConsoleColor>
        {
            ["INFO"] = ConsoleColor.Blue,
            ["OK"] = ConsoleColor.Green,
            ["WARN"] = ConsoleColor.Yellow,
            ["ERROR"] = ConsoleColor.Red,
            ["API"] = ConsoleColor.Cyan
        };
        Console.ForegroundColor = colors.GetValueOrDefault(level, color);
        Console.Write($"[{level}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }
}

// Models
record MatchResult(string Url, string MatchType);

class ModInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string Website { get; set; } = "";
    public List<string> Authors { get; set; } = [];
    public string? CurseForgeUrl { get; set; }
    public string? FoundVia { get; set; }
}

class CfMod
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Url { get; set; } = "";
    public List<string> Authors { get; set; } = [];
}

class ManifestJson
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Website { get; set; }
    public List<ManifestAuthor>? Authors { get; set; }
}

class ManifestAuthor
{
    public string? Name { get; set; }
}

class ModCache
{
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, CachedMod> Mods { get; set; } = [];
}

class CachedMod
{
    public string? CurseForgeUrl { get; set; }
    public bool NotFound { get; set; }
    public DateTime CachedAt { get; set; }
}

class CfResponse
{
    public List<CfModData>? Data { get; set; }
}

class CfModData
{
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public CfLinks? Links { get; set; }
    public List<CfAuthor>? Authors { get; set; }
}

class CfLinks
{
    public string? WebsiteUrl { get; set; }
}

class CfAuthor
{
    public string? Name { get; set; }
}
