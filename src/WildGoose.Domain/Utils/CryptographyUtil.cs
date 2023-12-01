using System.Security.Cryptography;

namespace WildGoose.Domain.Utils;

public class CryptographyUtil
{
    public static async Task<string> ComputeMd5Async(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var data = new byte[stream.Length];
        _ = await stream.ReadAsync(data, 0, data.Length);

        var bytes = MD5.HashData(data);
        return Convert.ToHexString(bytes);
    }
}