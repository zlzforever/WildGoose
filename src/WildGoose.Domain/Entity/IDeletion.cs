namespace WildGoose.Domain.Entity;

public interface IDeletion
{
    /// <summary>
    /// 是否已经删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 删除人标识
    /// </summary>
    string DeleterId { get; set; }

    /// <summary>
    /// 删除人名称
    /// </summary>
    string DeleterName { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    DateTimeOffset? DeletionTime { get; set; }

    public void SetDeletion(string deleterId, string deleterName, DateTimeOffset deletionTime = default)
    {
        // 删除只能一次操作， 因此如果已经有值， 不能再做设置
        // 若更新成功为 true， 则不会发生再次更新的情况
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletionTime = deletionTime == default ? DateTimeOffset.Now : deletionTime;
        DeleterId = deleterId;
        DeleterName = deleterName;
    }
}