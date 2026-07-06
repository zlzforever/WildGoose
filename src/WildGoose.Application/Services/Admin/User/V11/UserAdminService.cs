using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Identity;
using WildGoose.Application.Services.Admin.User.V10.IntegrationEvents;
using WildGoose.Application.Services.Admin.User.V11.Command;
using WildGoose.Application.Services.Admin.User.V11.Dto;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Domain.Options;
using WildGoose.Domain.Utils;

namespace WildGoose.Application.Services.Admin.User.V11;

public class UserAdminService(
    WildGooseDbContext dbContext,
    ISession session,
    IOptions<DbOptions> dbOptions,
    ILogger<UserAdminService> logger,
    IMemoryCache memoryCache,
    UserManager<WildGoose.Domain.Entity.User> userManager,
    IOptions<DaprOptions> dapOptions,
    IOptions<WildGooseOptions> wildGooseOptions)
    : BaseService(dbContext, session, dbOptions, logger, memoryCache)
{
    public async Task<UserDto> AddAsync(AddUserCommand command)
    {
        var options = wildGooseOptions.Value;
        if (options.AddUserRoles.Length == 0 || !Session.Roles.Any(x => options.AddUserRoles.Contains(x)))
        {
            throw new WildGooseFriendlyException(ErrorCodes.Forbidden);
        }

        var user = new WildGoose.Domain.Entity.User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            PhoneNumber = command.PhoneNumber,
            UserName = command.UserName,
            Name = command.Name,
            //NormalizedUserName = normalizedUserName
        };

        var userExtension = new UserExtension { Id = user.Id };

        var transaction = DbContext.Database.CurrentTransaction ?? await DbContext.Database.BeginTransactionAsync();

        IdentityResult identityResult;
        if (Defaults.DisablePasswordLogin)
        {
            identityResult = await userManager.CreateAsync(user);
        }
        else
        {
            if (string.IsNullOrEmpty(command.Password))
            {
                command.Password = PasswordGenerator.GeneratePassword();
            }

            userExtension.SetPasswordInfo(command.Password);
            identityResult = await userManager.CreateAsync(user, command.Password);
        }

        identityResult.CheckErrors();

        UserExtensionPropertyHelper.SetProperties(userExtension, command.Properties,
            wildGooseOptions.Value.UserPropertyMappings);
        await DbContext.AddAsync(userExtension);

        // comments by lewis 20231117: _userManager 会自己调用 SaveChanges
        identityResult.CheckErrors();
        await DbContext.SaveChangesAsync();

        // 所有操作成功，提交事务
        await transaction.CommitAsync();

        await PublishEventAsync(dapOptions.Value, new UserAddedEvent
        {
            UserId = user.Id
        });

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
                ? user.CreationTime.Value.ToLocalTime().ToString(Defaults.SecondTimeFormat)
                : "-"
        };
    }
}