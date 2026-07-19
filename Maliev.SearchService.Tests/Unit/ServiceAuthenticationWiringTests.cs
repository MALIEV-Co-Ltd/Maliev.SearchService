using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Tests SearchService's outbound workload authentication boundary.
/// </summary>
public sealed class ServiceAuthenticationWiringTests
{
    private const string ExpectedToken = "centrally-issued-search-token";

    /// <summary>
    /// SearchService startup should opt into the AuthService exchange and central IAM client only.
    /// </summary>
    [Fact]
    public void Program_RegistersSearchExchangeWithoutLegacySigner()
    {
        var source = ReadRepositoryFile("Maliev.SearchService.Api", "Program.cs");

        Assert.Contains("builder.AddAuthServiceTokenExchange(\"SearchService\");", source, StringComparison.Ordinal);
        Assert.Contains("builder.AddAuthServiceIAMClient();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddIAMServiceClient", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The registered process identity should be exact and legacy local-signing services should not resolve.
    /// </summary>
    [Fact]
    public void AuthServiceIamClient_RegistersExactIdentityWithoutLegacySigningServices()
    {
        var builder = CreateConfiguredBuilder();

        builder.AddAuthServiceTokenExchange("SearchService");
        builder.AddAuthServiceIAMClient();

        using var provider = builder.Services.BuildServiceProvider();
        var identity = provider.GetRequiredService<ServiceProcessIdentity>();

        Assert.Equal("SearchService", identity.ServiceName);
        Assert.Single(provider.GetServices<IIamServiceClient>());
        Assert.Null(provider.GetService<IServiceAccountTokenProvider>());
        Assert.Null(provider.GetService<ServiceAccountAuthenticationHandler>());
    }

    /// <summary>
    /// IAM permission requests should carry the bearer token supplied by the AuthService exchange.
    /// </summary>
    [Fact]
    public async Task IamPermissionCheck_UsesAuthServiceExchangedBearerToken()
    {
        var builder = CreateConfiguredBuilder();
        var capture = new AuthorizationCaptureHandler();
        builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
            new CapturingPrimaryHandlerFilter(capture));

        builder.AddAuthServiceTokenExchange("SearchService");
        builder.Services.AddSingleton<IAuthServiceTokenProvider>(new StubTokenProvider());
        builder.AddAuthServiceIAMClient();

        await using var provider = builder.Services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var iamClient = scope.ServiceProvider.GetRequiredService<IIamServiceClient>();

        var allowed = await iamClient.CheckPermissionAsync(
            $"search-test-{Guid.NewGuid():N}",
            "search.documents.read",
            cancellationToken: CancellationToken.None);

        Assert.True(allowed);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", ExpectedToken), capture.Authorization);
        Assert.Equal(new Uri("https://iam.test/iam/v1/auth/check-permission"), capture.RequestUri);
    }

    /// <summary>
    /// Missing or malformed workload credentials should fail options validation before exchange use.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("service-search-service", "short")]
    public void AuthServiceExchange_InvalidCredentials_FailsClosed(string? clientId, string? clientSecret)
    {
        var builder = CreateConfiguredBuilder(clientId, clientSecret);
        builder.AddAuthServiceTokenExchange("SearchService");

        using var provider = builder.Services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AuthServiceTokenExchangeOptions>>().Value);
    }

    /// <summary>
    /// SearchService CI should consume the published alpha ServiceDefaults line that includes central exchange support.
    /// </summary>
    [Fact]
    public void ServiceDefaultsDependency_PinsPublishedCentralExchangeVersion()
    {
        var source = ReadRepositoryFile("Directory.Build.props");

        Assert.Contains("<ServiceDefaultsVersion Condition=\"'$(ServiceDefaultsVersion)' == ''\">1.0.89-alpha</ServiceDefaultsVersion>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<ServiceDefaultsVersion Condition=\"'$(ServiceDefaultsVersion)' == ''\">1.0.*", source, StringComparison.Ordinal);

        foreach (var project in new[]
                 {
                     "Maliev.SearchService.Api/Maliev.SearchService.Api.csproj",
                     "Maliev.SearchService.Infrastructure/Maliev.SearchService.Infrastructure.csproj",
                     "Maliev.SearchService.Tests/Maliev.SearchService.Tests.csproj"
                 })
        {
            var projectSource = ReadRepositoryFile(project.Split('/'));
            Assert.Contains(
                "<PackageReference Include=\"Maliev.Aspire.ServiceDefaults\" Version=\"$(ServiceDefaultsVersion)\" />",
                projectSource,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Empty configured origins must not shadow ServiceDefaults' safe environment-aware discovery defaults.
    /// </summary>
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void ApplicationConfiguration_DoesNotDeclareEmptyServiceOrigins(string fileName)
    {
        var source = ReadRepositoryFile("Maliev.SearchService.Api", fileName);

        Assert.DoesNotContain("\"BaseUrl\": \"\"", source, StringComparison.Ordinal);
    }

    private static HostApplicationBuilder CreateConfiguredBuilder(
        string? clientId = "service-search-service",
        string? clientSecret = "search-test-secret-with-at-least-32-bytes")
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing"
        });

        using var rsa = RSA.Create(2048);
        builder.Configuration["ServiceAuthentication:ClientId"] = clientId;
        builder.Configuration["ServiceAuthentication:ClientSecret"] = clientSecret;
        builder.Configuration["Services:AuthService:BaseUrl"] = "https://auth.test";
        builder.Configuration["Services:IAMService:BaseUrl"] = "https://iam.test";
        builder.Configuration["Jwt:PublicKey"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()));
        builder.Configuration["Jwt:Issuer"] = "https://api.maliev.com";
        builder.Configuration["Jwt:Audience"] = "https://api.maliev.com";

        return builder;
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            Path.Combine(segments)));

        Assert.True(File.Exists(path), $"Could not find source file: {path}");
        return File.ReadAllText(path);
    }

    private sealed class StubTokenProvider : IAuthServiceTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ExpectedToken);
    }

    private sealed class AuthorizationCaptureHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"allowed\":true}", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CapturingPrimaryHandlerFilter(HttpMessageHandler primaryHandler)
        : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            for (var index = builder.AdditionalHandlers.Count - 1; index >= 0; index--)
            {
                if (builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ServiceDiscovery",
                        StringComparison.Ordinal) == true ||
                    builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ResolvingHttpDelegatingHandler",
                        StringComparison.Ordinal) == true)
                {
                    builder.AdditionalHandlers.RemoveAt(index);
                }
            }

            builder.PrimaryHandler = primaryHandler;
        };
    }
}
