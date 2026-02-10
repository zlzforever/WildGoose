using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Extensions;
using WildGoose.Application.User.V10.Command;
using WildGoose.Application.User.V10.Dto;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.User.V10;

public class UserService(
    WildGooseDbContext dbContext,
    ISession session,
    IOptions<DbOptions> dbOptions,
    ILogger<UserService> logger,
    UserManager<WildGoose.Domain.Entity.User> userManager,
    IMemoryCache memoryCache,
    IOptions<JsonOptions> jsonOptions)
    : BaseService(dbContext, session, dbOptions, logger, memoryCache)
{
    public async Task ResetPasswordAsync(ResetPasswordCommand command)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(x =>
            x.Id == Session.UserId);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        // 先注册变化
        var extension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == user.Id);
        if (extension == null)
        {
            extension = new UserExtension { Id = user.Id };
            Utils.SetPasswordInfo(extension, command.NewPassword);
            await DbContext.AddAsync(extension);
        }
        else
        {
            Utils.SetPasswordInfo(extension, command.NewPassword);
        }

        // 若更新密码成功，会同步把 UserExtension 也保存
        var result = await userManager.ChangePasswordAsync(user, command.OriginalPassword, command.NewPassword);
        result.CheckErrors();
    }

    public async Task ResetPasswordByCaptchaAsync(ResetPasswordByCaptchaCommand command)
    {
        var password = command.NewPassword;
        var user = await userManager.Users.FirstOrDefaultAsync(x =>
            x.PhoneNumber == command.PhoneNumber || x.UserName == command.PhoneNumber);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        // 先注册变化
        var extension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == user.Id);
        if (extension == null)
        {
            extension = new UserExtension { Id = user.Id };
            Utils.SetPasswordInfo(extension, password);
            await DbContext.AddAsync(extension);
        }
        else
        {
            Utils.SetPasswordInfo(extension, password);
        }

        // TODO: 要和 STS 统一
        // 若更新密码成功，会同步把 UserExtension 也保存
        (await userManager.ResetPasswordAsync(user, command.Captcha, password)).CheckErrors();
    }

    public async Task<IEnumerable<OrganizationDto>> GetOrganizationsAsync(string userId,
        bool isAdministrator = false)
    {
        IQueryable<OrganizationDetail> queryable;
        var organizationTable = DbContext.Set<OrganizationDetail>();
        var organizationAdministratorTable = DbContext.Set<OrganizationAdministrator>();
        var organizationUserTable = DbContext.Set<OrganizationUser>();
        // 查询用户是机构管理员的机构
        if (isAdministrator)
        {
            queryable = from t1 in organizationTable
                join t2 in organizationUserTable on t1.Id equals t2.OrganizationId
                join t3 in organizationAdministratorTable on t1.Id equals t3.OrganizationId
                where t2.UserId == userId && t3.UserId == userId
                select t1;
        }
        else
        {
            queryable = from t1 in organizationTable
                join t2 in organizationUserTable on t1.Id equals t2.OrganizationId
                where t2.UserId == userId
                select t1;
        }

        var organizationDetails = await queryable
            .AsNoTracking()
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Code)
            .Select(x => new
            {
                Organization = x,
                Scope = DbContext.Set<OrganizationScope>().AsNoTracking()
                    .Where(y => y.OrganizationId == x.Id).Select(z => z.Scope).ToList(),
            }).ToListAsync();
        var jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
        var entities = organizationDetails.Select(x => new OrganizationDto
        {
            Id = x.Organization.Id,
            Name = x.Organization.Name,
            ParentId = x.Organization.ParentId,
            ParentName = x.Organization.ParentName,
            Scope = x.Scope,
            HasChild = x.Organization.HasChild,
            Code = x.Organization.Code,
            Metadata = string.IsNullOrEmpty(x.Organization.Metadata)
                ? default
                : JsonSerializer.Deserialize<JsonElement>(x.Organization.Metadata, jsonSerializerOptions)
        }).ToList();
        return entities;
    }
}