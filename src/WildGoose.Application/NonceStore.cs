using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WildGoose.Domain.Options;

namespace WildGoose.Application;

public class NonceStore(WildGooseDbContext dbContext, IOptions<DbOptions> dbOptions)
{
    /// <summary>
    /// 写入一次性 Nonce（带过期时间，和 IDistributedCache 行为对齐）
    /// </summary>
    /// <param name="nonceKey">键</param>
    /// <param name="payload">存储内容字节</param>
    /// <param name="ttl">过期时长</param>
    public async Task<bool> SetAsync(string nonceKey, byte[] payload, TimeSpan ttl)
    {
        var isMySql = string.Equals(dbOptions.Value.DatabaseType, "mysql",
            StringComparison.OrdinalIgnoreCase);

        var tableName = $"{dbOptions.Value.TablePrefix}cache_entries";
        var conn = dbContext.Database.GetDbConnection();

        if (isMySql)
        {
            var sql = $"""
                           INSERT IGNORE INTO {tableName} (id, value, expiresattime)
                           VALUES (@Key, @Value, DATE_ADD(NOW(), INTERVAL @TtlSecond SECOND))
                       """;

            var effected = await conn.ExecuteAsync(sql, new
            {
                Key = nonceKey,
                Value = payload,
                TtlSecond = (int)ttl.TotalSeconds
            });
            return effected > 0;
        }

        var pgSql = $"""
                         INSERT INTO {tableName} (id, value, expiresattime)
                         VALUES (@Key, @Value, NOW() + @Ttl)
                         ON CONFLICT (id) DO NOTHING
                     """;

        var pgEffected = await conn.ExecuteAsync(pgSql, new
        {
            Key = nonceKey,
            Value = payload,
            Ttl = ttl
        });
        return pgEffected > 0;
    }
}