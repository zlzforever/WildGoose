using System.Security.Cryptography;
using System.Text;

namespace WildGoose.Domain.Utils;

public static class CryptographyUtil
{
    public static async Task<string> ComputeMd5Async(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var data = new byte[stream.Length];
        _ = await stream.ReadAsync(data, 0, data.Length);

        var bytes = MD5.HashData(data);
        return Convert.ToHexString(bytes);
    }
    
    public static Aes CreateAesEcb(string key)
    {
        var keyArray = Encoding.UTF8.GetBytes(key);
        var aes = Aes.Create();
        aes.Key = keyArray;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    public static string CreateAesKey()
    {
        var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.KeySize = 128; // 可以设置为 128、192 或 256 位
        aes.GenerateKey();
        return Convert.ToBase64String(aes.Key);
    }

    public static byte[] AesEcbDecrypt(Aes aes, string text)
    {
        var toEncryptArray = Convert.FromBase64String(text);
        using var decrypt = aes.CreateDecryptor();
        return decrypt.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
    }
}