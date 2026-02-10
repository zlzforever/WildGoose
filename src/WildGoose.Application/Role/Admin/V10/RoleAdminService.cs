using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Extensions;
using WildGoose.Application.Role.Admin.V10.Command;
using WildGoose.Application.Role.Admin.V10.Dto;
using WildGoose.Application.Role.Admin.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Domain.Extensions;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Role.Admin.V10;

public class RoleAdminService(
    WildGooseDbContext dbContext,
    ISession session,
    IOptions<DbOptions> dbOptions,
    ILogger<RoleAdminService> logger,
    IMemoryCache memoryCache,
    RoleManager<WildGoose.Domain.Entity.Role> roleManager)
    : BaseService(dbContext, session, dbOptions, logger, memoryCache)
{
    public async Task<string> AddAsync(AddRoleCommand command)
    {
        if (Defaults.AdminRole.Equals(command.Name, StringComparison.OrdinalIgnoreCase) ||
            Defaults.OrganizationAdmin.Equals(command.Name, StringComparison.OrdinalIgnoreCase) ||
            Defaults.UserAdmin.Equals(command.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new WildGooseFriendlyException(1, "禁止使用系统角色名");
        }

        if (await roleManager.RoleExistsAsync(command.Name))
        {
            throw new WildGooseFriendlyException(1, "角色已经存在");
        }

        var role = new WildGoose.Domain.Entity.Role(command.Name)
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Version = 1,
            Description = command.Description,
            Statement = "[]"
        };
        var roleResult = await roleManager.CreateAsync(role);
        roleResult.CheckErrors();
        // roleManager.CreateAsync 会调用 DbContext.SaveChangesAsync
        // await DbContext.SaveChangesAsync();
        return role.Id;
    }

    public async Task DeleteAsync(DeleteRoleCommand command)
    {
        var role = await DbContext.Set<WildGoose.Domain.Entity.Role>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (role == null)
        {
            throw new WildGooseFriendlyException(1, "角色不存在");
        }

        if (Defaults.AdminRole.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.OrganizationAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.UserAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new WildGooseFriendlyException(1, "禁止操作系统角色信息");
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync();
        try
        {
            await DbContext.Set<IdentityUserRole<string>>().Where(x => x.RoleId == role.Id)
                .ExecuteDeleteAsync();
            await DbContext.Set<WildGoose.Domain.Entity.Role>().Where(x => x.Id == role.Id)
                .ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "删除角色失败");
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception e2)
            {
                Logger.LogError(e2, "删除角色回滚失败");
            }

            throw new WildGooseFriendlyException(1, "删除角色失败");
        }
    }

    public async Task UpdateAsync(UpdateRoleCommand command)
    {
        var role = await roleManager.FindByIdAsync(command.Id);
        if (role == null)
        {
            throw new WildGooseFriendlyException(1, "角色不存在");
        }

        if (Defaults.AdminRole.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.OrganizationAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.UserAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new WildGooseFriendlyException(1, "禁止操作系统角色信息");
        }

        role.Name = command.Name;
        role.NormalizedName = roleManager.NormalizeKey(command.Name);
        role.Description = command.Description;

        // if (await DbContext.Set<WildGoose.Domain.Entity.Role>()
        //         .AnyAsync(x => x.NormalizedName == role.NormalizedName && x.Id != role.Id))
        // {
        //     throw new WildGooseFriendlyException(1, "角色名已经存在");
        // }

        // roleManager.UpdateAsync 会校验角色名重复
        (await roleManager.UpdateAsync(role)).CheckErrors();
        // await DbContext.SaveChangesAsync();
    }

    public async Task UpdateStatementAsync(UpdateStatementCommand command)
    {
        var role = await DbContext.Set<WildGoose.Domain.Entity.Role>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (role == null)
        {
            throw new WildGooseFriendlyException(1, "角色不存在");
        }

        if (Defaults.AdminRole.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.OrganizationAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.UserAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new WildGooseFriendlyException(1, "禁止操作系统角色信息");
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
        };
        role.Statement = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(command.Statement), options);
        await DbContext.SaveChangesAsync();
    }

    public async Task<PagedResult<RoleDto>> GetRolesAsync(GetRolesQuery query)
    {
        var queryable = DbContext.Set<WildGoose.Domain.Entity.Role>()
            .AsNoTracking();
        if (!string.IsNullOrEmpty(query.Q))
        {
            queryable = queryable.Where(x => x.Name.Contains(query.Q));
        }

        var result = await queryable.OrderByDescending(x => x.LastModificationTime).Select(x => new
            {
                x.Id,
                x.Name,
                x.Version,
                x.Description,
                x.LastModificationTime
            })
            .PagedQueryAsync(query.Page, query.Limit);

        var dtoList = result.Data.Select(x => new RoleDto
        {
            Id = x.Id,
            Name = x.Name,
            Version = x.Version,
            Description = x.Description,
            LastModificationTime = x.LastModificationTime.HasValue
                ? x.LastModificationTime.Value.ToLocalTime().ToString(Defaults.SecondTimeFormat)
                : "-"
        }).ToList();
        var roleIdList = result.Data.Select(x => x.Id).ToList();
        var t1 = DbContext.Set<RoleAssignableRole>();
        var t2 = DbContext.Set<WildGoose.Domain.Entity.Role>();
        var assignableRoles = await (from relationship in t1
            join role in t2 on relationship.AssignableId equals role.Id
            where roleIdList.Contains(relationship.RoleId)
            select new
            {
                relationship.RoleId,
                relationship.AssignableId,
                AssignableName = role.Name
            }).ToListAsync();

        foreach (var roleDto in dtoList)
        {
            roleDto.AssignableRoles = assignableRoles.Where(x => x.RoleId == roleDto.Id)
                .Select(x => new RoleBasicDto
                {
                    Id = x.AssignableId, Name = x.AssignableName
                }).ToList();
        }

        return new PagedResult<RoleDto>(result.Page, result.Limit, result.Total, dtoList);
    }

    public async Task<RoleDto> GetAsync(GetRoleQuery query)
    {
        var role = await DbContext.Set<WildGoose.Domain.Entity.Role>()
            .AsNoTracking()
            .Where(x => x.Id == query.Id)
            .FirstOrDefaultAsync();
        if (role == null)
        {
            return null;
        }

        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Version = role.Version,
            Description = role.Description,
            Statement = string.IsNullOrEmpty(role.Statement)
                ? "[]"
                : role.Statement,
            LastModificationTime = role.LastModificationTime.HasValue
                ? role.LastModificationTime.Value.ToString(Defaults.SecondTimeFormat)
                : "-"
        };
    }

    public async Task AddAssignableRoleAsync(AddAssignableRoleCommand command)
    {
        var role = await DbContext.Set<WildGoose.Domain.Entity.Role>()
            .AsNoTracking()
            .Where(x => x.Id == command.Id)
            .FirstOrDefaultAsync();
        if (role == null)
        {
            return;
        }

        if (Defaults.AdminRole.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.OrganizationAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
            Defaults.UserAdmin.Equals(role.NormalizedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new WildGooseFriendlyException(1, "禁止操作系统角色信息");
        }

        var existList = await DbContext.Set<RoleAssignableRole>().AsNoTracking()
            .Where(x => x.RoleId == command.Id)
            .Select(x => x.AssignableId)
            .ToListAsync();

        foreach (var assignableRoleId in command.AssignableRoleIds)
        {
            var exists = existList.Contains(assignableRoleId);
            if (exists)
            {
                continue;
            }

            var relationship = new RoleAssignableRole
            {
                RoleId = command.Id,
                AssignableId = assignableRoleId
            };
            await DbContext.AddAsync(relationship);
        }

        await DbContext.SaveChangesAsync();
    }

    public async Task DeleteAssignableRoleAsync(DeleteAssignableRoleCommand command)
    {
        var relationship = await DbContext.Set<RoleAssignableRole>()
            .FirstOrDefaultAsync(x => x.RoleId == command.Id && x.AssignableId == command.AssignableRoleId);
        if (relationship == null)
        {
            throw new WildGooseFriendlyException(1, "数据不存在");
        }

        DbContext.Remove(relationship);
        await DbContext.SaveChangesAsync();
    }

    public async Task<List<RoleBasicDto>> GetAssignableRolesAsync()
    {
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return DbContext.Set<WildGoose.Domain.Entity.Role>()
                .AsNoTracking()
                .Where(x => x.Name != Defaults.OrganizationAdmin)
                .Select(x => new RoleBasicDto
                {
                    Id = x.Id,
                    Name = x.Name
                }).ToList();
        }

        var userId = Session.UserId;
        var roles = await (from userRole in DbContext.Set<IdentityUserRole<string>>()
            join roleAssignableRole in DbContext.Set<RoleAssignableRole>() on userRole.RoleId equals roleAssignableRole
                .RoleId
            join role in DbContext.Set<WildGoose.Domain.Entity.Role>() on roleAssignableRole.AssignableId equals role.Id
            where userRole.UserId == userId && role.Name != Defaults.OrganizationAdmin &&
                  role.Name != Defaults.AdminRole
            select new RoleBasicDto
            {
                Id = role.Id,
                Name = role.Name
            }).AsNoTracking().ToListAsync();
        roles = roles.DistinctBy(x => x.Id).ToList();
        return roles;
    }
}