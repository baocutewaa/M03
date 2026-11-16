using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace M03.SecurityTests;

public sealed class EnvironmentBaselineTests
{
    [Fact]
    public async Task WebApp_StartsAndServesHomePage_WhenConnectionStringConfigured()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:SmsApp"] = "Server=.;Database=SMS;Integrated Security=True;TrustServerCertificate=True;"
                    });
                });
            });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void WebApp_FailsFast_WhenRequiredConnectionStringMissing()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:SmsApp"] = ""
                    });
                });
            });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("ConnectionStrings:SmsApp", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.Production.json")]
    public void ConfigFiles_DoNotContainPlaintextSecrets(string fileName)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var filePath = Path.Combine(projectRoot, fileName);

        Assert.True(File.Exists(filePath), $"Expected config file not found: {filePath}");

        var content = File.ReadAllText(filePath);
        Assert.False(
            Regex.IsMatch(content, @"(Password=|Pwd=|User ID=sa|SigningKey\s*"":\s*""[^""]+)", RegexOptions.IgnoreCase),
            $"Potential secret found in {fileName}");
    }
}
