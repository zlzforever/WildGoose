using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    HttpSession session,
    IOptions<DbOptions> dbOptions,
    ILogger<UserService> logger,
    IPasswordValidator<WildGoose.Domain.Entity.User> passwordValidator,
    UserManager<WildGoose.Domain.Entity.User> userManager,
    IOptions<JsonOptions> jsonOptions)
    : BaseService(dbContext, session, dbOptions, logger)
{
    public async Task ResetPasswordByCaptchaAsync(ResetPasswordByCaptchaCommand command)
    {
        if (command.ConfirmPassword != command.NewPassword)
        {
            throw new WildGooseFriendlyException(1, "两次输入的密码不一致");
        }

        var password = command.NewPassword;
        var passwordValidatorResult =
            await passwordValidator.ValidateAsync(userManager, new WildGoose.Domain.Entity.User(), password);
        passwordValidatorResult.CheckErrors();

        var user = await userManager.Users.FirstOrDefaultAsync(x =>
            x.PhoneNumber == command.PhoneNumber || x.UserName == command.PhoneNumber);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        // var result =
        //     await userManager.VerifyChangePhoneNumberTokenAsync(user, command.Captcha, command.PhoneNumber);
        // if (!result)
        // {
        //     // 验证失败的处理逻辑
        //     throw new WildGooseFriendlyException(1, "验证码不正确");
        // }
        //
        // // 验证成功，可以允许用户更改密码
        // var token = await userManager.GeneratePasswordResetTokenAsync(user);

        (await userManager.ResetPasswordAsync(user, command.Captcha, password)).CheckErrors();

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

        await DbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<OrganizationDto>> GetOrganizationsAsync(string userId, bool isAdministrator = false)
    {
        IQueryable<WildGoose.Domain.Entity.Organization> queryable;
        var organizationTable = DbContext.Set<WildGoose.Domain.Entity.Organization>();
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

        var jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
        var entities = await queryable
            .Include(x => x.Parent)
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new OrganizationDto
            {
                Id = x.Id,
                Name = x.Name,
                ParentId = x.Parent.Id,
                ParentName = x.Parent.Name,
                Scope = DbContext.Set<OrganizationScope>().AsNoTracking()
                    .Where(y => y.OrganizationId == x.Id).Select(z => z.Scope).ToList(),
                HasChild = DbContext
                    .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                    .Any(y => y.Parent.Id == x.Id),
                Code = x.Code,
                Metadata = string.IsNullOrEmpty(x.Metadata)
                    ? default
                    : JsonSerializer.Deserialize<JsonElement>(x.Metadata, jsonSerializerOptions)
            }).ToListAsync();
        return entities;
    }
}