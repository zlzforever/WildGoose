using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WildGoose.Controllers.V10;

namespace WildGoose.Tests;

public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 添加控制器
            services.AddControllers().AddApplicationPart(typeof(UserController).Assembly);
        }).ConfigureLogging(logging =>
        {
            logging.ClearProviders(); // 清除默认的日志提供器
            logging.AddSerilog(new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger());
        });
    }
}