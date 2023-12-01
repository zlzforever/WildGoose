namespace WildGoose.Domain.Entity;

public interface IModification
{
    /// <summary>
    /// 最后修改人标识
    /// </summary>
    string LastModifierId { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    DateTimeOffset? LastModificationTime { get; set; }

    /// <summary>
    /// 最后修改人名称
    /// </summary>
    string LastModifierName { get; set; }

    public void SetModification(string lastModifierId, string lastModifierName,
        DateTimeOffset lastModificationTime = default)
    {
        LastModificationTime = lastModificationTime == default ? DateTimeOffset.Now : lastModificationTime;
        LastModifierId = lastModifierId;
        LastModifierName = lastModifierName;
    }
}