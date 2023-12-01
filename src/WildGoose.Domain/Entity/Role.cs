using Microsoft.AspNetCore.Identity;

namespace WildGoose.Domain.Entity;

public class Role : IdentityRole, ICreation, IModification
{
    /// <summary>
    /// 备注
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 权限策略版本
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 策略定义
    /// </summary>
    public string Statement { get; set; }

    #region IDeletion

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset? CreationTime { get; set; }

    /// <summary>
    /// 创建人标识
    /// </summary>
    public string CreatorId { get; set; }

    /// <summary>
    /// 创建人名称
    /// </summary>
    public string CreatorName { get; set; }

    /// <summary>
    /// 最后修改人标识
    /// </summary>
    public string LastModifierId { get; set; }

    /// <summary>
    /// 最后修改人名称
    /// </summary>
    public string LastModifierName { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset? LastModificationTime { get; set; }

    #endregion

    public Role(string name) : base(name)
    {
    }
}