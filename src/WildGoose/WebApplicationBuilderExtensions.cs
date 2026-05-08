using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Serilog.Events;
using WildGoose.Application;
using WildGoose.Application.OSS;
using WildGoose.Application.Permission.Internal.V10;
using WildGoose.Application.Services;
using WildGoose.Application.Services.Admin.Organization.V10;
using WildGoose.Application.Services.Admin.Role.V10;
using WildGoose.Application.Services.Admin.User.V10;
using WildGoose.Application.Services.Organization.V10;
using WildGoose.Application.Services.User.V10;
using WildGoose.Serilog;
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
                    .Configuration(builder.Configuration).Enrich.With(new WildGooseLogEventEnricher())
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
                    .Enrich.With(new WildGooseLogEventEnricher())
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
            builder.Services.TryAddScoped<OrganizationService>();
            builder.Services.TryAddScoped<UserAdminService>();
            builder.Services.TryAddScoped<Application.Services.Admin.User.V11.UserAdminService>();
            builder.Services.TryAddScoped<RoleAdminService>();
            builder.Services.TryAddScoped<ObjectStorageService>();
            builder.Services.TryAddScoped<UserService>();
            builder.Services.TryAddScoped<ISession>(provider =>
                HttpSession.Create(provider.GetRequiredService<IHttpContextAccessor>()));
            builder.Services.TryAddScoped<PermissionService>();
            builder.Services.TryAddSingleton<ScopeServiceProvider>();
            builder.Services.AddHostedService<GenerateTop3LevelOrganizationsToFileService>();
        }
    }
}