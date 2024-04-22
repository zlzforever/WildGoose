using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Dto;
using WildGoose.Application.Organization.Admin.V10.Command;
using WildGoose.Application.Organization.Admin.V10.Dto;
using WildGoose.Application.Organization.Admin.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Organization.Admin.V10;

public class OrganizationService(
    WildGooseDbContext dbContext,
    HttpSession session,
    IOptions<DbOptions> dbOptions,
    ILogger<OrganizationService> logger,
    UserManager<WildGoose.Domain.Entity.User> userManager)
    : BaseService(dbContext,
        session, dbOptions,
        logger)
{
    public async Task<OrganizationSimpleDto> AddAsync(AddOrganizationCommand command)
    {
        // 只有超级管理员可以创建一级机构
        if (string.IsNullOrEmpty(command.ParentId))
        {
            if (!Session.IsSupperAdmin())
            {
                throw new WildGooseFriendlyException(1, "权限不足")
                {
                    LogInfo = "只有超级管理员可以创建一级机构"
                };
            }
        }
        else
        {
            if (!await HasOrganizationPermissionAsync(command.ParentId))
            {
                throw new WildGooseFriendlyException(1, "权限不足");
            }
        }

        // var store = DbContext.Set<WildGoose.Domain.Entity.Organization>();
        // if (await store.AnyAsync(x => x.Name == command.Name))
        // {
        //     throw new WildGooseFriendlyException(1, "机构名重复");
        // }

        var organization = Create(command);
        await SetParentAsync(organization, command.ParentId);

        // 检查上级机构是否存在， 防止无权限的用户提供一个不存在的上级机构， 从而创建一级机构
        if (!Session.IsSupperAdmin() && organization.Parent == null)
        {
            throw new WildGooseFriendlyException(1, "上级机构不存在");
        }

        await DbContext.AddAsync(organization);

        foreach (var scope in command.Scope)
        {
            DbContext.Add(new OrganizationScope
            {
                OrganizationId = organization.Id,
                Scope = scope
            });
        }

        await DbContext.SaveChangesAsync();

        return new OrganizationSimpleDto
        {
            Id = organization.Id,
            Name = organization.Name,
            ParentId = organization.Parent?.Id,
            HasChild = false
        };
    }

    public async Task<string> DeleteAsync(string id)
    {
        var store = DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .Include(x => x.Parent);
        var organization = await store.FirstOrDefaultAsync(x => x.Id == id);
        if (organization == null)
        {
            throw new WildGooseFriendlyException(1, "机构不存在");
        }

        // 只有超级管理员可以删除一级机构
        if (string.IsNullOrEmpty(organization.Parent?.Id))
        {
            if (!Session.IsSupperAdmin())
            {
                throw new WildGooseFriendlyException(1, "权限不足")
                {
                    LogInfo = "只有超级管理员可以创建一级机构"
                };
            }
        }
        else
        {
            if (!await HasOrganizationPermissionAsync(organization.Parent?.Id))
            {
                throw new WildGooseFriendlyException(1, "权限不足");
            }
        }

        if (await DbContext.Set<WildGoose.Domain.Entity.Organization>()
                .Where(x => x.Parent.Id == id)
                .AnyAsync())
        {
            throw new WildGooseFriendlyException(1, "请先删除所有子机构再进行删除机构操作");
        }

        // comments 只是关系， 直接删除即可
        // var containsUser = await DbContext.Set<OrganizationUser>()
        //     .AsNoTracking()
        //     .Where(x => x.OrganizationId == id)
        //     .AnyAsync();
        //
        // if (containsUser)
        // {
        //     throw new WildGooseFriendlyException(1, "请先删除机构下的用户");
        // }

        await using var transaction = await DbContext.Database.BeginTransactionAsync();

        try
        {
            // 删除机构下的用户
            var t1 = DbContext.Set<OrganizationAdministrator>().EntityType.GetTableName();
            var t2 = DbContext.Set<OrganizationScope>().EntityType.GetTableName();
            var t3 = DbContext.Set<OrganizationUser>().EntityType.GetTableName();
            var conn = DbContext.Database.GetDbConnection();
            await conn.ExecuteAsync(
                $$"""
                  DELETE FROM {{t1}} WHERE organization_id = @Id;
                  DELETE FROM {{t2}} WHERE organization_id = @Id;
                  DELETE FROM {{t3}} WHERE organization_id = @Id;
                  """, new { Id = id });
            DbContext.Remove(organization);

            await DbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            Logger.LogError("删除机构失败 {Exception}", e);
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception e1)
            {
                Logger.LogError("执行回滚失败 {Exception}", e1);
            }

            throw new WildGooseFriendlyException(1, "删除失败");
        }

        return organization.Id;
    }

    public async Task<OrganizationSimpleDto> UpdateAsync(UpdateOrganizationCommand command)
    {
        if (!await HasOrganizationPermissionAsync(command.Id) || !await HasOrganizationPermissionAsync(command.ParentId))
        {
            throw new WildGooseFriendlyException(1, "权限不足");
        }

        // parentId
        var store = DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .Include(x => x.Parent);
        var organization = await store.FirstOrDefaultAsync(x => x.Id == command.Id);
        if (organization == null)
        {
            throw new WildGooseFriendlyException(1, "机构不存在");
        }

        organization.Code = command.Code;
        organization.Name = command.Name;
        organization.Address = command.Address;
        organization.Description = command.Description;

        // 只有超级管理员可以修改元数据
        if (Session.IsSupperAdmin())
        {
            organization.SetMetadata(command.Metadata);
        }

        await SetParentAsync(organization, command.ParentId);

        var administrators = await (from relationship in DbContext.Set<OrganizationUser>()
                .AsNoTracking()
                .Where(x => x.OrganizationId == command.Id && command.Administrators.Contains(x.UserId))
            join user in DbContext.Set<WildGoose.Domain.Entity.User>() on relationship.UserId equals user.Id
            select new UserDto
            {
                Id = user.Id,
                Name = user.Name
            }).ToListAsync();

        var organizationAdministrators = await DbContext.Set<OrganizationAdministrator>()
            .Where(x => x.OrganizationId == organization.Id)
            .ToListAsync();
        DbContext.RemoveRange(organizationAdministrators);

        // TODO: 检查人员是否在机构中
        foreach (var administrator in administrators)
        {
            DbContext.Add(new OrganizationAdministrator
            {
                OrganizationId = organization.Id,
                UserId = administrator.Id
            });
        }

        var scope = await DbContext.Set<OrganizationScope>()
            .Where(x => x.OrganizationId == organization.Id)
            .ToListAsync();
        DbContext.RemoveRange(scope);
        foreach (var v in command.Scope)
        {
            DbContext.Add(new OrganizationScope
            {
                OrganizationId = organization.Id,
                Scope = v
            });
        }

        await DbContext.SaveChangesAsync();
        var dto = new OrganizationSimpleDto
        {
            Id = organization.Id,
            Name = organization.Name,
            ParentId = organization.Parent?.Id,
            HasChild = DbContext
                .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                .Any(x => x.Parent.Id == organization.Id)
        };

        return dto;
    }

    public async Task<OrganizationDetailDto> GetAsync(GetDetailQuery query)
    {
        // await VerifyOrganizationPermissionAsync(query.Id);

        var organization = await DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .AsNoTracking()
            .Where(x => x.Id == query.Id)
            .Select(x => new
            {
                x.Id, x.Name, x.Code, x.Address, x.Description, x.Metadata,
                ParentId = x.Parent.Id,
                ParentName = x.Parent.Name,
                ParentParentId = x.Parent.Parent.Id
            })
            .FirstOrDefaultAsync();

        if (organization == null)
        {
            return null;
        }

        var dto = new OrganizationDetailDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Code = organization.Code,
            Address = organization.Address,
            Description = organization.Description,
            Metadata = string.IsNullOrEmpty(organization.Metadata)
                ? string.Empty
                : organization.Metadata
        };
        if (!string.IsNullOrEmpty(organization.ParentId))
        {
            dto.Parent = new OrganizationSimpleDto
            {
                Id = organization.ParentId,
                Name = organization.ParentName,
                HasChild = true,
                ParentId = organization.ParentParentId
            };
        }

        var scopeQueryable = DbContext
            .Set<OrganizationScope>()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id)
            .Select(x => new
            {
                Id = "",
                Value = x.Scope
            });

        var administratorQueryable = from admin in DbContext.Set<OrganizationAdministrator>()
            join user in DbContext.Set<WildGoose.Domain.Entity.User>() on admin.UserId equals user.Id
            where admin.OrganizationId == organization.Id
            select new
            {
                Id = Convert.ToString(admin.UserId),
                Value = user.Name
            };
        var unionList = await administratorQueryable.Union(scopeQueryable).AsNoTracking().ToListAsync();
        dto.Scope = new List<string>();
        dto.Administrators = new List<UserDto>();
        foreach (var tmp in unionList)
        {
            if (string.IsNullOrEmpty(tmp.Id))
            {
                dto.Scope.Add(tmp.Value);
            }
            else
            {
                dto.Administrators.Add(new UserDto
                {
                    Id = tmp.Id,
                    Name = tmp.Value
                });
            }
        }

        return dto;
    }

    public async Task<List<SubOrganizationDto>> GetSubListAsync(GetSubListQuery query)
    {
        if (Session.IsSupperAdmin())
        {
            return await DbContext
                .Set<WildGoose.Domain.Entity.Organization>()
                .Include(x => x.Parent)
                .AsNoTracking()
                .Where(x => x.Parent.Id == query.ParentId)
                .OrderBy(x => x.Code)
                .Select(organization => new SubOrganizationDto
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    ParentId = organization.Parent.Id,
                    ParentName = organization.Parent.Name,
                    HasChild = DbContext
                        .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                        .Any(x => x.Parent.Id == organization.Id)
                }).ToListAsync();
        }

        // 查询管理的机构
        if (string.IsNullOrEmpty(query.ParentId))
        {
            var organizations = await GetAdminOrganizationsAsync();
            var idList = organizations.Select(x => x.Id);
            return await DbContext
                .Set<WildGoose.Domain.Entity.Organization>()
                .Include(x => x.Parent)
                .AsNoTracking()
                .Where(x => idList.Contains(x.Id))
                .OrderBy(x => x.Code)
                .Select(organization => new SubOrganizationDto
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    ParentId = organization.Parent.Id,
                    ParentName = organization.Parent.Name,
                    HasChild = DbContext
                        .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                        .Any(x => x.Parent.Id == organization.Id)
                }).ToListAsync();
        }

        if (await HasOrganizationPermissionAsync(query.ParentId))
        {
            return await DbContext
                .Set<WildGoose.Domain.Entity.Organization>()
                .Include(x => x.Parent)
                .AsNoTracking()
                .Where(x => x.Parent.Id == query.ParentId)
                .OrderBy(x => x.Code)
                .Select(organization => new SubOrganizationDto
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    ParentId = organization.Parent.Id,
                    ParentName = organization.Parent.Name,
                    HasChild = DbContext
                        .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                        .Any(x => x.Parent.Id == organization.Id)
                }).ToListAsync();
        }

        Logger.LogWarning("用户 {UserId} 无访问机构 {OrganizationId} 的权限", Session.UserId, query.ParentId);
        return new List<SubOrganizationDto>();
    }

    public async Task AddAdministratorAsync(AddAdministratorCommand command)
    {
        if (!await HasOrganizationPermissionAsync(command.Id))
        {
            throw new WildGooseFriendlyException(1, "权限不足");
        }

        var user = await userManager.FindByIdAsync(command.UserId);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        var relationship = new OrganizationAdministrator
        {
            OrganizationId = command.Id,
            UserId = command.UserId
        };
        // TODO: 如果多次添加是报异常还是？
        await userManager.AddToRoleAsync(user, Defaults.OrganizationAdmin);
        await DbContext.AddAsync(relationship);
        await DbContext.SaveChangesAsync();
    }

    public async Task DeleteAdministratorAsync(DeleteAdministratorCommand command)
    {
        if (!await HasOrganizationPermissionAsync(command.Id))
        {
            throw new WildGooseFriendlyException(1, "权限不足");
        }

        var user = await userManager.FindByIdAsync(command.UserId);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        if (!await HasOrganizationPermissionAsync(command.Id))
        {
            throw new WildGooseFriendlyException(1, "权限不足");
        }

        var relationships = await DbContext.Set<OrganizationAdministrator>()
            .Where(x => x.UserId == command.UserId)
            .ToListAsync();
        var relationship = relationships.FirstOrDefault(x => x.OrganizationId == command.Id);

        if (relationship != null)
        {
            DbContext.Remove(relationship);

            // 若用户没有作为机构管理员， 则删除对应的角色
            if (relationships.Count == 1)
            {
                await userManager.RemoveFromRoleAsync(user, Defaults.OrganizationAdmin);
            }
        }

        await DbContext.SaveChangesAsync();
    }


    private WildGoose.Domain.Entity.Organization Create(
        AddOrganizationCommand command)
    {
        var organization = new WildGoose.Domain.Entity.Organization
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = command.Name,
            Code = command.Code,
            Address = command.Address,
            Description = command.Description,
        };
        // 只有超级管理员可以修改元数据
        if (Session.IsSupperAdmin())
        {
            organization.SetMetadata(command.Metadata);
        }

        return organization;
    }

    private async Task SetParentAsync(WildGoose.Domain.Entity.Organization organization,
        string parentId)
    {
        organization.Parent = string.IsNullOrEmpty(parentId)
            ? null
            : await DbContext.Set<WildGoose.Domain.Entity.Organization>().FirstOrDefaultAsync(x => x.Id == parentId);
    }

