using WildGoose.Application.Dto;
using WildGoose.Domain.Entity;
using WildGoose.Domain.Options;

namespace WildGoose.Application.Services.Admin.User;

public static class UserExtensionPropertyHelper
{
    private static readonly string[] PropertyNames =
    [
        "Property01", "Property02", "Property03", "Property04", "Property05",
        "Property06", "Property07", "Property08", "Property09"
    ];

    /// <summary>
    /// 将字典中的扩展属性按映射关系设置到 UserExtension 实体上
    /// </summary>
    public static void SetProperties(UserExtension extension, Dictionary<string, string> properties,
        Dictionary<string, UserPropertyMapping> propertyMap)
    {
        if (properties == null || propertyMap == null || propertyMap.Count == 0)
        {
            return;
        }

        var extensionType = typeof(UserExtension);
        foreach (var (key, value) in properties)
        {
            if (propertyMap.TryGetValue(key, out var mapping) &&
                mapping.Column != null &&
                PropertyNames.Contains(mapping.Column))
            {
                extensionType.GetProperty(mapping.Column)?.SetValue(extension, value);
            }
        }
    }

    /// <summary>
    /// 将 UserExtension 实体上的扩展属性按映射关系转换为 List&lt;ExtProperty&gt;
    /// </summary>
    public static List<ExtProperty> GetProperties(UserExtension extension,
        Dictionary<string, UserPropertyMapping> propertyMap)
    {
        var result = new List<ExtProperty>();
        if (propertyMap == null || propertyMap.Count == 0)
        {
            return result;
        }

        var extensionType = typeof(UserExtension);
        foreach (var (key, mapping) in propertyMap)
        {
            if (mapping.Column == null || !PropertyNames.Contains(mapping.Column))
            {
                continue;
            }

            var value = extension == null
                ? null
                : extensionType.GetProperty(mapping.Column)?.GetValue(extension) as string;
            result.Add(new ExtProperty
            {
                Name = key,
                DisplayName = mapping.DisplayName,
                Value = value
            });
        }

        return result;
    }

    // private static string FirstCharToLower(string str)
    // {
    //     // 空值/空字符串直接返回
    //     if (string.IsNullOrEmpty(str))
    //     {
    //         return str;
    //     }
    //
    //     // 首字符小写 + 剩余字符拼接
    //     return char.ToLower(str[0]) + str[1..];
    // }
}