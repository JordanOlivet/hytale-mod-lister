using System.Globalization;
using System.Text.RegularExpressions;

namespace HytaleModLister.Api.Helpers;

public enum VersionType
{
    Unknown,
    DateEU,
    DateISO,
    Simple,
    SemVer
}

public static partial class VersionComparer
{
    // SemVer: 1.0.0, 1.0.0-beta, 1.0.0-SNAPSHOT
    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[\w.]+)?$")]
    private static partial Regex SemVerRegex();

    // Simple: 1.0, 1.0-beta, 1.0-SNAPSHOT
    [GeneratedRegex(@"^\d+\.\d+(?:-[\w.]+)?$")]
    private static partial Regex SimpleRegex();

    [GeneratedRegex(@"^\d{1,2}\.\d{1,2}\.\d{4}$")]
    private static partial Regex DateEURegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateISORegex();

    public static VersionType DetectType(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return VersionType.Unknown;

        // Remove pre-release suffix for type detection
        var baseVersion = version.Split('-')[0];

        if (Regex.IsMatch(baseVersion, @"^\d+\.\d+\.\d+$"))
            return VersionType.SemVer;

        if (Regex.IsMatch(baseVersion, @"^\d+\.\d+$"))
            return VersionType.Simple;

        if (DateEURegex().IsMatch(version))
            return VersionType.DateEU;

        if (DateISORegex().IsMatch(version))
            return VersionType.DateISO;

        return VersionType.Unknown;
    }

    public static string GetHigherVersion(string? v1, string? v2)
    {
        var comparison = Compare(v1, v2);
        return comparison >= 0 ? (v1 ?? "N/A") : (v2 ?? "N/A");
    }

    public static int Compare(string? v1, string? v2)
    {
        // Handle null/empty cases
        if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2))
            return 0;
        if (string.IsNullOrWhiteSpace(v1))
            return -1;
        if (string.IsNullOrWhiteSpace(v2))
            return 1;

        var type1 = DetectType(v1);
        var type2 = DetectType(v2);

        // Both are numeric versions (SemVer or Simple) - compare by segments
        // This handles cases like 1.0.0 vs 1.0 (should be equal)
        if (IsNumericVersion(type1) && IsNumericVersion(type2))
        {
            return CompareNumericVersions(v1, v2);
        }

        // If same type, compare within type
        if (type1 == type2)
        {
            return type1 switch
            {
                VersionType.DateEU => CompareDateEU(v1, v2),
                VersionType.DateISO => CompareDateISO(v1, v2),
                _ => string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase)
            };
        }

        // Different types: Numeric > Date > Unknown
        return GetTypePriority(type1).CompareTo(GetTypePriority(type2));
    }

    private static bool IsNumericVersion(VersionType type) =>
        type is VersionType.SemVer or VersionType.Simple;

    private static int GetTypePriority(VersionType type) => type switch
    {
        VersionType.SemVer => 4,
        VersionType.Simple => 4, // Same priority as SemVer since we compare by value
        VersionType.DateISO => 2,
        VersionType.DateEU => 2,
        _ => 0
    };

    private static int CompareNumericVersions(string v1, string v2)
    {
        // Split version and pre-release
        var parts1 = v1.Split('-', 2);
        var parts2 = v2.Split('-', 2);

        var segments1 = parts1[0].Split('.').Select(int.Parse).ToArray();
        var segments2 = parts2[0].Split('.').Select(int.Parse).ToArray();

        // Compare numeric segments (missing segments treated as 0)
        for (int i = 0; i < Math.Max(segments1.Length, segments2.Length); i++)
        {
            var seg1 = i < segments1.Length ? segments1[i] : 0;
            var seg2 = i < segments2.Length ? segments2[i] : 0;

            if (seg1 != seg2)
                return seg1.CompareTo(seg2);
        }

        // Same version numbers, compare pre-release using hierarchy
        var preRelease1 = parts1.Length > 1 ? parts1[1] : null;
        var preRelease2 = parts2.Length > 1 ? parts2[1] : null;

        return ComparePreRelease(preRelease1, preRelease2);
    }

    /// <summary>
    /// Compare pre-release tags with hierarchy:
    /// release (no tag) > rc > beta > alpha > dev > snapshot > unknown
    /// </summary>
    private static int ComparePreRelease(string? pr1, string? pr2)
    {
        // No pre-release > has pre-release
        if (pr1 == null && pr2 == null) return 0;
        if (pr1 == null) return 1;  // 1.0 > 1.0-anything
        if (pr2 == null) return -1; // 1.0-anything < 1.0

        // Both have pre-release, compare by priority
        var (priority1, remainder1) = GetPreReleasePriority(pr1);
        var (priority2, remainder2) = GetPreReleasePriority(pr2);

        if (priority1 != priority2)
            return priority1.CompareTo(priority2);

        // Same priority level, compare remainder (e.g., beta.1 vs beta.2)
        return ComparePreReleaseRemainder(remainder1, remainder2);
    }

    /// <summary>
    /// Get priority for pre-release tag. Higher = more stable.
    /// </summary>
    private static (int priority, string remainder) GetPreReleasePriority(string preRelease)
    {
        var lower = preRelease.ToLowerInvariant();

        // Extract the tag type and any suffix (e.g., "beta.2" -> "beta", ".2")
        var match = Regex.Match(lower, @"^(rc|release[-.]?candidate|beta|alpha|dev|snapshot)(?:[-.]?(.*))?$");

        if (!match.Success)
        {
            // Unknown pre-release type - lowest priority
            return (0, preRelease);
        }

        var tag = match.Groups[1].Value;
        var remainder = match.Groups[2].Success ? match.Groups[2].Value : "";

        var priority = tag switch
        {
            "rc" or "release-candidate" or "releasecandidate" => 80,
            "beta" => 60,
            "alpha" => 40,
            "dev" => 20,
            "snapshot" => 10,
            _ => 0
        };

        return (priority, remainder);
    }

    /// <summary>
    /// Compare pre-release remainders (e.g., "1" vs "2" in beta.1 vs beta.2)
    /// </summary>
    private static int ComparePreReleaseRemainder(string r1, string r2)
    {
        if (string.IsNullOrEmpty(r1) && string.IsNullOrEmpty(r2)) return 0;
        if (string.IsNullOrEmpty(r1)) return -1; // beta < beta.1
        if (string.IsNullOrEmpty(r2)) return 1;  // beta.1 > beta

        // Try numeric comparison
        if (int.TryParse(r1, out var num1) && int.TryParse(r2, out var num2))
            return num1.CompareTo(num2);

        // Fallback to string comparison
        return string.Compare(r1, r2, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareDateEU(string v1, string v2)
    {
        // Format: dd.MM.yyyy
        if (DateTime.TryParseExact(v1, "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date1) &&
            DateTime.TryParseExact(v2, "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date2))
        {
            return date1.CompareTo(date2);
        }

        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareDateISO(string v1, string v2)
    {
        // Format: yyyy-MM-dd
        if (DateTime.TryParseExact(v1, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date1) &&
            DateTime.TryParseExact(v2, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date2))
        {
            return date1.CompareTo(date2);
        }

        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }
}
