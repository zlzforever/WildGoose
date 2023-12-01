using System.Text.RegularExpressions;

namespace WildGoose.Domain.Entity;

public class Statement
{
    /// <summary>
    /// 授权效果： 禁止、通过
    /// Allow | Deny
    /// </summary>
    public string Effect { get; set; }

    /// <summary>
    /// 资源
    /// </summary>
    public List<string> Resource { get; set; }

    /// <summary>
    /// 操作
    /// </summary>
    public List<string> Action { get; set; }

    public string Assert(string action, string resource)
    {
        // 不包含 Action， 无法断言
        var containAction = Action.Any(x => x == action || Regex.IsMatch(action, WildCardToRegex(x)));
        if (!containAction)
        {
            return null;
        }

        if (resource == null)
        {
            if (Resource == null || Resource.Count == 0)
            {
                return Effect;
            }

            // Action 匹配上， 但资源匹配不上， 无法断言
            return null;
        }

        var match = Resource.Any(r => resource == r || Regex.IsMatch(resource, WildCardToRegex(r)));
        return match ? Effect : null;
    }

    private static string WildCardToRegex(string rex)
    {
        return $"^{rex.Replace("?", "[\\w:/]{1}").Replace("*", "[\\w:/]*")}$";
    }
}