namespace WildGoose.Application.Dto;

/// <summary>
/// 用户扩展属性
/// </summary>
public class ExtProperty
{
    /// <summary>
    /// 属性名称（逻辑名称，如 "Title"、"EmployeeId"）
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 显示名称（用于前端 label 绑定，如 "职位"、"工号"）
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 属性值
    /// </summary>
    public string Value { get; set; }
}
