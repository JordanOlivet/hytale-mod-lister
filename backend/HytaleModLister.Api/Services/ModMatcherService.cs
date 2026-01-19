using System.Text.RegularExpressions;
using Fastenshtein;
using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public partial class ModMatcherService : IModMatcherService
{
    public MatchResult? FindBestMatch(string localName, IEnumerable<CfMod> cfMods)
    {
        var normalizedLocal = Normalize(localName);
        var localSlug = Slugify(localName);

        foreach (var cf in cfMods)
        {
            var normalizedCf = Normalize(cf.Name);

            // Exact normalized match
            if (normalizedCf == normalizedLocal)
                return new MatchResult(cf.Url, "exact");

            // Slug match
            if (cf.Slug == localSlug)
                return new MatchResult(cf.Url, "slug");

            // Substring match (min 5 chars)
            if (normalizedCf.Length >= 5 && normalizedLocal.Length >= 5)
            {
                if (normalizedCf.Contains(normalizedLocal) || normalizedLocal.Contains(normalizedCf))
                    return new MatchResult(cf.Url, "substring");
            }
        }

        // Fuzzy matching with Levenshtein
        MatchResult? bestFuzzy = null;
        var bestSimilarity = 79; // 80% threshold

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

    private static string Normalize(string text) =>
        NonAlphaNumericRegex().Replace(text.ToLowerInvariant(), "");

    private static string Slugify(string text) =>
        MultiDashRegex().Replace(NonAlphaNumericToSlugRegex().Replace(text.ToLowerInvariant(), "-"), "-").Trim('-');

    private static int LevenshteinSimilarity(string s1, string s2)
    {
        var maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 100;
        var distance = Levenshtein.Distance(s1, s2);
        return (int)((1.0 - (double)distance / maxLen) * 100);
    }

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlphaNumericToSlugRegex();

    [GeneratedRegex("-+")]
    private static partial Regex MultiDashRegex();
}
