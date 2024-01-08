using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Serilog.Events;
using WildGoose.Application;
using WildGoose.Application.Organization.Admin.V10;
using WildGoose.Application.Permission.Internal.V10;
using WildGoose.Application.Role.Admin.V10;
using WildGoose.Application.User.Admin.V10;
using WildGoose.Domain;
using WildGoose.Infrastructure;
using ISession = WildGoose.Domain.ISession;

namespace WildGoose;

public static class WebApplicationBuilderExtensions
{
    public static void AddSerilog(this WebApplicationBuilder builder)
    {
        var serilogSection = builder.Configuration.GetSection("Serilog");
        if (serilogSection.GetChildren().Any())
        {
            Log.Logger = new LoggerConfiguration().ReadFrom
                .Configuration(builder.Configuration)
                .CreateLogger();
        }
        else
        {
            var logFile = Environment.GetEnvironmentVariable("LOG_PATH");
            if (string.IsNullOrEmpty(logFile))
            {
                logFile = Environment.GetEnvironmentVariable("LOG");
            }

            if (string.IsNullOrEmpty(logFile))
            {
                logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "logs/wild_goose.log".ToLowerInvariant());
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
                // .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                // .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console().WriteTo.Async(x => x.File(logFile, rollingInterval: RollingInterval.Day))
                .CreateLogger();
        }

        builder.Logging.AddSerilog();
    }

    public static void RegisterServices(this WebApplicationBuilder builder)
    {
        // builder.Services.TryAddScoped<DomainService>();
        builder.Services.TryAddScoped<OrganizationService>();
        builder.Services.TryAddScoped<WildGoose.Application.Organization.V10.OrganizationService>();
        builder.Services.TryAddScoped<UserService>();
        builder.Services.TryAddScoped<RoleService>();
        builder.Services.TryAddScoped<IObjectStorageService, ObjectStorageService>();
        builder.Services.TryAddScoped<Application.User.V10.UserService>();
        builder.Services.TryAddScoped<ISession, HttpSession>();
        builder.Services.TryAddScoped<PermissionService>();
        builder.Services.TryAddScoped<HttpSession>(provider =>
            HttpSession.Create(provider.GetRequiredService<IHttpContextAccessor>()));
    }
}