using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Identity.Sm;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Logging;
using WildGoose.Application;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Filters;
using WildGoose.Infrastructure;
using WildGoose.Middlewares;

namespace WildGoose;

public class Program
{
    public static async Task Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = WebApplication.CreateBuilder(args);

        builder.AddSerilog();
        builder.Configuration.AddEnvironmentVariables();
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
        var dbOptions = AddEfCore(builder);

        builder.RegisterServices();
        // 应该不需要 cookie 认证
        // builder.Services.AddAuthentication(o =>
        // {
        //     o.DefaultScheme = IdentityConstants.ApplicationScheme;
        //     o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        // }).AddIdentityCookies();
        builder.Services.ConfigAuthentication(builder.Configuration);
        builder.Services.AddMemoryCache();
        builder.Services.AddHealthChecks();
        builder.Services.AddIdentityCore<User>(o =>
            {
                o.Stores.MaxLengthForKeys = 128;
                o.SignIn.RequireConfirmedAccount = true;
            })
            .AddRoles<Role>()
            .AddErrorDescriber<ChineseIdentityErrorDescriber>()
            .AddDefaultTokenProviders()
            .AddUserConfirmation<DefaultUserConfirmation<User>>()
            .AddUserValidator<NewUserValidator<User>>()
            .AddEntityFrameworkStores<WildGooseDbContext>();
        if (bool.TryParse(builder.Configuration["ENABLE_SM3_PASSWORD_HASHER"],
                out var enable) &&
            enable)
        {
            builder.Services.AddSm3PasswordHasher<User>();
        }

        var serviceCollection = (ServiceCollection)builder.Services;

        var items = serviceCollection.Where(x => x.ServiceType == typeof(IUserValidator<User>) &&
                                                 x.ImplementationType != typeof(NewUserValidator<User>)).ToList();
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
                    origins = origins == null || origins.Length == 0 ? new[] { "http://localhost:5173" } : origins;
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

        await SeedData.Init(app.Services);

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

    private static DbOptions AddEfCore(WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("DbContext");
        var config = section.Get<DbOptions>();
        if (config == null)
        {
            throw new ArgumentException($"Missing configuration for {nameof(DbOptions)}");
        }

        // Add services to the container.
        var connectionString = config.ConnectionString ??
                               throw new InvalidOperationException(
                                   "Connection string 'DbContext:ConnectionString' not found.");
        var tablePrefix = config.TablePrefix;
        var databaseType = config.DatabaseType;
        Console.WriteLine($"Using {tablePrefix} database type: {databaseType}");
        if ("mysql".Equals(databaseType, StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddDbContextPool<WildGooseDbContext>(options =>
            {
                if (config.EnableSensitiveDataLogging)
                {
                    options.EnableSensitiveDataLogging();
                }

                options.ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
                });

                // var dbContextOptions = options.Options;
                // var infra = ((IDbContextOptionsBuilderInfrastructure)options);
#pragma warning disable EF1001
#pragma warning disable CS0618 // Type or member is obsolete

                // var extension = dbContextOptions.FindExtension<MySqlOptionsExtension>() ?? new MySqlOptionsExtension();
                //
                // infra.AddOrUpdateExtension(extension);
                // var coreOptionsExtension =
                //     dbContextOptions.FindExtension<CoreOptionsExtension>() ?? new CoreOptionsExtension();
                // coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(
                //     coreOptionsExtension.WarningsConfiguration.TryWithExplicit(
                //         RelationalEventId.AmbientTransactionWarning, WarningBehavior.Throw));
                // infra.AddOrUpdateExtension(coreOptionsExtension);
                //
                // var mySqlDbContextOptionsBuilder = new MySqlDbContextOptionsBuilder(options)
                //     .TranslateParameterizedCollectionsToConstants();
                //
                // mySqlDbContextOptionsBuilder.MigrationsHistoryTable($"{tablePrefix}migrations_history");

                options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 29)),
                    b => { b.MigrationsHistoryTable($"{tablePrefix}migrations_history"); });

#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore EF1001
            });
        }
        else
        {
            builder.Services.AddDbContextPool<WildGooseDbContext>(options =>
            {
                if (config.EnableSensitiveDataLogging)
                {
                    options.EnableSensitiveDataLogging();
                }

                options.ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
                });
                options.UseNpgsql(connectionString,
                    b => { b.MigrationsHistoryTable($"{tablePrefix}migrations_history"); });
            });
        }

        return config;
    }
}