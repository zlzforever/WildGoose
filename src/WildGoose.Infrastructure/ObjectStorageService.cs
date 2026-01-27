using WildGoose.Domain;

namespace WildGoose.Infrastructure;

public class ObjectStorageService : IObjectStorageService
{
    public Task<string> PutAsync(string key, Stream stream)
    {
        throw new NotImplementedException();
    }
}