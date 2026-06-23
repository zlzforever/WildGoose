using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Serilog.Events;
using WildGoose.Application;
using WildGoose.Application.Cache;
using WildGoose.Application.Ef;
using WildGoose.Application.OSS;
using WildGoose.Application.Permission.Internal.V10;
using WildGoose.Application.Services;
using WildGoose.Application.Services.Admin.Organization.V10;
using WildGoose.Application.Services.Admin.Role.V10;
using WildGoose.Application.Services.Admin.User.V10;
using WildGoose.Application.Services.Organization.V10;
using WildGoose.Application.Services.User.V10;
using WildGoose.Domain;
using WildGoose.Domain.Options;
using WildGoose.Serilog;
using ISession = WildGoose.Domain.ISession;

namespace WildGoose;

public static class WebApplicationBuilderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public void AddSubstitution()
        {
            var env = builder.Environment;
            var replaceFiles = new Dictionary<string, int?>
            {
                { "appsettings.json", null },
                { $"appsettings.{env.EnvironmentName}.json", null }
            };
            var sources = builder.Configuration.Sources;
            for (var i = 0; i < builder.Configuration.Sources.Count; i++)
            {
                if (sources[i] is not FileConfigurationSource fcs)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(fcs.Path))
                {
                    continue;
                }

                if (replaceFiles.ContainsKey(fcs.Path))
                {
                    replaceFiles[fcs.Path] = i;
                }
            }

            string SubstituteEnv(string text)
            {
                return Regex.Replace(text, @"\$\{(?<k>.*?)\}", m =>
                {
                    var key = m.Groups["k"].Value.Trim();
                    return Environment.GetEnvironmentVariable(key) ?? m.Value;
                });
            }

            void ReplaceSource(ConfigurationManager configurationManager, int index, string path)
            {
                if (!File.Exists(path))
                {
                    return;
                }

                using var stream =
                    new MemoryStream(System.Text.Encoding.UTF8.GetBytes(SubstituteEnv(File.ReadAllText(path))));
                configurationManager.Sources[index] = new JsonStreamConfigurationSource
                {
                    Stream = stream
                };
            }

            foreach (var kv in replaceFiles)
            {
                if (kv.Value == null)
                {
                    continue;
                }

                ReplaceSource(builder.Configuration, kv.Value.Value, kv.Key);
            }
        }

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
            var factory = new LoggerFactory();
            factory.AddSerilog();
            Defaults.Logger = factory.CreateLogger("WildGoose");
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
            builder.Services.TryAddSingleton<NonceStore>();
            builder.Services.AddHostedService<GenerateTop3LevelOrganizationsToFileService>();
        }

        public DbOptions AddEfCore()
        {
            var section = builder.Configuration.GetSection("DbContext");
            var dbOptions = section.Get<DbOptions>();
            if (dbOptions == null)
            {
                throw new ArgumentException($"Missing configuration for {nameof(DbOptions)}");
            }

            // Add services to the container.
            var connectionString = dbOptions.ConnectionString ??
                                   throw new InvalidOperationException(
                                       "Connection string 'DbContext:ConnectionString' not found.");
            var tablePrefix = dbOptions.TablePrefix;
            var databaseType = dbOptions.DatabaseType;
            if ("mysql".Equals(databaseType, StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddDbContextPool<WildGooseDbContext>(options =>
                {
                    if (dbOptions.EnableSensitiveDataLogging)
                    {
                        options.EnableSensitiveDataLogging();
                    }

                    options.ConfigureWarnings(warnings =>
                    {
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
                    });

                    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 29)),
                        b => { b.MigrationsHistoryTable($"{tablePrefix}migrations_history"); });
                    options.ReplaceService<IMigrationsSqlGenerator, MySqlPrefixedMigrationsSqlGenerator>();
                });
            }
            else
            {
                builder.Services.AddDbContextPool<WildGooseDbContext>(options =>
                {
                    if (dbOptions.EnableSensitiveDataLogging)
                    {
                        options.EnableSensitiveDataLogging();
                    }

                    options.ConfigureWarnings(warnings =>
                    {
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
                    });
                    options.UseNpgsql(connectionString,
                        b => { b.MigrationsHistoryTable($"{tablePrefix}migrations_history"); });
                    options.ReplaceService<IMigrationsSqlGenerator, NpgsqlPrefixedMigrationsSqlGenerator>();
                });
            }

            return dbOptions;
        }

        public void AddCache(DbOptions dbOptions)
        {
            if (dbOptions == null)
            {
                throw new ArgumentException($"Missing configuration for {nameof(DbOptions)}");
            }

            // Add services to the container.
            var connectionString = dbOptions.ConnectionString ??
                                   throw new InvalidOperationException(
                                       "Connection string 'DbContext:ConnectionString' not found.");
            builder.Services.AddMemoryCache();
            var databaseType = dbOptions.DatabaseType;

            if ("mysql".Equals(databaseType, StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddDistributedMySqlCache(options =>
                {
                    if (!connectionString.Contains("Allow User Variables=True"))
                    {
                        throw new ArgumentException(
                            "Allow User Variables=True is required in the connection string for MySQL distributed cache.");
                    }

                    options.ConnectionString = connectionString;
                    options.SchemaName = ""; // MySQL 库名写在连接串，此处留空
                    options.TableName = builder.Configuration.GetValue<string>("MySqlCache:TableName", "cache_entries");
                    var expirationInterval =
                        builder.Configuration.GetValue<string>("MySqlCache:ExpiredItemsDeletionInterval");
                    if (!string.IsNullOrEmpty(expirationInterval) &&
                        TimeSpan.TryParse(expirationInterval, out var interval))
                    {
                        options.ExpiredItemsDeletionInterval = interval;
                    }
                    else
                    {
                        options.ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30); // 定时清理过期缓存
                    }

                    var slidingExpiration =
                        builder.Configuration.GetValue<string>("MySqlCache:DefaultSlidingExpiration");
                    if (!string.IsNullOrEmpty(slidingExpiration) &&
                        TimeSpan.TryParse(slidingExpiration, out var sliding))
                    {
                        options.DefaultSlidingExpiration = sliding;
                    }
                    else
                    {
                        options.DefaultSlidingExpiration = TimeSpan.FromMinutes(10);
                    }

                    MySqlUtil.CreateIfNotExists(connectionString, options.SchemaName, options.TableName);
                });
            }
            else
            {
                builder.Services.AddDistributedPostgresCache(options =>
                {
                    options.ConnectionString = connectionString;
                    options.SchemaName = builder.Configuration.GetValue<string>("PostgresCache:SchemaName", "public");
                    options.TableName =
                        builder.Configuration.GetValue<string>("PostgresCache:TableName", "cache_entries");
                    options.CreateIfNotExists = builder.Configuration.GetValue("PostgresCache:CreateIfNotExists", true);
                    options.UseWAL = builder.Configuration.GetValue("PostgresCache:UseWAL", false);

                    var expirationInterval =
                        builder.Configuration.GetValue<string>("PostgresCache:ExpiredItemsDeletionInterval");
                    if (!string.IsNullOrEmpty(expirationInterval) &&
                        TimeSpan.TryParse(expirationInterval, out var interval))
                    {
                        options.ExpiredItemsDeletionInterval = interval;
                    }
                    else
                    {
                        options.ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30); // 定时清理过期缓存
                    }

                    var slidingExpiration =
                        builder.Configuration.GetValue<string>("PostgresCache:DefaultSlidingExpiration");
                    if (!string.IsNullOrEmpty(slidingExpiration) &&
                        TimeSpan.TryParse(slidingExpiration, out var sliding))
                    {
                        options.DefaultSlidingExpiration = sliding;
                    }
                    else
                    {
                        options.DefaultSlidingExpiration = TimeSpan.FromMinutes(10);
                    }
                });
            }

            builder.Services.AddHybridCache();
        }
    }
}