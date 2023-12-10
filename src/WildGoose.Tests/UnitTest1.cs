using WildGoose.Domain.Entity;
using Xunit;

namespace WildGoose.Tests;

public class Tests
{
    [Fact]
    public void AssertResource()
    {
        var statement = new Statement
        {
            Action = new List<string> { "*" },
            Resource = new List<string> { "*" },
            Effect = "Allow"
        };
        var value = statement.Assert("asdfasdfasdf", "asdf");
        Assert.Equal("Allow", value);
    }
}