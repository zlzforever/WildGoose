using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
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
            throw new WildGooseFriendlyException(403, "访问受限");
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            command.Password = PasswordGenerator.GeneratePassword();
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
        Utils.SetPasswordInfo(userExtension, command.Password);
        await DbContext.AddAsync(userExtension);

        var result = await userManager.CreateAsync(user, command.Password);
        // comments by lewis 20231117: _userManager 会自己调用 SaveChanges
        result.CheckErrors();
        await DbContext.SaveChangesAsync();

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