using System.Text.RegularExpressions;

namespace HytaleModLister.Api.Helpers;

public static partial class VersionExtractor
{
    // SemVer with optional pre-release: AdminUI-1.0.4, ModName_v2.3.1-beta
    [GeneratedRegex(@"[-_]v?(\d+\.\d+\.\d+(?:-[a-zA-Z0-9]+(?:\.[a-zA-Z0-9]+)*)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex SemVerPattern();

    // Simple version with pre-release: Mod-1.0-SNAPSHOT, Mod-1.0-beta
    [GeneratedRegex(@"[-_]v?(\d+\.\d+(?:-[a-zA-Z0-9]+(?:\.[a-zA-Z0-9]+)*)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex SimpleWithPreReleasePattern();

    // Date EU format: Aures_Horses_13.01.2026
    [GeneratedRegex(@"[-_](\d{1,2}\.\d{1,2}\.\d{4})$", RegexOptions.IgnoreCase)]
    private static partial Regex DateEUPattern();

    // Date ISO format: ModName_2026-01-13
    [GeneratedRegex(@"[-_](\d{4}-\d{2}-\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex DateISOPattern();

    public static string? ExtractFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Remove extension for matching (handles .jar, .zip, and any other extension)
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // Try SemVer first (3 segments, most specific)
        var match = SemVerPattern().Match(nameWithoutExt);
        if (match.Success)
            return match.Groups[1].Value;

        // Try Date EU format (before simple version to avoid false matches)
        match = DateEUPattern().Match(nameWithoutExt);
        if (match.Success)
            return match.Groups[1].Value;

        // Try Date ISO format
        match = DateISOPattern().Match(nameWithoutExt);
        if (match.Success)
            return match.Groups[1].Value;

        // Try simple version with optional pre-release (2 segments)
        match = SimpleWithPreReleasePattern().Match(nameWithoutExt);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }
}