//     private Task<bool> HasParentPermissionAsync(string organizationId)
//     {
//         var tb1 = DbContext.Set<WildGoose.Domain.Entity.Organization>().EntityType.GetTableName();
//         var tb2 = DbContext.Set<OrganizationAdministrator>().EntityType.GetTableName();
//         var sql = $$"""
//                     SELECT id, name, parent_id FROM {{tb1}} WHERE id = (SELECT parent_id FROM {{tb1}} WHERE id = @OrganizationId);
//                     WITH RECURSIVE recursion AS
//                                        (SELECT t1.id, t1.name, t1.parent_id, true as leaf
//                                         from {{tb1}} t1
//                                         where (t1.id in (select distinct organization_id
//                                                         from {{tb2}}
//                                                         where user_id = @UserId) or t1.id = (SELECT parent_id FROM {{tb1}} WHERE id = @OrganizationId)) and t1.is_deleted <> true
//                                         UNION ALL
//                                         SELECT t2.id, t2.name, t2.parent_id, false
//                                         from {{tb1}} t2,
//                                              recursion t3
//                                         WHERE t2.id = t3.parent_id)
//                     SELECT distinct *
//                     FROM recursion T;
//                     """;
//         return HasPermissionAsync(sql, organizationId);
//     }

    // private async ValueTask<bool> HasPermissionAsync(string sql, string organizationId)
    // {
    //     var gridReader = await DbContext.Database.GetDbConnection()
    //         .QueryMultipleAsync(sql, new { Session.UserId, OrganizationId = organizationId });
    //     var checkOrganization = await gridReader.ReadSingleOrDefaultAsync<OrganizationEntity>();
    //
    //     var organizations = new List<OrganizationEntity>();
    //     var entityDict = new Dictionary<string, OrganizationEntity>();
    //     foreach (var entity in await gridReader.ReadAsync<OrganizationEntity>())
    //     {
    //         if (entity.Leaf)
    //         {
    //             organizations.Add(entity);
    //         }
    //
    //         entityDict.TryAdd(entity.Id, entity);
    //     }
    //
    //     if (checkOrganization == null)
    //     {
    //         return false;
    //     }
    //
    //     checkOrganization.BuildPath(entityDict);
    //     // 5
    //     var checkPath = checkOrganization.Path;
    //     foreach (var kv in entityDict)
    //     {
    //         var organization = kv.Value;
    //         organization.BuildPath(entityDict);
    //     }
    //
    //     // 5/2
    //     return organizations.Any(x => checkPath.StartsWith(x.Path));
    // }

    // public async Task AddDefaultRoleAsync(AddDefaultRoleCommand command)
    // {
    //     await VerifyOrganizationPermissionAsync(command.Id);
    //
    //     var relationship = new OrganizationRole
    //     {
    //         OrganizationId = command.Id,
    //         RoleId = command.RoleId
    //     };
    //
    //     await DbContext.AddAsync(relationship);
    //     await DbContext.SaveChangesAsync();
    // }
    //
    // public async Task DeleteDefaultRoleAsync(DeleteDefaultRoleCommand command)
    // {
    //     await VerifyOrganizationPermissionAsync(command.Id);
    //
    //     var relationship = await DbContext.Set<OrganizationRole>()
    //         .FirstOrDefaultAsync(x => x.OrganizationId == command.Id && x.RoleId == command.RoleId);
    //
    //     if (relationship != null)
    //     {
    //         DbContext.Remove(relationship);
    //         await DbContext.SaveChangesAsync();
    //     }
    // }

    // public async Task<List<OrganizationDto>> GetListAsync(string parentId)
    // {
    //     await using var conn = DbContext.Database.GetDbConnection();
    //     var organizations = await DbContext.Set<OrganizationTree>()
    //         .AsNoTracking()
    //         .Where(x => x.ParentId == parentId)
    //         .ToListAsync();
    //     return organizations.Select(x => new OrganizationDto
    //     {
    //         Id = x.Id,
    //         Name = x.Name,
    //         Code = x.Code,
    //         ParentId = x.ParentId,
    //         ParentName = x.ParentName,
    //         Branch = x.Branch
    //     }).ToList();
    // }

    // public async Task<OrganizationDetailDto> GetAsync(string id)
    // {
    //     await VerifyOrganizationPermissionAsync(id);
    //
    //     var organization = await DbContext.Set<WildGoose.Domain.Entity.Organization>()
    //         .Include(x => x.Parent)
    //         // .ThenInclude(x=>x.Parent)
    //         .AsNoTracking()
    //         .Where(x => x.Id == id)
    //         .FirstOrDefaultAsync();
    //     var dto = new OrganizationDetailDto
    //     {
    //         Id = organization.Id,
    //         Name = organization.Name,
    //         Code = organization.Code,
    //         Address = organization.Address,
    //         Description = organization.Description,
    //     };
    //     if (organization.Parent != null)
    //     {
    //         dto.Parent = new OrganizationSimpleDto
    //         {
    //             Id = organization.Parent.Id,
    //             Name = organization.Parent.Name,
    //             HasChild = DbContext
    //                 .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
    //                 .Any(x => x.Parent.Id == organization.Parent.Id),
    //             ParentId = organization.Parent.Id
    //         };
    //     }
    //
    //     return dto;
    // }
}