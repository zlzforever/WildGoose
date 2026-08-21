using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using WildGoose;
using Xunit;

namespace WildGoose.Tests.Authentication;

public sealed class ConfigurationSubstitutionTests
{
    [Fact]
    public void AddSubstitution_LoadsDevelopmentConfigurationWithoutDisposedStream()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        var exception = Record.Exception(builder.AddSubstitution);

        Assert.Null(exception);
        Assert.Equal("http://localhost:8099", builder.Configuration["JwtBearer:Authority"]);
    }
}
