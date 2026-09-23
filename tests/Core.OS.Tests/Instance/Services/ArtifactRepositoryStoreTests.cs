using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Messaging;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Services;

public class ArtifactRepositoryStoreTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IOptions<InstanceOptions> _options;
    private readonly ILogger<ArtifactRepositoryStore> _logger = Substitute.For<ILogger<ArtifactRepositoryStore>>();
    private readonly ArtifactRepository _releaseSource = new()
    {
        Id = Guid.NewGuid(),
        Endpoint = "https://system.update.ifm/vicione-suite/release",
        UserName = "wildman",
        Password = "pa$$w0rd",
        Modified = DateTimeOffset.UtcNow,
        ModifiedBy = Environment.UserName,
    };

    private readonly ArtifactRepository _stagingSource = new()
    {
        Id = Guid.NewGuid(),
        Endpoint = "https://system.update.ifm/vicione-suite/staging",
        UserName = "hammerer",
        Password = "d00dle",
        Modified = DateTimeOffset.UtcNow,
        ModifiedBy = Environment.UserName,
    };

    private readonly ServiceProvider _services;

    public ArtifactRepositoryStoreTests()
    {
        _options = Options.Create(new InstanceOptions
        {
            HomeDirectory = "Home",
            BackupDirectory = "Backup",
            CacheDirectory = "Cache",
            Type = Sdk.Instance.InstanceType.Standalone
        });

        _fileSystem = new MockFileSystem();
        var rooted = _fileSystem.GetRootedHomeDirectory(_options.Value);
        _fileSystem.AddDirectory(_fileSystem.Path.Combine(rooted, _options.Value.HomeDirectory));

        _services = new ServiceCollection()
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(_options)
            .AddSingleton(_logger)
            .AddSingleton<IArtifactRepositoryStore, ArtifactRepositoryStore>()
            .BuildServiceProvider();
    }

    public sealed class MigrateConfiguredRepositories : ArtifactRepositoryStoreTests
    {
        [Fact]
        public async Task Should_not_throw_on_missing_section()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            var config = new TestConfig().BuildConfiguration();

            // Act
            await repositoryStore.MigrateConfiguredRepositories(config, _logger, TestContext.Current.CancellationToken);

            // Assert
            var result = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_migrate_backwards_compatible_options_from_memory_collection()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { "ModuleApi:Endpoint", "https://system.update.ifm/" },
                { "ModuleApi:UserName", "wildman" },
                { "ModuleApi:Password", "pa$$w0rd" }
            });

            // Act
            await repositoryStore.MigrateConfiguredRepositories(builder.Build(), _logger, TestContext.Current.CancellationToken);

            // Assert
            var repositories = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            repositories.Should().HaveCount(1);
            repositories[0].Endpoint.Should().Be("https://system.update.ifm/");
            repositories[0].UserName.Should().Be("wildman");
            repositories[0].Password.Should().Be("pa$$w0rd");
        }

        [Fact]
        public async Task Should_migrate_compatible_options_from_configuration()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();

            var migrateEndpoint = "https://system.update.ifm/";
            var currentEndpoint = "https://staging.update.ifm/";

            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                // Compatibility with v0.40.0.
                { "ModuleApi:Endpoint", migrateEndpoint },
                { "ModuleApi:UserName", "wildman" },
                { "ModuleApi:Password", "pa$$w0rd" },

                // Compatibility with v1.1.0.
                { "ArtifactRepository:Sources:0:Endpoint", currentEndpoint },
                { "ArtifactRepository:Sources:0:UserName", "hammerer" },
                { "ArtifactRepository:Sources:0:Password", "d00dle" },
            });

            // Act
            await repositoryStore.MigrateConfiguredRepositories(builder.Build(), _logger, TestContext.Current.CancellationToken);

            // Assert
            var repositories = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            repositories.Should().HaveCount(2);

            var migratedOption = repositories.First(s => s.Endpoint == migrateEndpoint);
            migratedOption.Endpoint.Should().Be(migrateEndpoint);
            migratedOption.UserName.Should().Be("wildman");
            migratedOption.Password.Should().Be("pa$$w0rd");

            var currentOption = repositories.First(s => s.Endpoint == currentEndpoint);
            currentOption.Endpoint.Should().Be(currentEndpoint);
            currentOption.UserName.Should().Be("hammerer");
            currentOption.Password.Should().Be("d00dle");
        }

        [Fact]
        public async Task Should_not_migrate_environment_if_repositories_already_exist()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { "ArtifactRepository:Sources:0:Endpoint", "https://system.update.ifm/hammerer" },
                { "ArtifactRepository:Sources:0:UserName", "hammerer" },
                { "ArtifactRepository:Sources:0:Password", "d00dle" },
            });

            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);

            // Act
            await repositoryStore.MigrateConfiguredRepositories(builder.Build(), _logger, TestContext.Current.CancellationToken);

            // Assert
            var repositories = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            repositories.Should().HaveCount(1);

            var sourceOption = repositories.First(s => s.Endpoint == _releaseSource.Endpoint);
            sourceOption.UserName.Should().Be(_releaseSource.UserName);
            sourceOption.Password.Should().Be(_releaseSource.Password);
        }
    }

    public sealed class GetRepositories : ArtifactRepositoryStoreTests
    {
        [Fact]
        public async Task Should_return_empty_list_when_no_sources_exist()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();

            // Act
            var result = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_all_sources_when_source_ids_is_null()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);
            await repositoryStore.CreateOrUpdate(_stagingSource, TestContext.Current.CancellationToken);

            // Act
            var result = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task Should_return_only_matching_sources_when_source_ids_provided()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);
            await repositoryStore.CreateOrUpdate(_stagingSource, TestContext.Current.CancellationToken);

            // Act
            var result = await repositoryStore.GetRepositories([_releaseSource.Id], TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(1);
            result[0].Id.Should().Be(_releaseSource.Id);
        }

        [Fact]
        public async Task Should_return_empty_list_when_no_source_ids_match()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);

            // Act
            var result = await repositoryStore.GetRepositories([Guid.NewGuid()], TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeEmpty();
        }
    }

    public sealed class CreateOrUpdate : ArtifactRepositoryStoreTests
    {
        [Fact]
        public async Task Should_create_source_and_return_created_action()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();

            // Act
            var result = await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CrudAction.Created);
            var repositories = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            repositories.Should().HaveCount(1);
            repositories[0].Id.Should().Be(_releaseSource.Id);
        }

        [Fact]
        public async Task Should_update_existing_source_and_return_updated_action()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);

            var updatedSource = new ArtifactRepository
            {
                Id = _releaseSource.Id,
                Endpoint = "https://updated.endpoint/",
                UserName = "newuser",
                Password = "newpass",
                Modified = DateTimeOffset.UtcNow,
                ModifiedBy = Environment.UserName,
            };

            // Act
            var result = await repositoryStore.CreateOrUpdate(updatedSource, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CrudAction.Updated);
            var repositories = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            repositories.Should().HaveCount(1);
            repositories[0].Endpoint.Should().Be("https://updated.endpoint/");
            repositories[0].UserName.Should().Be("newuser");
            repositories[0].Password.Should().Be("newpass");
        }

        [Fact]
        public async Task Should_persist_multiple_created_sources()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();

            // Act
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);
            await repositoryStore.CreateOrUpdate(_stagingSource, TestContext.Current.CancellationToken);

            // Assert
            var repositories = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            repositories.Should().HaveCount(2);
        }
    }

    public sealed class Delete : ArtifactRepositoryStoreTests
    {
        [Fact]
        public async Task Should_delete_existing_source_and_return_it()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);
            await repositoryStore.CreateOrUpdate(_stagingSource, TestContext.Current.CancellationToken);

            // Act
            var deleted = await repositoryStore.Delete([_releaseSource.Id], TestContext.Current.CancellationToken);

            // Assert
            deleted.Should().HaveCount(1);
            deleted.First().Id.Should().Be(_releaseSource.Id);

            var remaining = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            remaining.Should().HaveCount(1);
            remaining[0].Id.Should().Be(_stagingSource.Id);
        }

        [Fact]
        public async Task Should_delete_multiple_sources_and_return_them()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);
            await repositoryStore.CreateOrUpdate(_stagingSource, TestContext.Current.CancellationToken);

            // Act
            var deleted = await repositoryStore.Delete([_releaseSource.Id, _stagingSource.Id], TestContext.Current.CancellationToken);

            // Assert
            deleted.Should().HaveCount(2);
            var remaining = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            remaining.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_empty_when_no_matching_source_to_delete()
        {
            // Arrange
            var repositoryStore = _services.GetRequiredService<IArtifactRepositoryStore>();
            await repositoryStore.CreateOrUpdate(_releaseSource, TestContext.Current.CancellationToken);

            // Act
            var deleted = await repositoryStore.Delete([Guid.NewGuid()], TestContext.Current.CancellationToken);

            // Assert
            deleted.Should().BeEmpty();
            var repositories = await repositoryStore.GetRepositories(null, TestContext.Current.CancellationToken);
            repositories.Should().HaveCount(1);
        }
    }
}
