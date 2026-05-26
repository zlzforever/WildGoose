using Microsoft.EntityFrameworkCore;
using WildGoose.Domain;

namespace WildGoose.Application.Extensions;

public static class PagedQueryExtensions
{
    public static async Task<PagedResult<TEntity>> PagedQueryAsync<TEntity>(
        this IQueryable<TEntity> queryable,
        int page, int limit)
        where TEntity : class
    {
        page = page < 1 ? 1 : page;
        limit = limit < 1 ? 10 : limit;
        limit = limit > 100 ? 100 : limit;

        var total = await queryable.CountAsync();
        var data = total == 0
            ? Enumerable.Empty<TEntity>()
            : await queryable.Skip((page - 1) * limit).Take(limit).AsSplitQuery().ToListAsync();
        return new PagedResult<TEntity>(page, limit, total, data);
    }
}