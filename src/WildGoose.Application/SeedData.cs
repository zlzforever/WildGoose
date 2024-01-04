using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public class SeedData
{
    public static async Task Init(IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        Defaults.OrganizationTableName =
            dbContext.Set<WildGoose.Domain.Entity.Organization>().EntityType.GetTableName();
        Defaults.OrganizationAdministratorTableName =
            dbContext.Set<OrganizationAdministrator>().EntityType.GetTableName();

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
            new(Defaults.OrganizationAdmin, "机构管理员", "[]")
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
            await userMgr.CreateAsync(admin, "Admin@2023");
            await userMgr.AddToRoleAsync(admin, "admin");
        }

        await dbContext.SaveChangesAsync();
    }
}