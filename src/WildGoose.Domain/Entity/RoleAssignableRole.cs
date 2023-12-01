namespace WildGoose.Domain.Entity;

/// <summary>
/// 角色可以分配哪些角色
/// </summary>
public class RoleAssignableRole
{
    public string RoleId { get; set; }
    public string AssignableId { get; set; }
}