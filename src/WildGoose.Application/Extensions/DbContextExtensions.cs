using Dapper;
using Microsoft.EntityFrameworkCore;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Extensions;

public static class DbContextExtensions
{
//     /// <summary>
//     /// 传入 N 入组织，如果对这 N 个组织都有管理权限，则返回 true，否则返回 false
//     /// </summary>
//     /// <param name="context"></param>
//     /// <param name="userId"></param>
//     /// <param name="organizationIds"></param>
//     /// <returns></returns>
//     public static async Task<bool> AllPermissionAsync(this WildGooseDbContext context, string userId,
//         params string[] organizationIds)
//     {
//         var t1 = context.Set<OrganizationAdministrator>().EntityType.GetTableName();
//         var t2 = context.Set<WildGoose.Domain.Entity.Organization>().EntityType.GetTableName();
//         var sql = $$"""
//                     WITH RECURSIVE recursion AS
//                                        (SELECT t1.*
//                                         from {{t2}} t1
//                                         where t1.id = any(@IdList)
//                                         UNION ALL
//                                         SELECT t2.*
//                                         from {{t2}} t2,
//                                              recursion t3
//                                         WHERE t2.id = t3.parent_id)
//                     SELECT *
//                     FROM recursion t;
//                     """;
//
//         var conn = context.Database.GetDbConnection();
//         var ors = (await conn.QueryAsync<WildGoose.Domain.Entity.Organization>(sql, new
//         {
//             IdList = organizationIds
//         })).ToList();
//
//         var pathList = (await conn.QueryAsync<string>(
//             $$"""
//               SELECT t2.path FROM {{t1}} t1 JOIN {{t2}} t2
//               ON t1.organization_id = t2.id
//               WHERE t1.user_id = @UserId
//               """, new
//             {
//                 UserId = userId
//             })).ToList();
//         return pathList.Any(x => organizationIds.All(x.Contains));
//     }

    /// <summary>
    /// 任意一个组织有管理权限，则返回 true，否则返回 false
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userId"></param>
    /// <param name="organizationIds"></param>
    /// <returns></returns>
    public static async Task<bool> AnyPermissionAsync(this WildGooseDbContext context, string userId,
        params string[] organizationIds)
    {
        var t1 = context.Set<OrganizationAdministrator>().EntityType.GetTableName();
        var t2 = context.Set<OrganizationDetail>().EntityType.GetTableName();

        await using var conn = context.Database.GetDbConnection();
        // TODO: 是不是把 organizationId 传入 SQL 进行查询会更好
        var pathList = (await conn.QueryAsync<string>(
            $$"""
              SELECT t2.path FROM {{t1}} t1 JOIN {{t2}} t2
              ON t1.organization_id = t2.id
              WHERE t1.user_id = @UserId
              """, new
            {
                UserId = userId
            })).ToList();
        return pathList.Any(x => organizationIds.Any(x.Contains));
    }
}