using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public static class SeedData
{
    public static async Task Init(IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        var dbOptions = scope.ServiceProvider.GetRequiredService<IOptions<DbOptions>>().Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        Defaults.OrganizationTableName =
            dbContext.Set<WildGoose.Domain.Entity.Organization>().EntityType.GetTableName();
        Defaults.OrganizationAdministratorTableName =
            dbContext.Set<OrganizationAdministrator>().EntityType.GetTableName();
        Defaults.OrganizationScopeTableName = dbContext.Set<OrganizationScope>().EntityType.GetTableName();
        Defaults.OrganizationDetailTableName = dbContext.Set<OrganizationDetail>().EntityType.GetTableName();

        var defaultRoles = new List<(string Name, string Description, string Statement)>
        {
            new(Defaults.AdminRole, "超级管理员", JsonSerializer.Serialize(new List<Statement>
            {
                new()
                {
                    Effect = Effect.Allow,
                    Resource = new List<string> { "*" },
                    Action = new List<string> { "*" }
                }
            })),
            new(Defaults.OrganizationAdmin, "机构管理员", "[]"),
            new(Defaults.UserAdmin, "用户管理员", "[]")
        };
        foreach (var role in defaultRoles)
        {
            var normalizedName = role.Name.ToUpperInvariant();
            var entity = dbContext.Roles.FirstOrDefault(x => x.NormalizedName == normalizedName);
            if (entity == null)
            {
                entity = new WildGoose.Domain.Entity.Role(role.Name)
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    NormalizedName = normalizedName,
                    Version = 1,
                    Description = role.Description,
                    Statement = role.Statement
                };
                ((ICreation)entity).SetCreation("system", "system");
                dbContext.Add(entity);
            }

            switch (entity.Name)
            {
                case Defaults.OrganizationAdmin:
                    Defaults.OrganizationAdminRoleId = entity.Id;
                    break;
                case Defaults.AdminRole:
                    Defaults.AdminRoleId = entity.Id;
                    break;
                case Defaults.UserAdmin:
                    Defaults.UserAdminRoleId = entity.Id;
                    break;
            }
        }

        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<WildGoose.Domain.Entity.User>>();
        var admin = userMgr.Users.FirstOrDefault(x => x.UserName == "admin");
        if (admin == null)
        {
            admin = new WildGoose.Domain.Entity.User
            {
                Id = ObjectId.GenerateNewId().ToString(),
                UserName = "admin",
                EmailConfirmed = true
            };
            ((ICreation)admin).SetCreation("system", "system");
            var defaultPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
            defaultPassword = string.IsNullOrEmpty(defaultPassword)
                ? PasswordGenerator.GeneratePassword()
                : defaultPassword;
            Console.WriteLine("Default admin password: " + defaultPassword);
            await userMgr.CreateAsync(admin, defaultPassword);
            await userMgr.AddToRoleAsync(admin, "admin");
        }

        var conn = dbContext.Database.GetDbConnection();
        var materializedName = $"{dbOptions.TablePrefix}organization_detail";
        var databaseName = conn.Database;
        var isMysql = "mysql".Equals(dbOptions.DatabaseType, StringComparison.OrdinalIgnoreCase);
        var materializedExistSql = isMysql
            ? $$"""
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.TABLES
                    WHERE TABLE_SCHEMA = '{{databaseName}}'  -- 数据库名
                    AND TABLE_NAME = '{{materializedName}}'
                );
                """
            : $$"""
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_class c
                             JOIN pg_namespace n ON c.relnamespace = n.oid
                    WHERE c.relname = '{{materializedName}}'
                      AND c.relkind = 'm'  -- 'm' 表示物化视图
                      AND n.nspname = 'public'  -- 默认为 public
                )
                """;
        if (isMysql)
        {
            var sql = $"""
                       create table if not exists {dbOptions.TablePrefix}organization_scope
                       (
                           organization_id varchar(36)  not null,
                           scope           varchar(256) not null,
                           primary key (organization_id, scope)
                       );
                       """;
            await conn.ExecuteAsync(sql);
        }

        var materializedExisted = await conn.QuerySingleAsync<bool>(materializedExistSql);
        if (!materializedExisted)
        {
            var sqlPath = isMysql
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sqls", "MySql", "organization_detail.sql")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sqls", "Postgre", "organization_detail.sql");
            var text = await File.ReadAllTextAsync(sqlPath);
            text = text.Replace("${table_prefix}", dbOptions.TablePrefix);
            await conn.ExecuteAsync(text);
        }

        // await conn.ExecuteAsync("");
        // TODO: 
        // 删除用户的索一索引
        await dbContext.SaveChangesAsync();
    }
}