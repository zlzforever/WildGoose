// using Microsoft.AspNetCore.Identity;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
// using MongoDB.Bson;
// using WildGoose.Application.Domain.V10.Command;
// using WildGoose.Application.Domain.V10.Dto;
// using WildGoose.Domain;
// using WildGoose.Domain.Entity;
// using WildGoose.Infrastructure;
//
// namespace WildGoose.Application.Domain.V10;
//
// public class DomainService : BaseService
// {
//     public DomainService(WildGooseDbContext dbContext, HttpSession session, IOptions<DbOptions> dbOptions,
//         ILogger<DomainService> logger) : base(dbContext, session, dbOptions, logger)
//     {
//     }
//
//     public async Task<string> AddAsync(CreateDomainCommand command)
//     {
//         var name = command.Name;
//         var normalizedName = name.ToUpperInvariant();
//
//         var store = DbContext.Set<WildGoose.Domain.Entity.Domain>();
//
//         if (await store.AnyAsync(x => x.NormalizedName == normalizedName))
//         {
//             throw new WildGooseFriendlyException(1, "应用域已经存在");
//         }
//
//         var domain = new WildGoose.Domain.Entity.Domain
//         {
//             Id = ObjectId.GenerateNewId().ToString(),
//             Name = name,
//             NormalizedName = normalizedName,
//             Description = command.Description
//         };
//
//         await store.AddAsync(domain);
//         await DbContext.SaveChangesAsync();
//         return domain.Id;
//     }
//
//     public async Task UpdateAsync(UpdateDomainCommand command)
//     {
//         var store = DbContext.Set<WildGoose.Domain.Entity.Domain>();
//         var domain = await store.FirstOrDefaultAsync(x => x.Id == command.Id);
//         if (domain == null)
//         {
//             throw new WildGooseFriendlyException(1, "应用域不存在");
//         }
//
//         var name = command.Name;
//         var normalizedName = name.ToUpperInvariant();
//
//         if (await store.AnyAsync(x => x.Id != command.Id && x.NormalizedName == normalizedName))
//         {
//             throw new WildGooseFriendlyException(1, "名称已经存在");
//         }
//
//         domain.Name = name;
//         domain.NormalizedName = normalizedName;
//
//         await DbContext.SaveChangesAsync();
//     }
//
//     public async Task DeleteAsync(string id)
//     {
//         var store = DbContext.Set<WildGoose.Domain.Entity.Domain>();
//         var domain = await store.FirstOrDefaultAsync(x => x.Id == id);
//         if (domain == null)
//         {
//             throw new WildGooseFriendlyException(1, "应用域不存在");
//         }
//
//         if (await DbContext.Set<DomainRole>().AnyAsync())
//         {
//             throw new WildGooseFriendlyException(1, "请先清空应用域内数据");
//         }
//
//         store.Remove(domain);
//         await DbContext.SaveChangesAsync();
//     }
//
//     public async Task<IEnumerable<DomainDto>> GetListAsync()
//     {
//         var store = DbContext.Set<WildGoose.Domain.Entity.Domain>();
//         return await store.AsNoTracking().Select(x => new DomainDto
//         {
//             Id = x.Id,
//             Name = x.Name,
//             Description = x.Description
//         }).ToListAsync();
//     }
//
//     public async Task<IEnumerable<RoleDto>> GetRoleListAsync(string id)
//     {
//         var store = DbContext.Set<WildGoose.Domain.Entity.Role>().AsNoTracking();
//         var queryable = from role in store
//             join roleDomain in DbContext.Set<DomainRole>() on role.Id equals roleDomain.RoleId
//             where roleDomain.DomainId == id
//             select new RoleDto
//             {
//                 Id = role.Id,
//                 Name = role.Name
//             };
//         return await queryable.ToListAsync();
//     }
// }