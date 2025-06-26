using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Extensions;
using WildGoose.Application.User.Admin.V10.IntegrationEvents;
using WildGoose.Application.User.Admin.V11.Command;
using WildGoose.Application.User.Admin.V11.Dto;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.User.Admin.V11;

public class UserAdminService(
    WildGooseDbContext dbContext,
    ISession session,
    IOptions<DbOptions> dbOptions,
    ILogger<UserAdminService> logger,
    UserManager<WildGoose.Domain.Entity.User> userManager,
    IOptions<DaprOptions> dapOptions,
    IOptions<WildGooseOptions> wildGooseOptions)
    : BaseService(dbContext, session, dbOptions, logger)
{
    public async Task<UserDto> AddAsync(AddUserCommand command)
    {
        var options = wildGooseOptions.Value;
        if (options.AddUserRoles.Length == 0 || !Session.Roles.Any(x => options.AddUserRoles.Contains(x)))
        {
            throw new WildGooseFriendlyException(403, "访问受限");
        }

        // 
        // userManager.CreateAsync 是会校验的
        // 
        // // 验证密码是否符合要求
        // var passwordValidatorResult =
        //     await passwordValidator.ValidateAsync(userManager, new WildGoose.Domain.Entity.User(),
        //         command.Password);
        // passwordValidatorResult.CheckErrors();

        var normalizedUserName = userManager.NormalizeName(command.UserName);
        if (await userManager.Users.AnyAsync(x => x.NormalizedUserName == normalizedUserName))
        {
            throw new WildGooseFriendlyException(1, "用户名已经存在");
        }
 
        var user = new WildGoose.Domain.Entity.User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            PhoneNumber = command.PhoneNumber,
            UserName = command.UserName,
            Name = command.Name,
            NormalizedUserName = normalizedUserName
        };

        var userExtension = new UserExtension { Id = user.Id };
        Utils.SetPasswordInfo(userExtension, command.Password);
        await DbContext.AddAsync(userExtension);

        var result = await userManager.CreateAsync(user, command.Password);
        // comments by lewis 20231117: _userManager 会自己调用 SaveChanges
        result.CheckErrors();

        var daprClient = GetDaprClient();
        if (daprClient != null && !string.IsNullOrEmpty(dapOptions.Value.Pubsub))
        {
            await daprClient.PublishEventAsync(dapOptions.Value.Pubsub, nameof(UserAddedEvent),
                new UserAddedEvent
                {
                    UserId = user.Id
                });
        }

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Name = user.Name,
            Organizations = [],
            Enabled = !user.LockoutEnabled,
            PhoneNumber = user.PhoneNumber,
            Roles = [],
            IsAdministrator = false,
            CreationTime = user.CreationTime.HasValue
                ? user.CreationTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "-"
        };
    }
}