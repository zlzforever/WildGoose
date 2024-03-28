using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public class GenerateTopLevelOrgService(IServiceProvider serviceProvider) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "data");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return Task.Run(async () =>
        {
            using var scope = serviceProvider.CreateScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<GenerateTopLevelOrgService>>();
            var jsonOption = scope.ServiceProvider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
                .Value;
            while (!stoppingToken.IsCancellationRequested)
            {
                var topList = await GetListAsync(dbContext, [null]);
                var lv1List = await GetListAsync(dbContext, topList.Select(x => x.Id).ToList());
                var lv2List = await GetListAsync(dbContext, lv1List.Select(x => x.Id).ToList());
                var lv3List = await GetListAsync(dbContext, lv2List.Select(x => x.Id).ToList());
                var list = topList.Concat(lv1List).Concat(lv2List).Concat(lv3List).ToList();
                var json = JsonSerializer.Serialize(list, jsonOption.JsonSerializerOptions);
                await File.WriteAllTextAsync($"{dir}/organizations.json", json, stoppingToken);
                logger.LogInformation("更新组织机构缓存数据成功");
                await Task.Delay(60 * 1000, stoppingToken);
            }
        }, stoppingToken);
    }

    private async Task<List<OrganizationEntity>> GetListAsync(WildGooseDbContext dbContext, List<string> parentIdList)
    {
        return await dbContext.Set<WildGoose.Domain.Entity.Organization>()
            .AsNoTracking()
            .Where(x => parentIdList.Contains(x.Parent.Id))
            .OrderBy(x => x.Code)
            .Select(x => new OrganizationEntity
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ParentId = x.Parent.Id,
                ParentName = x.Parent.Name,
                HasChild = dbContext
                    .Set<WildGoose.Domain.Entity.Organization>()
                    .Any(y => y.Parent.Id == x.Id)
            })
            .ToListAsync();
    }


    class OrganizationEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string ParentId { get; set; }
        public string ParentName { get; set; }
        public bool HasChild { get; set; }
    }
}