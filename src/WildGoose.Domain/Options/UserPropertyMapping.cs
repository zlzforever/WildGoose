namespace WildGoose.Domain.Options;

/// <summary>
/// 用户扩展属性映射配置
/// </summary>
public class UserPropertyMapping
{
    /// <summary>
    /// 存储字段名（如 Property01）
    /// </summary>
    public string Column { get; set; }

    /// <summary>
    /// 前端显示名称（如 "职位"、"工号"）
    /// </summary>
    public string DisplayName { get; set; }
}
