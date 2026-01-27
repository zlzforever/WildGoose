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
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddSerilog()
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
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .Enrich.WithMachineName()
                    .WriteTo.Console()
                    .WriteTo.Async(x => x.File(logPath, rollingInterval: RollingInterval.Day))
                    .CreateLogger();
            }

            builder.Logging.AddSerilog();
            return builder;
        }

        public void RegisterServices()
        {
            // builder.Services.TryAddScoped<DomainService>();
            builder.Services.TryAddScoped<OrganizationAdminService>();
            builder.Services.TryAddScoped<Application.Organization.V10.OrganizationService>();
            builder.Services.TryAddScoped<UserAdminService>();
            builder.Services.TryAddScoped<WildGoose.Application.User.Admin.V11.UserAdminService>();
            builder.Services.TryAddScoped<RoleAdminService>();
            builder.Services.TryAddScoped<IObjectStorageService, ObjectStorageService>();
            builder.Services.TryAddScoped<Application.User.V10.UserService>();
            builder.Services.TryAddScoped<ISession>(provider =>
                HttpSession.Create(provider.GetRequiredService<IHttpContextAccessor>()));
            builder.Services.TryAddScoped<PermissionService>();
            builder.Services.TryAddSingleton<ScopeServiceProvider, HttpContextScopeServiceProvider>();
            builder.Services.AddHostedService<GenerateTopThreeLevelOrganizationsCacheFileService>();
        }
    }
}