using HytaleModLister.Api.Models;

namespace HytaleModLister.Api.Services;

public interface IModUpdateService
{
    Task<UpdateModResponse> UpdateModAsync(string fileName, bool skipRefresh = false);
}
