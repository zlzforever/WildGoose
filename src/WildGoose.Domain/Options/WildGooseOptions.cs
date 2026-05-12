namespace WildGoose.Domain.Options;


public class WildGooseOptions
{
    public string[] AddUserRoles { get; set; } = [];

    /// <summary>
    /// 用户扩展属性映射字典，key 为逻辑名称（前端传入），value 包含存储字段名和显示名称
    /// 例如：{ "Title": { "Column": "Property01", "DisplayName": "职位" } }
    /// </summary>
    public Dictionary<string, UserPropertyMapping> UserPropertyMappings { get; set; } = new();
}