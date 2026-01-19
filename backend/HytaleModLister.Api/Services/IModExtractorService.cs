using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public interface IModExtractorService
{
    List<ModInfo> ExtractMods(string modsDirectory);
}
