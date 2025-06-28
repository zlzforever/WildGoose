using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WildGoose.Application;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Filters;
using WildGoose.Infrastructure;

namespace WildGoose;

public class Program
{
    public static async Task Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = WebApplication.CreateBuilder(args);

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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();
        builder.Services.ConfigureIdentityServer(builder.Configuration);
        builder.Services.AddResponseCaching();
        var identity = builder.Configuration.GetSection("Identity");
        builder.Services.Configure<IdentityOptions>(identity);
        builder.Services.Configure<DbOptions>(builder.Configuration.GetSection("DbContext"));
        builder.Services.Configure<IdentityExtensionOptions>(builder.Configuration.GetSection("Identity"));
        builder.Services.Configure<WildGooseOptions>(builder.Configuration.GetSection("WildGoose"));
        builder.Services.Configure<DaprOptions>(builder.Configuration.GetSection("Dapr"));

        AddEfCore(builder);

        builder.RegisterServices();

// builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication(o =>
        {
            o.DefaultScheme = IdentityConstants.ApplicationScheme;
            o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();
        builder.Services.AddMemoryCache();
        builder.Services.AddHealthChecks();
        builder.Services.AddIdentityCore<User>(o =>
            {
                o.Stores.MaxLengthForKeys = 128;
                o.SignIn.RequireConfirmedAccount = true;
            })
            .AddRoles<Role>()
            .AddErrorDescriber<IdentityErrorDescriber>()
            .AddDefaultTokenProviders()
            .AddUserConfirmation<DefaultUserConfirmation<User>>()
            .AddUserValidator<NewUserValidator<User>>()
            .AddEntityFrameworkStores<WildGooseDbContext>();
        var serviceCollection = (ServiceCollection)builder.Services;

        var items = serviceCollection.Where(x => x.ServiceType == typeof(IUserValidator<User>) &&
                                                 x.ImplementationType != typeof(NewUserValidator<User>)).ToList();
        foreach (var descriptor in items)
        {
            serviceCollection.Remove(descriptor);
        }

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
        var rootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        if (!Directory.Exists(rootFolder))
        {
            Directory.CreateDirectory(rootFolder);
        }

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

        SeedData.Init(app.Services).GetAwaiter().GetResult();

        app.UseRouting();
        var healthCheckPath = Environment.GetEnvironmentVariable("HEALTH_CHECK_PATH") ?? "/healthz";
        app.UseHealthChecks(healthCheckPath);
        app.UseCors(corsPolicyName);
        app.UseResponseCaching();
        app.UseAuthorization();
        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapControllers()
            .RequireAuthorization("JWT")
            .RequireCors(corsPolicyName);
        await app.RunAsync();

        Console.WriteLine("Bye");
    }

    private static void AddEfCore(WebApplicationBuilder builder)
    {
        // Add services to the container.
        var connectionString = builder.Configuration["DbContext:ConnectionString"] ??
                               throw new InvalidOperationException(
                                   "Connection string 'DbContext:ConnectionString' not found.");
        var tablePrefix = builder.Configuration["DbContext:TablePrefix"] ?? string.Empty;
        var databaseType = builder.Configuration["DbContext:DatabaseType"] ?? "PostgreSql";
        if ("mysql".Equals(databaseType, StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddDbContextPool<WildGooseDbContext>(options =>
            {
#if DEBUG
                options.EnableSensitiveDataLogging();
#endif
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    b => { b.MigrationsHistoryTable($"{tablePrefix}migrations_history"); });
            });
        }
        else
        {
            builder.Services.AddDbContextPool<WildGooseDbContext>(options =>
            {
#if DEBUG
                options.EnableSensitiveDataLogging();
#endif
                options.UseNpgsql(connectionString,
                    b => { b.MigrationsHistoryTable($"{tablePrefix}migrations_history"); });
            });
        }
    }
}