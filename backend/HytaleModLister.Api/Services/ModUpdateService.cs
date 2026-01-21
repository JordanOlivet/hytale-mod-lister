using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public class ModUpdateService : IModUpdateService
{
    private readonly IModRefreshService _refreshService;
    private readonly ICurseForgeService _curseForgeService;
    private readonly ICacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ModUpdateService> _logger;

    public ModUpdateService(
        IModRefreshService refreshService,
        ICurseForgeService curseForgeService,
        ICacheService cacheService,
        IConfiguration configuration,
        ILogger<ModUpdateService> logger)
    {
        _refreshService = refreshService;
        _curseForgeService = curseForgeService;
        _cacheService = cacheService;
        _configuration = configuration;
        _logger = logger;
    }

    private string ModsPath => _configuration.GetValue("ModsPath", "/app/mods");

    public async Task<UpdateModResponse> UpdateModAsync(string fileName, bool skipRefresh = false)
    {
        _logger.LogInformation("Starting update for mod: {FileName}", fileName);

        // 1. Find the mod in the current list
        var mods = _refreshService.GetCurrentMods();
        var mod = mods.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

        if (mod == null)
        {
            _logger.LogWarning("Mod not found: {FileName}", fileName);
            return new UpdateModResponse(false, "Mod not found in the current list");
        }

        // 2. Check if mod has a CurseForge URL
        if (string.IsNullOrEmpty(mod.CurseForgeUrl))
        {
            _logger.LogWarning("Mod {FileName} has no CurseForge URL", fileName);
            return new UpdateModResponse(false, "This mod has no CurseForge URL associated");
        }

        // 3. Extract slug from CurseForge URL
        var slug = ExtractSlugFromUrl(mod.CurseForgeUrl);
        if (string.IsNullOrEmpty(slug))
        {
            _logger.LogWarning("Could not extract slug from URL: {Url}", mod.CurseForgeUrl);
            return new UpdateModResponse(false, "Could not extract mod identifier from CurseForge URL");
        }

        _logger.LogInformation("Extracted slug: {Slug} from URL: {Url}", slug, mod.CurseForgeUrl);

        // 4. Get mod info from CurseForge API
        var cfMod = await _curseForgeService.GetModBySlugAsync(slug);
        if (cfMod == null)
        {
            _logger.LogWarning("Could not find mod on CurseForge with slug: {Slug}", slug);
            return new UpdateModResponse(false, "Could not find this mod on CurseForge");
        }

        // 5. Get the latest file
        var latestFile = cfMod.LatestFiles?
            .OrderByDescending(f => f.FileDate ?? DateTime.MinValue)
            .FirstOrDefault();

        if (latestFile == null)
        {
            _logger.LogWarning("No files found for mod: {ModId}", cfMod.Id);
            return new UpdateModResponse(false, "No files available for this mod on CurseForge");
        }

        _logger.LogInformation("Latest file: {FileName} (ID: {FileId})", latestFile.FileName, latestFile.Id);

        // 6. Get download URL
        string? downloadUrl = latestFile.DownloadUrl;

        if (string.IsNullOrEmpty(downloadUrl))
        {
            // Try to get download URL via API
            downloadUrl = await _curseForgeService.GetFileDownloadUrlAsync(cfMod.Id, latestFile.Id);
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            _logger.LogWarning("Download disabled for mod {ModId} file {FileId}", cfMod.Id, latestFile.Id);
            return new UpdateModResponse(false, "Download is disabled for this mod (distribution not allowed by author)");
        }

        // 7. Download to temporary file
        var tempPath = Path.Combine(Path.GetTempPath(), $"mod_update_{Guid.NewGuid()}.tmp");
        try
        {
            _logger.LogInformation("Downloading from: {Url}", downloadUrl);

            using (var downloadStream = await _curseForgeService.DownloadFileAsync(downloadUrl))
            {
                if (downloadStream == null)
                {
                    return new UpdateModResponse(false, "Failed to download the mod file");
                }

                using var fileStream = File.Create(tempPath);
                await downloadStream.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Downloaded to temp file: {TempPath}", tempPath);

            // 8. Verify integrity if hash is available
            var md5Hash = latestFile.Hashes?.FirstOrDefault(h => h.Algo == 2)?.Value; // 2 = MD5
            if (!string.IsNullOrEmpty(md5Hash))
            {
                var computedHash = await ComputeMd5Async(tempPath);

                if (!computedHash.Equals(md5Hash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Hash mismatch! Expected: {Expected}, Got: {Computed}", md5Hash, computedHash);
                    return new UpdateModResponse(false, "Downloaded file integrity check failed");
                }

                _logger.LogInformation("Hash verification passed");
            }
            else
            {
                _logger.LogWarning("No MD5 hash available for verification, proceeding without integrity check");
            }

            // 9. Move to mods folder
            var newFileName = latestFile.FileName ?? $"{mod.Name}-{mod.LatestCurseForgeVersion}.jar";
            var newFilePath = Path.Combine(ModsPath, newFileName);
            var oldFilePath = Path.Combine(ModsPath, fileName);

            // Ensure we don't overwrite if same name
            if (File.Exists(newFilePath) && !newFilePath.Equals(oldFilePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(newFilePath);
            }

            File.Move(tempPath, newFilePath, overwrite: true);
            _logger.LogInformation("Moved new file to: {NewPath}", newFilePath);

            // 10. Delete old file only if it's different from the new one
            if (!oldFilePath.Equals(newFilePath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldFilePath))
            {
                File.Delete(oldFilePath);
                _logger.LogInformation("Deleted old file: {OldPath}", oldFilePath);
            }

            // 11. Invalidate the cache for this mod so it gets fresh data
            _cacheService.InvalidateMod(mod.Name);
            _logger.LogInformation("Cache invalidated for mod: {ModName}", mod.Name);

            // 12. Refresh the mods list synchronously to ensure fresh data (unless skipped for bulk updates)
            if (!skipRefresh)
            {
                try
                {
                    await _refreshService.RefreshModsAsync(forceRefresh: false);
                    _logger.LogInformation("Mods list refreshed after update");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing mods list after update");
                    // Don't fail the update if refresh fails
                }
            }

            _logger.LogInformation("Successfully updated mod {OldFileName} to {NewFileName}", fileName, newFileName);
            return new UpdateModResponse(true, "Mod updated successfully", newFileName, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mod update");

            // Clean up temp file
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            return new UpdateModResponse(false, $"Update failed: {ex.Message}");
        }
    }

    private static string? ExtractSlugFromUrl(string url)
    {
        // URL format: https://www.curseforge.com/hytale/mods/slug-name
        var match = Regex.Match(url, @"curseforge\.com/[^/]+/mods/([^/?\s]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static async Task<string> ComputeMd5Async(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await md5.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
