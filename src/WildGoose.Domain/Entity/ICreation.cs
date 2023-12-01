namespace WildGoose.Domain.Entity;

public interface ICreation
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTimeOffset? CreationTime { get; set; }

    /// <summary>
    /// 创建人标识
    /// </summary>
    string CreatorId { get; set; }

    /// <summary>
    /// 创建人名称
    /// </summary>
    string CreatorName { get; set; }

    public void SetCreation(string creatorId, string creatorName, DateTimeOffset creationTime = default)
    {
        // 创建只能一次操作， 因此如果已经有值， 不能再做设置
        // 若更新成功则创建时间不会为空， 不会发生再次更新的情况
        if (CreationTime.HasValue)
        {
            return;
        }

        CreationTime = creationTime == default ? DateTimeOffset.Now : creationTime;
        CreatorId = creatorId;
        CreatorName = creatorName;
    }
}