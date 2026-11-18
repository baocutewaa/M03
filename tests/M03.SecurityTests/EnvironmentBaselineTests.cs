using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace M03.SecurityTests;

[Collection("WebApp")]
public sealed class EnvironmentBaselineTests
{
    [Fact]
    public async Task WebApp_StartsAndServesLoginAndHealth_WhenConnectionStringConfigured()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var loginResponse = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var cssResponse = await client.GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, cssResponse.StatusCode);

        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
        Assert.Contains("/Account/Login", homeResponse.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
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
