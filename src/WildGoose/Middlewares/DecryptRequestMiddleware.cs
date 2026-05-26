using WildGoose.Domain.Utils;

namespace WildGoose.Middlewares;

public class DecryptRequestMiddleware(RequestDelegate next)
{
    private const string VersionHeader = "Z-Encrypt-Version";
    private const string KeyHeader = "Z-Encrypt-Key";
    private static readonly string[] HttpMethods = ["POST", "PUT", "PATCH"];

    public async Task InvokeAsync(HttpContext context, ILogger<DecryptRequestMiddleware> logger)
    {
        // 仅 POST/PUT/PATCH 需要解密
        if (HttpMethods.All(x => !x.Equals(context.Request.Method, StringComparison.InvariantCultureIgnoreCase)))
        {
            await next(context);
            return;
        }

        var encryptVersion = context.Request.Headers[VersionHeader].ToString();
        var encryptKey = context.Request.Headers[KeyHeader].ToString();

        var encryptVersionIsNullOrEmpty = string.IsNullOrEmpty(encryptVersion);
        var encryptKeyIsNullOrEmpty = string.IsNullOrEmpty(encryptKey);

        // 若未传加密版本号和加密密钥， 则不解密
        if (encryptVersionIsNullOrEmpty && encryptKeyIsNullOrEmpty)
        {
            if (context.Request.Path.Value != null &&
                context.Request.Path.Value.EndsWith("/password", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.Body.WriteAsync("""
                                                       {
                                                           "code": 403,
                                                           "success": false,
                                                           "msg": "设置密码接口需使用请求体加密"
                                                       }
                                                       """u8.ToArray());
                return;
            }

            // admin/v1.0/users/6944f26a4a256b4e8f4cc500/password 需要强制加密
            await next(context);
            return;
        }

        // 只有同时传了加密版本号和加密密钥，才解密
        if (!encryptKeyIsNullOrEmpty && !encryptVersionIsNullOrEmpty)
        {
            try
            {
                if ("v1.0".Equals(encryptVersion, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogError("不再支持 v1.0 加密， 请升级版本");
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                if ("v1.1".Equals(encryptVersion, StringComparison.OrdinalIgnoreCase))
                {
                    // 前端固定对称加密的 KEY，仅应用对 WF 对一些敏感数据的拦截。
                    await DecryptV1Body(context, encryptKey);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "解密请求体出错");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                // 解密失败则不往下执行了
                return;
            }

            await next(context);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        }

        // 若未传加密密钥，则不解密
    }

    private static string GetRealKeyV11(string encryptKey)
    {
        var p1 = encryptKey.Substring(0, 10);
        var p2 = encryptKey.Substring(16, encryptKey.Length - 16);
        return p1 + p2;
    }

    private static async Task DecryptV1Body(HttpContext context, string encryptKey)
    {
        using var streamReader = new StreamReader(context.Request.Body);
        var encryptedBody = await streamReader.ReadToEndAsync();
        if (!string.IsNullOrEmpty(encryptedBody))
        {
            var key = GetRealKeyV11(encryptKey);
            var parts = encryptedBody.Split(':');
            using var aes = CryptographyUtil.CreateAesCBC(key, parts[0]);
            var ciphertext = parts[1];
            // 解密请求body
            var decryptedBody = CryptographyUtil.AesDecrypt(aes, ciphertext);
            context.Request.Body = new MemoryStream(decryptedBody);
        }
    }
}