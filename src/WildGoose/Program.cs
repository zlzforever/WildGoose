using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Identity.Sm;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Logging;
using WildGoose.Application;
using WildGoose.Application.Ef;
using WildGoose.Application.Identity;
using WildGoose.Authentication;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Domain.Options;
using WildGoose.Filters;
using WildGoose.Middlewares;
using WildGoose.Serilog;

namespace WildGoose;

public class Program
{
    public static async Task Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = WebApplication.CreateBuilder(args);
        builder.AddSubstitution();

        builder.AddSerilog();
        var mvcBuilder = builder.Services.AddControllers(x =>
        {
            x.Filters.Add<ResponseWrapperFilter>();
            x.Filters.Add<GlobalExceptionFilter>();
        }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        mvcBuilder.ConfigureApiBehaviorOptions(x =>
        {
            x.InvalidModelStateResponseFactory = InvalidModelStateResponseFactory.Instance;
        });

        mvcBuilder.AddDapr(_ => { });

        builder.Services.AddOptions();

        if (builder.Environment.IsDevelopment())
        {
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
        }

        builder.Services.AddResponseCaching();
        var identity = builder.Configuration.GetSection("Identity");
        builder.Services.Configure<IdentityOptions>(identity);
        builder.Services.Configure<DbOptions>(builder.Configuration.GetSection("DbContext"));
        builder.Services.Configure<IdentityExtensionOptions>(builder.Configuration.GetSection("Identity"));
        builder.Services.Configure<WildGooseOptions>(builder.Configuration.GetSection("WildGoose"));
        builder.Services.Configure<DaprOptions>(builder.Configuration.GetSection("Dapr"));

        builder.Services.AddHttpContextAccessor();
        var dbOptions = builder.AddEfCore();
        builder.RegisterServices();
        builder.Services.ConfigAuthentication(builder.Configuration);
        builder.AddCache(dbOptions);
        builder.Services.AddHealthChecks();
        var identityBuilder = builder.Services.AddIdentityCore<User>(o =>
            {
                o.Stores.MaxLengthForKeys = 128;
                o.SignIn.RequireConfirmedAccount = true;
            })
            .AddRoles<Role>()
            .AddErrorDescriber<ChineseIdentityErrorDescriber>()
            .AddDefaultTokenProviders()
            .AddUserConfirmation<DefaultUserConfirmation<User>>()
            .AddUserValidator<ExtendedUserValidator<User>>()
            .AddEntityFrameworkStores<WildGooseDbContext>();
        Defaults.DisablePasswordLogin = "true".Equals(builder.Configuration["DISABLE_PASSWORD_LOGIN"]);
        if (Defaults.DisablePasswordLogin)
        {
            identityBuilder.AddPasswordValidator<NoopPasswordValidator<User>>();
            identityBuilder.AddUserStore<NoPasswordStore>();
        }

        if (bool.TryParse(builder.Configuration["ENABLE_SM3_PASSWORD_HASHER"],
                out var enable) &&
            enable)
        {
            builder.Services.AddSm3PasswordHasher<User>();
        }

        var serviceCollection = (ServiceCollection)builder.Services;

        var items = serviceCollection.Where(x => x.ServiceType == typeof(IUserValidator<User>) &&
                                                 x.ImplementationType != typeof(ExtendedUserValidator<User>)).ToList();
        foreach (var descriptor in items)
        {
            serviceCollection.Remove(descriptor);
        }

        // 全开放，应该在网关上统一处理
        var corsPolicyName = "___AllowSpecificOrigin";
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: corsPolicyName,
                policy =>
                {
                    var origins = builder.Configuration.GetSection("AllowedCorsOrigins").Get<string[]>();
                    origins = origins == null || origins.Length == 0 ? ["http://localhost:5173"] : origins;
                    policy.WithOrigins(origins)
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetPreflightMaxAge(TimeSpan.FromDays(7));
                });
        });

        var app = builder.Build();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
        LogHelper.Logger = new SerilogIdentityLogger(logger);

        var rootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        if (!Directory.Exists(rootFolder))
        {
            Directory.CreateDirectory(rootFolder);
        }

        if (dbOptions.AutoMigrationEnabled)
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
            var migrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
            if (migrations.Any())
            {
                logger.LogInformation("Applying migrations: {Migrations}", string.Join(", ", migrations));
                await dbContext.Database.MigrateAsync();
            }
            else
            {
                logger.LogInformation("No Applying migrations");
            }
        }

        // 获取才会初始化表
        var hybridCache = app.Services.GetRequiredService<HybridCache>();
        await hybridCache.SetAsync("wildgoose:init", "1", new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromDays(1)
        });
        await SeedData.Init(app.Services);

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost
        });
        app.UseMiddleware<DecryptRequestMiddleware>();
        app.UseRouting();
        var healthCheckPath = Environment.GetEnvironmentVariable("HEALTH_CHECK_PATH") ?? "/healthz";
        app.UseHealthChecks(healthCheckPath);
        app.UseCors(corsPolicyName);
        app.UseResponseCaching();
        app.UseAuthorization();
        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapControllers().RequireCors(corsPolicyName);
        await app.RunAsync();

        Console.WriteLine("Bye");
    }
}