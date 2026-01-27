using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public class GenerateTopThreeLevelOrganizationsCacheFileService(
    IServiceProvider serviceProvider,
    IOptions<JsonOptions> jsonOptions,
    ILogger<GenerateTopThreeLevelOrganizationsCacheFileService> logger) : BackgroundService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;

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

            while (!stoppingToken.IsCancellationRequested)
            {
                var topList = await GetListAsync(dbContext, [null]);
                var lv1List = await GetListAsync(dbContext, topList.Select(x => x.Id).ToList());
                var lv2List = await GetListAsync(dbContext, lv1List.Select(x => x.Id).ToList());
                var lv3List = await GetListAsync(dbContext, lv2List.Select(x => x.Id).ToList());
                var list = topList.Concat(lv1List).Concat(lv2List).Concat(lv3List).ToList();
                var json = JsonSerializer.Serialize(list, _jsonSerializerOptions);
                await File.WriteAllTextAsync($"{dir}/organizations.json", json, stoppingToken);

                logger.LogInformation("Generate top three level organizations cache file success");
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
            .Select(x => new OrganizationEntity(x.Id, x.Name, x.Code, x.Parent.Id, x.Parent.Name,
                dbContext
                    .Set<WildGoose.Domain.Entity.Organization>()
                    .Any(y => y.Parent.Id == x.Id)))
            .ToListAsync();
    }

    [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
    private record OrganizationEntity(
        string Id,
        string Name,
        string Code,
        string ParentId,
        string ParentName,
        bool HasChild);
}