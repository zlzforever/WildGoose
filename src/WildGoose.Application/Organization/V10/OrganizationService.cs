using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Organization.V10.Dto;
using WildGoose.Application.Organization.V10.Queries;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Organization.V10;

public class OrganizationService : BaseService
{
    public OrganizationService(WildGooseDbContext dbContext, HttpSession session, IOptions<DbOptions> dbOptions,
        ILogger logger) : base(dbContext, session, dbOptions, logger)
    {
    }

    /// <summary>
    /// 只返回了机构信息，不含敏感信息，只要登录的就能访问
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public Task<List<SubOrganizationDto>> GetSubListAsync(GetSubListQuery query)
    {
        return DbContext
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
                Metadata = organization.Metadata,
                HasChild = DbContext
                    .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                    .Any(x => x.Parent.Id == organization.Id)
            }).ToListAsync();
    }
}