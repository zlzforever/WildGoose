namespace WildGoose.Domain;

public static class PasswordGenerator
{
    // 生成8位随机密码的方法
    public static string GeneratePassword(int length = 16)
    {
        // 定义密码包含的字符集（大小写字母+数字，可按需增删，如添加特殊符号）
        const string charSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#&!";
        if (length < 8)
        {
            throw new ArgumentException("Password length must be at least 8 characters long.", nameof(length));
        }

        // 初始化Random（基于系统时间种子，保证随机性）
        var random = new Random();
        // 用StringBuilder拼接字符（比直接字符串拼接更高效）
        var sb = new System.Text.StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            // 随机获取字符集索引，拼接字符
            var randomIndex = random.Next(0, charSet.Length);
            sb.Append(charSet[randomIndex]);
        }

        return sb.ToString();
    }
}