using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using WildGoose;
using WildGoose.Application;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Filters;
using WildGoose.Infrastructure;

DefaultTypeMap.MatchNamesWithUnderscores = true;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
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
            "logs/wildgoose.log".ToLowerInvariant());
    }

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
        .Enrich.FromLogContext()
#if DEBUG
        .WriteTo.Console()
#endif
        .WriteTo.Async(x => x.File(logFile, rollingInterval: RollingInterval.Day))
        .CreateLogger();
}

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
builder.Services.AddSwaggerGen();
builder.Services.ConfigureIdentityServer(builder.Configuration);
builder.Services.AddResponseCaching();
builder.Services.Configure<DbOptions>(builder.Configuration.GetSection("DbContext"));
builder.Services.Configure<IdentityExtensionOptions>(builder.Configuration.GetSection("Identity"));
builder.Services.Configure<DaprOptions>(builder.Configuration.GetSection("Dapr"));

// Add services to the container.
var connectionString = builder.Configuration["DbContext:ConnectionString"] ??
                       throw new InvalidOperationException("Connection string 'DbContext:ConnectionString' not found.");
var tablePrefix = builder.Configuration["DbContext:TablePrefix"] ?? string.Empty;
builder.Services.AddDbContext<WildGooseDbContext>(options =>
{
    options.UseNpgsql(connectionString, b => { b.MigrationsHistoryTable($"{tablePrefix}migrations_history"); });
});
builder.RegisterServices();

// builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = IdentityConstants.ApplicationScheme;
    o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();
builder.Services.AddMemoryCache();
builder.Services.AddIdentityCore<User>(o =>
    {
        o.Stores.MaxLengthForKeys = 128;
        o.SignIn.RequireConfirmedAccount = true;
    })
    .AddRoles<Role>()
    .AddErrorDescriber<IdentityErrorDescriber>()
    .AddDefaultTokenProviders()
    .AddUserConfirmation<DefaultUserConfirmation<User>>()
    .AddEntityFrameworkStores<WildGooseDbContext>();

// // TODO:
// builder.Services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<User>>();
// builder.Services.TryAddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<User>>();

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


var rootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
if (!Directory.Exists(rootFolder))
{
    Directory.CreateDirectory(rootFolder);
}

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
var migrations = await dbContext.Database.GetPendingMigrationsAsync();
if (migrations.Any())
{
    await dbContext.Database.MigrateAsync();
}

SeedData.Init(app.Services).GetAwaiter().GetResult();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors(corsPolicyName);
app.UseResponseCaching();
app.UseAuthorization();
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers().RequireAuthorization("Jwt").RequireCors(corsPolicyName);
app.Run();

Console.WriteLine("Bye");