using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
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
    public static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
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
            var logPath = builder.Configuration["LOG_PATH"] ?? builder.Configuration["LOGPATH"];
            if (string.IsNullOrEmpty(logPath))
            {
                logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs/log.txt");
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Async(x => x.File(logPath, rollingInterval: RollingInterval.Day))
                .CreateLogger();
        }

        builder.Logging.AddSerilog();
        return builder;
    }

    public static void RegisterServices(this WebApplicationBuilder builder)
    {
        // builder.Services.TryAddScoped<DomainService>();
        builder.Services.TryAddScoped<OrganizationService>();
        builder.Services.TryAddScoped<WildGoose.Application.Organization.V10.OrganizationService>();
        builder.Services.TryAddScoped<UserService>();
        builder.Services.TryAddScoped<WildGoose.Application.User.Admin.V11.UserService>();
        builder.Services.TryAddScoped<RoleService>();
        builder.Services.TryAddScoped<IObjectStorageService, ObjectStorageService>();
        builder.Services.TryAddScoped<Application.User.V10.UserService>();
        builder.Services.TryAddScoped<ISession, HttpSession>();
        builder.Services.TryAddScoped<PermissionService>();
        builder.Services.TryAddSingleton<ScopeServiceProvider, HttpContextScopeServiceProvider>();
        builder.Services.TryAddScoped<HttpSession>(provider =>
            HttpSession.Create(provider.GetRequiredService<IHttpContextAccessor>()));
        builder.Services.AddHostedService<GenerateTopLevelOrgService>();
    }
}