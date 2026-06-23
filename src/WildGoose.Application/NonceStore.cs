using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Npgsql;
using Pomelo.Extensions.Caching.MySql;
using WildGoose.Application.Cache;

namespace WildGoose.Application;

public class NonceStore(IDistributedCache distributedCache, IServiceProvider serviceProvider)
{
    /// <summary>
    /// 写入一次性 Nonce（带过期时间，和 IDistributedCache 行为对齐）
    /// </summary>
    /// <param name="nonceKey">键</param>
    /// <param name="payload">存储内容字节</param>
    /// <param name="ttl">过期时长</param>
    public async Task<bool> SetAsync(string nonceKey, byte[] payload, TimeSpan ttl)
    {
        if (distributedCache is MySqlCache)
        {
            var options = serviceProvider.GetRequiredService<IOptions<MySqlCacheOptions>>().Value;
            var sql = MySqlUtil.BuildInsertQuery(options.ConnectionString, options.SchemaName, options.TableName);
            await using var conn = new MySqlConnection(options.ConnectionString);
            var effected = await conn.ExecuteAsync(sql, new
            {
                Key = nonceKey,
                Value = payload,
                TtlSecond = (int)ttl.TotalSeconds
            });
            return effected > 0;
        }

        if (distributedCache is PostgresCache)
        {
            var options = serviceProvider.GetRequiredService<IOptions<PostgresCacheOptions>>().Value;
            var tableName = options.TableName;
            var pgSql = $"""
                             INSERT INTO {options.SchemaName}.{tableName} (id, value, expiresattime)
                             VALUES (@Key, @Value, NOW() + @Ttl)
                             ON CONFLICT (id) DO NOTHING
                         """;
            await using var conn = new NpgsqlConnection(options.ConnectionString);
            var pgEffected = await conn.ExecuteAsync(pgSql, new
            {
                Key = nonceKey,
                Value = payload,
                Ttl = ttl
            });
            return pgEffected > 0;
        }

        throw new NotSupportedException("Unsupported IDistributedCache implementation: " +
                                        distributedCache.GetType().FullName);
    }
}