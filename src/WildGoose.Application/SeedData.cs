using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public class SeedData
{
    public static void Init(IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var adminRole = dbContext.Roles.FirstOrDefault(x => x.NormalizedName == "ADMIN");
        if (adminRole == null)
        {
            adminRole = new WildGoose.Domain.Entity.Role("admin")
            {
                Id = ObjectId.GenerateNewId().ToString(),
                NormalizedName = "ADMIN",
                Version = 1,
                Description = "超级管理员",
                Statement = JsonSerializer.Serialize(new List<Statement>
                {
                    new()
                    {
                        Effect = Effect.Allow,
                        Resource = new List<string> { "*" },
                        Action = new List<string> { "*" }
                    }
                })
            };
            dbContext.Add(adminRole);
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
            userMgr.CreateAsync(admin, "Admin@2023").Wait();
        }

        dbContext.SaveChanges();
    }
}