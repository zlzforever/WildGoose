namespace WildGoose.Domain;

public interface IObjectStorageService
{
    Task<string> PutAsync(string key, Stream stream);
}