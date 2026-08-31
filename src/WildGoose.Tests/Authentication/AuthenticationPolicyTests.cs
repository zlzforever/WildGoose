using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WildGoose.Authentication;
using WildGoose.Authentication.GatewayJwtBearer;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class AuthenticationPolicyTests : BaseTests
{
    [Fact]
    public void JwtOnlyConfiguration_SetsJwtBearerAsEveryDefaultScheme()
    {
        using var provider = BuildProvider("JwtBearer");

        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Equal("JwtBearer", options.DefaultAuthenticateScheme);
        Assert.Equal("JwtBearer", options.DefaultChallengeScheme);
        Assert.Equal("JwtBearer", options.DefaultForbidScheme);
    }

    [Fact]
    public async Task MissingAuthenticationSchemes_UsesOnlyJwtBearerByDefault()
    {
        using var provider = BuildProvider(null);

        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var schemes = await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync("SCOPE");

        Assert.Equal("JwtBearer", options.DefaultAuthenticateScheme);
        Assert.Equal("JwtBearer", options.DefaultChallengeScheme);
        Assert.Equal("JwtBearer", options.DefaultForbidScheme);
        Assert.Equal(["JwtBearer"], schemes.Select(scheme => scheme.Name).ToArray());
        Assert.Equal(["JwtBearer"], policy!.AuthenticationSchemes);
    }

    [Fact]
    public async Task Policies_UseOnlyRegisteredJwtBearerSchemeAndKeepRoleContracts()
    {
        using var provider = BuildProvider("JwtBearer");
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var scopePolicy = await policyProvider.GetPolicyAsync("SCOPE");
        var superPolicy = await policyProvider.GetPolicyAsync(Defaults.SuperPolicy);
        var combinedPolicy = await policyProvider.GetPolicyAsync(Defaults.SuperOrUserAdminOrOrgAdminPolicy);

        Assert.NotNull(scopePolicy);
        Assert.Equal(["JwtBearer"], scopePolicy.AuthenticationSchemes);
        Assert.Contains(scopePolicy.Requirements,
            requirement => requirement is ClaimsAuthorizationRequirement claim &&
                           claim.ClaimType == "scope" &&
                           claim.AllowedValues!.SequenceEqual(["wildgoose-api"]));
        Assert.NotNull(superPolicy);
        Assert.Equal(["JwtBearer"], superPolicy.AuthenticationSchemes);
        Assert.Contains(superPolicy.Requirements,
            requirement => requirement is RolesAuthorizationRequirement roles &&
                           roles.AllowedRoles.SequenceEqual([Defaults.AdminRole]));
        Assert.NotNull(combinedPolicy);
        Assert.Equal(["JwtBearer"], combinedPolicy.AuthenticationSchemes);
        Assert.Contains(combinedPolicy.Requirements,
            requirement => requirement is RolesAuthorizationRequirement roles &&
                           roles.AllowedRoles.SequenceEqual([
                               Defaults.AdminRole,
                               Defaults.UserAdminRole,
                               Defaults.OrganizationAdminRole
                           ]));
    }

    [Fact]
    public async Task ExplicitSchemes_AreCopiedToDefaultPolicy()
    {
        using var provider = BuildProvider("JwtBearer,SecurityToken");

        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetDefaultPolicyAsync();

        Assert.Equal(["JwtBearer", "SecurityToken"], options.DefaultPolicy.AuthenticationSchemes);
        Assert.Equal(["JwtBearer", "SecurityToken"], policy.AuthenticationSchemes);
    }

    [Fact]
    public async Task SchemeNames_AreCanonicalizedBeforeBuildingDefaultPolicy()
    {
        using var provider = BuildProvider(" jwtbearer , securitytoken ");

        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetDefaultPolicyAsync();

        Assert.Equal(["JwtBearer", "SecurityToken"], policy.AuthenticationSchemes);
    }

    [Fact]
    public async Task HistoricalBearerAlias_MapsToRegisteredJwtBearerScheme()
    {
        using var provider = BuildProvider("Bearer");

        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("SCOPE");

        Assert.Equal("JwtBearer", options.DefaultAuthenticateScheme);
        Assert.Equal(["JwtBearer"], policy!.AuthenticationSchemes);
    }

    [Fact]
    public void UnknownAuthenticationScheme_FailsAtStartupConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = "JwtBearer,NotRegistered",
            ["JwtBearer:KeyPath"] = Path.Combine(Path.GetTempPath(), "missing-wildgoose-key.jwk")
        }).Build();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.ConfigAuthenticationCore(configuration, new TestHostEnvironment()));

        Assert.Contains("NotRegistered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HistoricalGatewayJwtBearerAlias_UsesGatewayBearerAndLegacyConfigurationSection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = "GatewayJwtBearer",
            ["GatewayJwtBearer:Name"] = "X-Legacy-Userinfo",
            ["GatewayJwtBearer:Issuer"] = "https://issuer.example",
            ["GatewayJwtBearer:Audience"] = "wildgoose-api"
        }).Build();

        services.ConfigAuthenticationCore(configuration, new TestHostEnvironment());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var scheme = await provider.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync("GatewayBearer");
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<GatewayJwtBearerOptions>>();
        var options = optionsMonitor.Get("GatewayBearer");
        var currentOptions = optionsMonitor.CurrentValue;

        Assert.NotNull(scheme);
        Assert.Equal("GatewayBearer", scheme.Name);
        Assert.Equal("X-Legacy-Userinfo", options.Name);
        Assert.Equal("https://issuer.example", options.Issuer);
        Assert.Equal("wildgoose-api", options.Audience);
        Assert.Equal("X-Legacy-Userinfo", currentOptions.Name);
        Assert.Equal("https://issuer.example", currentOptions.Issuer);
        Assert.Equal("wildgoose-api", currentOptions.Audience);
    }

    [Fact]
    public void GatewayBearerConfiguration_PreservesConfiguredAudience()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = "GatewayBearer",
            ["GatewayBearer:Name"] = "X-Userinfo",
            ["GatewayBearer:Issuer"] = "https://issuer.example",
            ["GatewayBearer:Audience"] = "configured-audience"
        }).Build();

        services.ConfigAuthenticationCore(configuration, new TestHostEnvironment());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var options = provider.GetRequiredService<IOptionsMonitor<GatewayJwtBearerOptions>>().Get("GatewayBearer");

        Assert.Equal("X-Userinfo", options.Name);
        Assert.Equal("https://issuer.example", options.Issuer);
        Assert.Equal("configured-audience", options.Audience);
    }

    private static ServiceProvider BuildProvider(string? schemes)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = schemes,
            ["JwtBearer:Authority"] = "https://issuer.example",
            ["JwtBearer:RequireHttpsMetadata"] = "true",
            ["JwtBearer:ValidateAudience"] = "true",
            ["JwtBearer:ValidateIssuer"] = "true",
            ["JwtBearer:ValidateLifetime"] = "true"
        }).Build();

        services.ConfigAuthenticationCore(configuration, new TestHostEnvironment());
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
