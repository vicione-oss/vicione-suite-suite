using Core.OS.DbContext;
using Core.OS.UserManagement.Consumers;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using ExternalIdProviderEntity = Core.Shared.UserManagement.Contracts.ExternalIdProvider;

namespace Core.OS.Tests.UserManagement.Consumers;

public sealed class GetExternalIdProviderConsumerTests : IAsyncDisposable
{
    private readonly ApplicationDbContextSqlite _applicationDbContext
        = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();

    public async ValueTask DisposeAsync() => await _applicationDbContext.DisposeAsync();

    private MassTransitTester CreateTester()
        => new(cfg =>
        {
            cfg.AddConsumer<GetExternalIdProviderConsumer>();
            cfg.AddSingleton<ApplicationDbContext>(_applicationDbContext);
        });

    private async Task SeedProvider(string? clientSecret)
    {
        _applicationDbContext.ExternalIdProviders.Add(new ExternalIdProviderEntity
        {
            Name = ProviderConstants.DefaultProviderName,
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = clientSecret
        });

        await _applicationDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_report_an_empty_provider_when_none_is_stored()
    {
        // Arrange
        await using var tester = CreateTester();

        // Act
        var response = await tester.TestRequest<GetExternalIdProviderResponse, GetExternalIdProvider>(new());

        // Assert
        response.Authority.Should().BeEmpty();
        response.ClientId.Should().BeEmpty();
        response.ClientSecretStored.Should().BeFalse();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_report_that_a_secret_is_stored_without_revealing_it()
    {
        // Arrange
        await SeedProvider("top-secret");
        await using var tester = CreateTester();

        // Act
        var response = await tester.TestRequest<GetExternalIdProviderResponse, GetExternalIdProvider>(new());

        // Assert
        response.Authority.Should().Be("https://idp.example.com");
        response.ClientId.Should().Be("client-id");
        response.ClientSecretStored.Should().BeTrue();

        response.ToString().Should().NotContain("top-secret");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Should_report_no_stored_secret_when_the_column_is_blank(string? clientSecret)
    {
        // Arrange
        await SeedProvider(clientSecret);
        await using var tester = CreateTester();

        // Act
        var response = await tester.TestRequest<GetExternalIdProviderResponse, GetExternalIdProvider>(new());

        // Assert
        response.ClientSecretStored.Should().BeFalse();
    }
}
