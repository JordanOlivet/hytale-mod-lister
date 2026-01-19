using System.IO.Compression;
using System.Text.Json;
using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public class ModExtractorService : IModExtractorService
{
    private readonly ILogger<ModExtractorService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModExtractorService(ILogger<ModExtractorService> logger)
    {
        _logger = logger;
    }

    public List<ModInfo> ExtractMods(string modsDirectory)
    {
        var mods = new List<ModInfo>();

        if (!Directory.Exists(modsDirectory))
        {
            _logger.LogWarning("Mods directory not found: {Path}", modsDirectory);
            return mods;
        }

        var files = Directory.GetFiles(modsDirectory, "*.*")
            .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            try
            {
                using var zip = ZipFile.OpenRead(file);
                var manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    _logger.LogDebug("No manifest.json in {File}", Path.GetFileName(file));
                    continue;
                }

                using var stream = manifestEntry.Open();
                var manifest = JsonSerializer.Deserialize<ManifestJson>(stream, JsonOpts);
                if (manifest == null) continue;

                var mod = new ModInfo
                {
                    Name = manifest.Name ?? Path.GetFileNameWithoutExtension(file),
                    FileName = Path.GetFileName(file),
                    Version = manifest.Version ?? "N/A",
                    Description = manifest.Description ?? "",
                    Website = manifest.Website ?? "",
                    Authors = manifest.Authors?.Select(a => a.Name).Where(n => n != null).Cast<string>().ToList() ?? []
                };

                // Check if website is a CurseForge URL
                if (!string.IsNullOrEmpty(mod.Website) &&
                    (mod.Website.Contains("curseforge.com/hytale/mods/") ||
                     mod.Website.Contains("legacy.curseforge.com/hytale/mods/")))
                {
                    mod.CurseForgeUrl = mod.Website;
                    mod.FoundVia = "manifest";
                }

                mods.Add(mod);
                _logger.LogDebug("Extracted mod: {Name} v{Version}", mod.Name, mod.Version);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading {File}", Path.GetFileName(file));
            }
        }

        return mods.OrderBy(m => m.Name).ToList();
    }
}
