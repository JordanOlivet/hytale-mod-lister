using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public interface IModMatcherService
{
    MatchResult? FindBestMatch(string localName, IEnumerable<CfMod> cfMods);
}
