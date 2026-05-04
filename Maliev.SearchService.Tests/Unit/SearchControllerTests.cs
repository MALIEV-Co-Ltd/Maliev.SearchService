using System.Security.Claims;
using Maliev.MessagingContracts.Contracts.Search;
using Maliev.SearchService.Api.Controllers;
using Maliev.SearchService.Api.Models;
using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Application.Services;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Tests for SearchService API controller behavior.
/// </summary>
public class SearchControllerTests
{
    /// <summary>
    /// Development configuration should not override Aspire IAM service discovery with a localhost URL.
    /// </summary>
    [Fact]
    public void DevelopmentConfiguration_DoesNotOverrideIamServiceDiscovery()
    {
        var configPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Maliev.SearchService.Api",
            "appsettings.Development.json"));
        var source = File.ReadAllText(configPath);

        Assert.DoesNotContain("localhost:5006", source, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Short queries should return an empty response without touching the index service.
    /// </summary>
    [Fact]
    public async Task Search_QueryBelowMinimum_ReturnsEmptyResponse()
    {
        var searchIndex = new Mock<ISearchIndexService>();
        var controller = CreateController(searchIndex);

        var result = await controller.Search("a");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SearchResponseDto>(ok.Value);
        Assert.Equal("a", response.Query);
        Assert.Empty(response.Results);
        searchIndex.Verify(
            service => service.SearchAsync(
                It.IsAny<SearchQueryDto>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Valid search requests should pass caller permissions and platform owner state to the index service.
    /// </summary>
    [Fact]
    public async Task Search_ValidQuery_ForwardsCallerPermissionClaims()
    {
        IReadOnlyCollection<string>? capturedClaims = null;
        bool? capturedIsPlatformOwner = null;
        var searchIndex = new Mock<ISearchIndexService>();
        searchIndex.Setup(service => service.SearchAsync(
                It.IsAny<SearchQueryDto>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<SearchQueryDto, IReadOnlyCollection<string>, bool, CancellationToken>((_, claims, isPlatformOwner, _) =>
            {
                capturedClaims = claims;
                capturedIsPlatformOwner = isPlatformOwner;
            })
            .ReturnsAsync(new SearchResponseDto("kanya", 0, []));
        var controller = CreateController(
            searchIndex,
            new Claim("permissions", "customer.customers.read"),
            new Claim(ClaimTypes.Role, "platform.owner"));

        var result = await controller.Search("kanya", limit: 5);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(capturedClaims);
        Assert.Contains("customer.customers.read", capturedClaims);
        Assert.True(capturedIsPlatformOwner);
    }

    /// <summary>
    /// Long search queries should return the standard error response shape.
    /// </summary>
    [Fact]
    public async Task Search_QueryAboveMaximum_ReturnsTypedErrorResponse()
    {
        var controller = CreateController(new Mock<ISearchIndexService>());

        var result = await controller.Search(new string('a', 121));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("QUERY_TOO_LONG", response.Code);
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
    }

    /// <summary>
    /// Reindex endpoint should publish a source-scoped command when requested.
    /// </summary>
    [Fact]
    public async Task RequestReindex_WithSourceService_PublishesReindexCommand()
    {
        SearchReindexRequestedCommand? published = null;
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint.Setup(endpoint => endpoint.Publish(
                It.IsAny<SearchReindexRequestedCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<SearchReindexRequestedCommand, CancellationToken>((message, _) => published = message)
            .Returns(Task.CompletedTask);
        var controller = CreateController(new Mock<ISearchIndexService>(), publishEndpoint);

        var result = await controller.RequestReindex("CustomerService");

        Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(published);
        Assert.Equal("CustomerService", published.Payload.SourceService);
        Assert.Equal("CustomerService", Assert.Single(published.ConsumedBy));
    }

    /// <summary>
    /// Global reindex requests should publish to all eligible producers.
    /// </summary>
    [Fact]
    public async Task RequestReindex_WithoutSourceService_PublishesGlobalCommand()
    {
        SearchReindexRequestedCommand? published = null;
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint.Setup(endpoint => endpoint.Publish(
                It.IsAny<SearchReindexRequestedCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<SearchReindexRequestedCommand, CancellationToken>((message, _) => published = message)
            .Returns(Task.CompletedTask);
        var controller = CreateController(new Mock<ISearchIndexService>(), publishEndpoint);

        var result = await controller.RequestReindex();

        Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(published);
        Assert.Null(published.Payload.SourceService);
        Assert.Equal("*", Assert.Single(published.ConsumedBy));
    }

    /// <summary>
    /// Reindex requests should reject unknown source services instead of publishing arbitrary consumer names.
    /// </summary>
    [Fact]
    public async Task RequestReindex_WithUnknownSourceService_ReturnsTypedErrorResponse()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var controller = CreateController(new Mock<ISearchIndexService>(), publishEndpoint);

        var result = await controller.RequestReindex("UnknownService");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_SOURCE_SERVICE", response.Code);
        publishEndpoint.Verify(endpoint => endpoint.Publish(
            It.IsAny<SearchReindexRequestedCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SearchController CreateController(
        Mock<ISearchIndexService> searchIndex,
        params Claim[] claims)
    {
        return CreateController(searchIndex, new Mock<IPublishEndpoint>(), claims);
    }

    private static SearchController CreateController(
        Mock<ISearchIndexService> searchIndex,
        Mock<IPublishEndpoint> publishEndpoint,
        params Claim[] claims)
    {
        var controller = new SearchController(searchIndex.Object, publishEndpoint.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
        return controller;
    }
}
