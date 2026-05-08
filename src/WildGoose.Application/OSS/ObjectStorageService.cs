namespace WildGoose.Application.OSS;

public class ObjectStorageService
{
    public Task<string> PutAsync(string key, Stream stream)
    {
        return Task.FromResult("");
    }
}