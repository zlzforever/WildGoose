using System.Xml.Linq;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class ProductionPublishBoundaryTests : BaseTests
{
    [Fact]
    public void ApiDockerfile_PublishesOnlyTheWildGooseApplication()
    {
        var dockerfile = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "api.Dockerfile"));

        Assert.Contains("dotnet publish src/WildGoose/WildGoose.csproj", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("src/WildGoose.Tests/WildGoose.Tests.csproj", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("COPY . .", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet publish  -o", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void TestProject_DoesNotCopyTheCommittedPrivateJwkToOutput()
    {
        var projectPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "WildGoose.Tests",
            "WildGoose.Tests.csproj");
        var project = XDocument.Load(projectPath);

        var copiedJwkItems = project
            .Descendants()
            .Where(element =>
                (string?)element.Attribute("Update") is "jwt.jwk" or "**/jwt.jwk" ||
                (string?)element.Attribute("Include") is "jwt.jwk" or "**/jwt.jwk")
            .Where(element => element.Descendants("CopyToOutputDirectory").Any())
            .ToArray();

        Assert.Empty(copiedJwkItems);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "api.Dockerfile")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
