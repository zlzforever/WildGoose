using Microsoft.AspNetCore.Identity;

namespace WildGoose.Domain.Entity;

public class User : IdentityUser, IDeletion, ICreation, IModification
{
    /// <summary>
    /// Full name in displayable form including all name parts
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Given name(s) or first name(s) of the End-User
    /// </summary>
    public string GivenName { get; set; }

    /// <summary>
    /// Surname(s) or last name(s) of the End-User
    /// </summary>
    public string FamilyName { get; set; }

    /// <summary>
    /// Middle name(s) of the End-User
    /// </summary>
    public string MiddleName { get; set; }

    /// <summary>
    /// Casual name of the End-User that may or may not be the same as the given_name
    /// </summary>
    public string NickName { get; set; }

    /// <summary>
    /// URL of the End-User's profile picture
    /// </summary>
    public string Picture { get; set; }

    public string Address { get; set; }

    /// <summary>
    /// 编号
    /// </summary>
    public string Code { get; set; }

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

    /// <summary>
    /// 是否已经删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 删除人标识
    /// </summary>
    public string DeleterId { get; set; }

    /// <summary>
    /// 删除人
    /// </summary>
    public string DeleterName { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTimeOffset? DeletionTime { get; set; }

    #endregion
}