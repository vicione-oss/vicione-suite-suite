using Core.OS.DbContext;
using Core.OS.UserManagement.Consumers;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using ExternalIdProviderEntity = Core.Shared.UserManagement.Contracts.ExternalIdProvider;

namespace Core.OS.Tests.UserManagement.Consumers;

public sealed class SetExternalIdProviderConsumerTests : IAsyncDisposable
{
    private const string OtherLoginProvider = "SomeOtherProvider";

    private readonly SqliteConnection _userConnection = TestExtensions.CreateSqliteMemoryConnection();
    private readonly ApplicationDbContextSqlite _applicationDbContext
        = TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>();

    private readonly UserDbContextSqlite _userDbContext;

    public SetExternalIdProviderConsumerTests()
        => _userDbContext = TestExtensions.CreateUserDbContextSqlite(_userConnection);

    public async ValueTask DisposeAsync()
    {
        await _applicationDbContext.DisposeAsync();
        await _userDbContext.DisposeAsync();
        await _userConnection.DisposeAsync();
    }

    private MassTransitTester CreateTester(ExternalIdProviderOptions? fileBasedProvider = null)
        => new(cfg =>
        {
            cfg.AddConsumer<SetExternalIdProviderConsumer>();
            cfg.AddConsumer<SetExternalIdProviderFaultConsumer>();
            // As instances: the container would dispose contexts it creates with the first message scope.
            cfg.AddSingleton<ApplicationDbContext>(_applicationDbContext);
            cfg.AddSingleton<UserDbContext>(_userDbContext);
            cfg.AddSingleton(CreateConfiguration(fileBasedProvider));
        });

