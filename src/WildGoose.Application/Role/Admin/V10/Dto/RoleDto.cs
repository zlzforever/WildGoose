namespace WildGoose.Application.Role.Admin.V10.Dto;
// ReSharper disable UnusedAutoPropertyAccessor.Global
public class RoleDto
{
    /// <summary>
    /// 主键
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public string LastModificationTime { get; set; }

    /// <summary>
    /// 权限策略版本
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 可授于角色
    /// </summary>
    public List<RoleBasicDto> AssignableRoles { get; set; }

    /// <summary>
    /// 权限策略
    /// </summary>
    public string Statement { get; set; }
}