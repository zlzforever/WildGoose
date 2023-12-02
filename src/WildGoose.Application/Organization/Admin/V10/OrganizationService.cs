using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Dto;
using WildGoose.Application.Extensions;
using WildGoose.Application.Organization.Admin.V10.Command;
using WildGoose.Application.Organization.Admin.V10.Dto;
using WildGoose.Application.Organization.Admin.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Organization.Admin.V10;

public class OrganizationService : BaseService
{
    private readonly UserManager<WildGoose.Domain.Entity.User> _userManager;

    public OrganizationService(WildGooseDbContext dbContext, HttpSession session, IOptions<DbOptions> dbOptions,
        ILogger<OrganizationService> logger, UserManager<WildGoose.Domain.Entity.User> userManager) : base(dbContext,
        session, dbOptions,
        logger)
    {
        _userManager = userManager;
    }

    public async Task<OrganizationSimpleDto> AddAsync(AddOrganizationCommand command)
    {
        var organization = Create(command);

        var store = DbContext.Set<WildGoose.Domain.Entity.Organization>();

        if (await store.AnyAsync(x => x.Name == command.Name))
        {
            throw new WildGooseFriendlyException(1, "机构名重复");
        }

        if (Session.IsSupperAdmin())
        {
            await SetParentAsync(organization, command.ParentId);
            await store.AddAsync(organization);
        }
        else
        {
            // 只有超管可以创建一级机构
            if (string.IsNullOrEmpty(command.ParentId))
            {
                throw new WildGooseFriendlyException(1, "权限不足");
            }

            if (!await DbContext.AllPermissionAsync(Session.UserId, command.ParentId))
            {
                throw new WildGooseFriendlyException(1, "权限不足");
            }

            await SetParentAsync(organization, command.ParentId);
            if (organization.Parent == null)
            {
                throw new WildGooseFriendlyException(1, "上级机构不存在");
            }

            await store.AddAsync(organization);
        }

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
        await VerifyOrganizationPermissionAsync(id);

        // TODO: 自己删除自己所在的机构？
        var store = DbContext.Set<WildGoose.Domain.Entity.Organization>();
        var organization = await store.FirstOrDefaultAsync(x => x.Id == id);
        if (organization == null)
        {
            throw new WildGooseFriendlyException(1, "机构不存在");
        }

        if (await DbContext.Set<WildGoose.Domain.Entity.Organization>()
                .Where(x => x.Parent.Id == id)
                .AnyAsync())
        {
            throw new WildGooseFriendlyException(1, "请先删除下属机构");
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
            var t3 = DbContext.Set<OrganizationUser>().EntityType.GetTableName();
            var conn = DbContext.Database.GetDbConnection();
            await conn.ExecuteAsync(
                $$"""
                  DELETE FROM {{t1}} WHERE organization_id = @Id;
                  DELETE FROM {{t3}} WHERE organization_id = @Id;
                  """, new { Id = id });
            store.Remove(organization);

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
        }

        return organization.Id;
    }

    public async Task<OrganizationSimpleDto> UpdateAsync(UpdateOrganizationCommand command)
    {
        // await VerifyOrganizationPermissionAsync(command.Id, command.ParentId);

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

    public async Task AddAdministratorAsync(AddAdministratorCommand command)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await VerifyOrganizationPermissionAsync(command.Id);

        var relationship = new OrganizationAdministrator
        {
            OrganizationId = command.Id,
            UserId = command.UserId
        };
        // TODO: 如果多次添加是报异常还是？
        await _userManager.AddToRoleAsync(user, Defaults.OrganizationAdmin);
        await DbContext.AddAsync(relationship);
        await DbContext.SaveChangesAsync();
    }

    public async Task DeleteAdministratorAsync(DeleteAdministratorCommand command)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await VerifyOrganizationPermissionAsync(command.Id);

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
                await _userManager.RemoveFromRoleAsync(user, Defaults.OrganizationAdmin);
            }
        }

        await DbContext.SaveChangesAsync();
    }

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

    public async Task<List<SubOrganizationDto>> GetSubListAsync(GetSubListQuery query)
    {
        // TODO:
        // await VerifyOrganizationPermissionAsync(query.ParentId);

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

        var t1 = DbContext.Set<OrganizationTree>();
        var t2 = DbContext.Set<OrganizationAdministrator>();
        var queryable = from org in t1
            join adminRelationship in t2 on org.Id equals adminRelationship.OrganizationId
            where adminRelationship.UserId == Session.UserId
            select org;
        if (!string.IsNullOrEmpty(query.ParentId))
        {
            queryable = queryable.Where(x => x.ParentId == query.ParentId);
        }

        // 先通过 branch 排序， 长度相同的， 短的排在前面
        var organizations = await queryable
            .AsNoTracking()
            .OrderBy(x => x.Branch)
            .Select(organization => new SubOrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                ParentId = organization.ParentId,
                ParentName = organization.ParentName,
                Branch = organization.Branch,
                HasChild = DbContext
                    .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                    .Any(x => x.Parent.Id == organization.Id)
            })
            .ToListAsync();
        var result = new List<SubOrganizationDto>();
        foreach (var organization in organizations)
        {
            // 任何上级机构已经存在， 则不添加
            if (result.Any(x => organization.Branch.StartsWith(x.Branch)))
            {
                continue;
            }

            result.Add(organization);
        }

        return result;
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
                x.Id, x.Name, x.Code, x.Address, x.Description,
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

    class Tmp
    {
        public string Id { get; set; }
        public string Value { get; set; }
    }

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

    private WildGoose.Domain.Entity.Organization Create(
        AddOrganizationCommand command)
    {
        var organization = new WildGoose.Domain.Entity.Organization
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = command.Name,
            Code = command.Code,
            Address = command.Address,
            Description = command.Description
        };
        return organization;
    }

    private async Task SetParentAsync(WildGoose.Domain.Entity.Organization organization,
        string parentId)
    {
        organization.Parent = string.IsNullOrEmpty(parentId)
            ? null
            : await DbContext.Set<WildGoose.Domain.Entity.Organization>().FirstOrDefaultAsync(x => x.Id == parentId);
    }
}