    private static IConfiguration CreateConfiguration(ExternalIdProviderOptions? fileBasedProvider)
    {
        var values = new Dictionary<string, string?>();

        if (fileBasedProvider is not null)
        {
            for (var i = 0; i < fileBasedProvider.Providers.Count; i++)
            {
                var provider = fileBasedProvider.Providers[i];
                var prefix = $"{ExternalIdProviderOptions.ConfigSection}:Providers:{i}";
                values[$"{prefix}:Name"] = provider.Name;
                values[$"{prefix}:Authority"] = provider.Authority;
                values[$"{prefix}:ClientId"] = provider.ClientId;
                values[$"{prefix}:ClientSecret"] = provider.ClientSecret;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ExternalIdProviderOptions FileBasedProvider(string authority, string clientId)
        => new()
        {
            Providers =
            [
                new Core.Shared.UserManagement.Configuration.ExternalIdProvider
                {
                    Name = "FileProvider",
                    Authority = authority,
                    ClientId = clientId,
                    ClientSecret = null
                }
            ]
        };

    private async Task SeedProvider(string authority, string clientId, string? clientSecret)
    {
        _applicationDbContext.ExternalIdProviders.Add(new ExternalIdProviderEntity
        {
            Name = ProviderConstants.DefaultProviderName,
            Authority = authority,
            ClientId = clientId,
            ClientSecret = clientSecret
        });

        await _applicationDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedLogin(string userId, string loginProvider)
    {
        _userDbContext.Users.Add(new SuiteUser { Id = userId, UserName = userId });
        _userDbContext.UserLogins.Add(new IdentityUserLogin<string>
        {
            UserId = userId,
            LoginProvider = loginProvider,
            ProviderKey = $"{userId}-key",
            ProviderDisplayName = loginProvider
        });

        await _userDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ExternalIdProviderEntity?> ReadStoredProvider()
        => await _applicationDbContext.ExternalIdProviders.AsNoTracking()
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

    private async Task<List<string>> ReadLoginProviders()
        => await _userDbContext.UserLogins.AsNoTracking()
            .Select(l => l.LoginProvider)
            .ToListAsync(TestContext.Current.CancellationToken);

    private async Task<List<string>> ReadLinkedUserIds(string loginProvider)
        => await _userDbContext.UserLogins.AsNoTracking()
            .Where(l => l.LoginProvider == loginProvider)
            .Select(l => l.UserId)
            .ToListAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_create_the_row_under_the_default_provider_name_when_none_is_stored()
    {
        // Arrange
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Set("secret")
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        var stored = await ReadStoredProvider();
        stored.Should().NotBeNull();
        stored!.Name.Should().Be(ProviderConstants.DefaultProviderName);
        stored.Authority.Should().Be("https://idp.example.com");
        stored.ClientId.Should().Be("client-id");
        stored.ClientSecret.Should().Be("secret");

        (await tester.Harness.Published.Any<ExternalIdProviderChanged>(TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_keep_the_stored_secret_when_the_command_carries_no_new_one()
    {
        // Arrange
        await SeedProvider("https://idp.example.com", "client-id", "stored-secret");
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadStoredProvider())!.ClientSecret.Should().Be("stored-secret");
    }

    [Fact]
    public async Task Should_clear_the_stored_secret_when_the_command_asks_for_it()
    {
        // Arrange
        await SeedProvider("https://idp.example.com", "client-id", "stored-secret");
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Clear
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadStoredProvider())!.ClientSecret.Should().BeNull();
    }

    [Fact]
    public async Task Should_leave_external_logins_alone_when_only_the_secret_is_rotated()
    {
        // Arrange
        await SeedProvider("https://idp.example.com", "client-id", "old-secret");
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Set("new-secret")
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        var stored = await ReadStoredProvider();
        stored!.Authority.Should().Be("https://idp.example.com");
        stored.ClientId.Should().Be("client-id");
        stored.ClientSecret.Should().Be("new-secret");

        (await ReadLinkedUserIds(ProviderConstants.DefaultProviderName)).Should().ContainSingle()
            .Which.Should().Be("user-1");
    }

    [Theory]
    [InlineData("https://other.example.com", "client-id")]
    [InlineData("https://idp.example.com", "other-client-id")]
    public async Task Should_unlink_every_external_login_when_the_identity_domain_changes(string authority, string clientId)
    {
        // Arrange
        await SeedProvider("https://idp.example.com", "client-id", "secret");
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await SeedLogin("user-2", ProviderConstants.DefaultProviderName);
        await SeedLogin("user-3", OtherLoginProvider);
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = authority,
            ClientId = clientId,
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadLoginProviders()).Should().ContainSingle()
            .Which.Should().Be(OtherLoginProvider);
    }

    [Theory]
    [InlineData("https://idp.example.com/realms/suite/")]
    [InlineData("https://IDP.Example.com/realms/suite")]
    [InlineData("https://idp.example.com:443/realms/suite")]
    public async Task Should_keep_the_logins_when_only_the_spelling_of_the_authority_changes(string authority)
    {
        // Arrange
        await SeedProvider("https://idp.example.com/realms/suite", "client-id", "secret");
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = authority,
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadLoginProviders()).Should().ContainSingle()
            .Which.Should().Be(ProviderConstants.DefaultProviderName);
    }

    [Fact]
    public async Task Should_unlink_leftover_logins_when_a_provider_is_configured_for_the_first_time()
    {
        // Arrange
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadLoginProviders()).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_keep_the_logins_when_an_unchanged_file_based_provider_is_adopted_into_the_panel()
    {
        // Arrange
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester(FileBasedProvider("https://idp.example.com", "client-id"));
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Set("secret")
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadStoredProvider())!.ClientSecret.Should().Be("secret");
        (await ReadLoginProviders()).Should().ContainSingle()
            .Which.Should().Be(ProviderConstants.DefaultProviderName);
    }

    [Fact]
    public async Task Should_keep_the_logins_when_a_file_based_provider_is_adopted_with_a_differently_spelled_authority()
    {
        // Arrange
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester(FileBasedProvider("https://idp.example.com/", "client-id"));
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadLoginProviders()).Should().ContainSingle()
            .Which.Should().Be(ProviderConstants.DefaultProviderName);
    }

    [Fact]
    public async Task Should_unlink_the_logins_when_a_different_provider_replaces_the_file_based_one()
    {
        // Arrange
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester(FileBasedProvider("https://file.example.com", "file-client-id"));
        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadLoginProviders()).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_keep_the_logins_when_removing_a_row_that_the_file_based_provider_matches()
    {
        // Arrange
        await SeedProvider("https://idp.example.com", "client-id", "secret");
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester(FileBasedProvider("https://idp.example.com", "client-id"));
        var command = new SetExternalIdProvider
        {
            Authority = string.Empty,
            ClientId = string.Empty,
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadStoredProvider()).Should().BeNull();
        (await ReadLoginProviders()).Should().ContainSingle()
            .Which.Should().Be(ProviderConstants.DefaultProviderName);
    }

    [Fact]
    public async Task Should_remove_the_provider_and_its_logins_when_both_fields_are_empty()
    {
        // Arrange
        await SeedProvider("https://idp.example.com", "client-id", "secret");
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await SeedLogin("user-2", OtherLoginProvider);
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = string.Empty,
            ClientId = "   ",
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await ReadStoredProvider()).Should().BeNull();
        (await ReadLoginProviders()).Should().ContainSingle()
            .Which.Should().Be(OtherLoginProvider);

        (await tester.Harness.Published.Any<ExternalIdProviderChanged>(TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_report_success_when_removing_an_already_removed_provider()
    {
        // Arrange
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = string.Empty,
            ClientId = string.Empty,
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ExternalIdProviderChanged>(TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await tester.Harness.Published.Any<SetExternalIdProviderError>(TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Should_fault_on_an_unexpected_failure_and_report_it_from_the_fault_consumer()
    {
        // Arrange
        // A disposed context makes the store step throw something the consumer does not expect.
        await using var tester = CreateTester();
        await _applicationDbContext.DisposeAsync();

        var command = new SetExternalIdProvider
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        var consume = async () =>
            await tester.TestCommandFault<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        await consume.Should().ThrowAsync<ObjectDisposedException>();

        (await tester.Harness.Published.Any<Fault<SetExternalIdProvider>>(TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await tester.Harness.Published.Any<ExternalIdProviderChanged>(TestContext.Current.CancellationToken))
            .Should().BeFalse();

        var errors = await tester.Harness.Published
            .SelectAsync<SetExternalIdProviderError>(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        errors.Should().ContainSingle()
            .Which.Context.Message.CorrelationId.Should().Be(command.CorrelationId);
    }

    [Theory]
    [InlineData("", "client-id")]
    [InlineData("https://idp.example.com", "")]
    [InlineData("http://idp.example.com", "client-id")]
    [InlineData("idp.example.com", "client-id")]
    public async Task Should_reject_the_command_without_touching_the_store(string authority, string clientId)
    {
        // Arrange
        await SeedProvider("https://stored.example.com", "stored-client-id", "secret");
        await SeedLogin("user-1", ProviderConstants.DefaultProviderName);
        await using var tester = CreateTester();
        var command = new SetExternalIdProvider
        {
            Authority = authority,
            ClientId = clientId,
            ClientSecret = ClientSecretUpdate.Keep
        };

        // Act
        await tester.TestCommand<SetExternalIdProvider, SetExternalIdProviderConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SetExternalIdProviderError>(TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await tester.Harness.Published.Any<ExternalIdProviderChanged>(TestContext.Current.CancellationToken))
            .Should().BeFalse();

        var stored = await ReadStoredProvider();
        stored!.Authority.Should().Be("https://stored.example.com");
        stored.ClientId.Should().Be("stored-client-id");
        (await ReadLoginProviders()).Should().ContainSingle();
    }
}
