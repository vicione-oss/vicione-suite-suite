using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Sdk.Backend.Artifacts;
using Sdk.Modules;

namespace Core.Module.Tests;

public partial class ModuleArtifactRepositoryTests
{
    public sealed class ArchitectureNameFilter : ModuleArtifactRepositoryTests
    {
        private readonly IArtifactRepository _artifactRepository = Substitute.For<IArtifactRepository>();
        private readonly IArtifactQueryBuilder _queryBuilder = Substitute.For<IArtifactQueryBuilder>();

        public ArchitectureNameFilter()
        {
            _queryBuilder.AndPathMatches(Arg.Any<string>()).Returns(_queryBuilder);
            _queryBuilder.AndNameMatches(Arg.Any<string>()).Returns(_queryBuilder);
            _queryBuilder.OrderByDescending(Arg.Any<string[]>()).Returns(_queryBuilder);
            _queryBuilder.Build().Returns("query");
            _artifactRepository.CreateQueryBuilder().Returns(_queryBuilder);

            var artifact = Substitute.For<IArtifact>();
            var queryResult = Substitute.For<IArtifactQueryResult>();
            queryResult.Artifacts.Returns([artifact]);
            _artifactRepository.Query(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(queryResult);
            _artifactRepository.Download(Arg.Any<IArtifact>(), Arg.Any<CancellationToken>())
                .Returns(_ => new MemoryStream("{}"u8.ToArray()));
        }

        private ModuleArtifactRepository CreateRepository(string osPlatform, Architecture osArchitecture)
            => new(_artifactRepository, new FileSystem(), ArtifactPlatformTheoryData.Create(osPlatform, osArchitecture), _logger);

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.ModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_query_module_artifacts_by_platform(string osPlatform, Architecture osArchitecture, string runtimeIdentifier)
        {
            // Arrange
            var repository = CreateRepository(osPlatform, osArchitecture);

            // Act
            await repository.QueryModuleArtifacts(TestContext.Current.CancellationToken);

            // Assert - 0.28.0-ci1523472-linux-arm64.zip
            _queryBuilder.Received(1).AndNameMatches($"*{runtimeIdentifier}.zip");
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.ModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_query_module_artifact_by_platform(string osPlatform, Architecture osArchitecture, string runtimeIdentifier)
        {
            // Arrange
            var repository = CreateRepository(osPlatform, osArchitecture);
            var package = new ModuleDependencyPackage { Name = _packageName, Version = _packageVersion };

            // Act
            await repository.QueryModuleArtifact(package, TestContext.Current.CancellationToken);

            // Assert
            _queryBuilder.Received(1).AndNameMatches($"{_packageVersion}-{runtimeIdentifier}.zip");
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.ModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_query_metadata_download_by_platform(string osPlatform, Architecture osArchitecture, string runtimeIdentifier)
        {
            // Arrange
            var repository = CreateRepository(osPlatform, osArchitecture);
            var package = new ModuleDependencyPackage { Name = _packageName, Version = _packageVersion };

            // Act
            await using var stream = await repository.GetMetadataDownloadStream(package, TestContext.Current.CancellationToken);

            // Assert - 0.28.0-ci1523472-linux-arm64_0.25.0.json
            _queryBuilder.Received(1).AndNameMatches($"*{_packageVersion}-{runtimeIdentifier}_*.json");
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.ModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_query_module_metadata_artifacts_by_platform(string osPlatform, Architecture osArchitecture, string runtimeIdentifier)
        {
            // Arrange
            var repository = CreateRepository(osPlatform, osArchitecture);

            // Act
            await repository.QueryModuleMetadataArtifacts(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _queryBuilder.Received(1).AndNameMatches($"*{runtimeIdentifier}*.json");
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.ModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_query_latest_module_metadata_artifact_by_platform(string osPlatform, Architecture osArchitecture,
            string runtimeIdentifier)
        {
            // Arrange
            var repository = CreateRepository(osPlatform, osArchitecture);

            // Act
            await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, _packageName, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _queryBuilder.Received(1).AndNameMatches($"*{runtimeIdentifier}*_{_sdkVersion.Major}.*.json");
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.UnsupportedModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_throw_when_querying_module_artifacts_for_a_platform_without_module_packages(string osPlatform,
            Architecture osArchitecture)
        {
            // Arrange
            var repository = CreateRepository(osPlatform, osArchitecture);

            // Act
            var act = () => repository.QueryModuleArtifacts(TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<PlatformNotSupportedException>();
        }
    }
}
