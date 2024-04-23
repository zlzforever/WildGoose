namespace WildGoose.Domain;

public class PagedResult<TEntity>(int page, int limit, int total, IEnumerable<TEntity> data)
{
    /// <summary>
    /// 数据列表
    /// </summary>
    public IEnumerable<TEntity> Data { get; } = data ?? Enumerable.Empty<TEntity>();

    /// <summary>
    /// 总计
    /// </summary>
    public int Total { get; } = total;

    /// <summary>
    /// 当前页数
    /// </summary>
    public int Page { get; } = page;

    /// <summary>
    /// 每页数据量
    /// </summary>
    public int Limit { get; } = limit;
}