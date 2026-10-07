using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Core.Artifacts;
using Core.Artifacts.Extensions;
using Core.Module.Contracts;
using Core.Tests.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Artifacts;
using Sdk.Testing.Client;
using Semver;

namespace Core.Module.Tests;

public class SuiteArtifactRepositoryTests
{
    /// <summary>
    /// Tests query <c>amd64</c> like the bundle under test, so the results do not depend on the test machine.
    /// </summary>
    private static readonly ArtifactPlatform LinuxX64Platform = new(OSPlatform.Linux, Architecture.X64);

    private static ServiceProvider CreateServiceProvider(ArtifactRepositoryOptions? apiOptions = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(apiOptions ?? SystemTestSettings.GetArtifactRepositoryOptions());
        var optionsProvider = Substitute.For<IArtifactRepositoryOptionsProvider>();
        optionsProvider.GetOptions().Returns(options.Value);

        return new ServiceCollection()
            .AddSingleton<HttpClient>()
            .AddSingleton(options)
            .AddSingleton<IFileSystem>(new FileSystem())
            .AddSingleton(Substitute.For<ILogger<SuiteArtifactRepository>>)
            .AddSingleton<SuiteArtifactRepository>()
            .AddSingleton<IArtifactPlatform>(LinuxX64Platform)
            .AddSingleton(options)
            .AddHttpClient()
            .AddArtifactRepository(s => optionsProvider)
            .BuildServiceProvider();
    }

    [Collection(NonParallelCollectionDefinitionClass.NonParallelCollection)]
    [Trait(Traits.Category, Traits.System)]
    public sealed class DownloadAndValidate : SuiteArtifactRepositoryTests
    {
        // We can't not use FileSystemAbstractions because Minisig does not support it
        private const string DownloadPath = "C:\\Work\\suite-download";

        private readonly List<string> PublicKeys = [
            "RWSbNk9dC1tWG0M19wR8eHH3F4nsqeVi7r6kgVBMYJd6aabbUsqpTLUT"
            ];

        private readonly SuiteArtifactBundle _bundle;

        public DownloadAndValidate()
        {
            var package = Substitute.For<IArtifact>();
            package.Name.Returns("vicione-suite_1.1.0~1944853_amd64.deb");
            package.Path.Returns("suites");
            package.Repository.Returns("vicione-suite-dev");

            // Only 330 byte
            var packageSignature = Substitute.For<IArtifact>();
            packageSignature.Name.Returns("vicione-suite_1.1.0~1944853_amd64.deb.minisig");
            packageSignature.Path.Returns("suites");
            packageSignature.Repository.Returns("vicione-suite-dev");

            // Suite deb package has ~ 66Mb
            _bundle = new SuiteArtifactBundle
            {
                Architecture = "amd64",
                Version = SemVersion.Parse("1.1.0-ci1944853"),
                Package = package,
                PackageSignature = packageSignature
            };
        }

        private ArtifactRepositoryOptions CreateOptionsWithKeys()
        {
            var options = SystemTestSettings.GetArtifactRepositoryOptions();
            options.PublicKeys = PublicKeys;
            return options;
        }

        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_download_and_validate_suite_install_package()
        {
            // Arrange            
            await using var services = CreateServiceProvider(CreateOptionsWithKeys());
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();

            // Act
            var result = await repository.DownloadAndValidate(DownloadPath, _bundle, TestContext.Current.CancellationToken);

            // Assert
            result.FilePath.Should().Be(fileSystem.Path.Combine(DownloadPath, _bundle.Package.Name));
            fileSystem.File.Exists(fileSystem.Path.Combine(DownloadPath, _bundle.PackageSignature!.Name))
                .Should().BeTrue("Signature is kept for validation by HM");
        }

        [Fact]
        public async Task Should_throw_when_no_package_signature_is_given()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();
            var suiteBundle = new SuiteArtifactBundle
            {
                Version = _bundle.Version,
                Architecture = _bundle.Architecture,
                Package = _bundle.Package,
                PackageSignature = null
            };

            // Act
            var action = () => repository.DownloadAndValidate(DownloadPath, suiteBundle, TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Download of*");
        }

        [Fact]
        public async Task Should_throw_when_no_public_keys_are_configured()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();

            // Act
            var action = () => repository.DownloadAndValidate(DownloadPath, _bundle, TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("At least one entry*");
        }

        [Fact]
        public async Task Should_throw_when_suite_package_signature_cannot_be_validated_with_keys_from_options()
        {
            // Arrange
            var options = SystemTestSettings.GetArtifactRepositoryOptions();
            options.PublicKeys = [Convert.ToBase64String("INVALID_PUBLIC_KEY"u8.ToArray())];

            await using var services = CreateServiceProvider(options);
            var repository = services.GetRequiredService<SuiteArtifactRepository>();

            // Act
            var action = () => repository.DownloadAndValidate(DownloadPath, _bundle, TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Suite package '*' signature is invalid.");
        }

        [Fact]
        public async Task Should_throw_when_suite_download_fails()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();
            var suiteArtifact = Substitute.For<IArtifact>();
            suiteArtifact.Name.Returns("i_does_not_exist_on_repo");
            suiteArtifact.Path.Returns("suites");
            suiteArtifact.Repository.Returns("vicione-suite");
            var suiteBundle = new SuiteArtifactBundle
            {
                Version = _bundle.Version,
                Architecture = _bundle.Architecture,
                Package = suiteArtifact,
                PackageSignature = _bundle.PackageSignature,
            };

            // Act
            var action = () => repository.DownloadAndValidate(DownloadPath, suiteBundle, TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_throw_when_signature_download_fails()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();
            var invalidSignature = Substitute.For<IArtifact>();
            invalidSignature.Name.Returns("i_does_not_exist_on_repo");
            invalidSignature.Path.Returns("suites");
            invalidSignature.Repository.Returns("vicione-suite");
            var suiteBundle = new SuiteArtifactBundle
            {
                Version = _bundle.Version,
                Architecture = _bundle.Architecture,
                Package = _bundle.Package,
                PackageSignature = invalidSignature
            };

            // Act
            var action = () => repository.DownloadAndValidate(DownloadPath, suiteBundle, TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<HttpRequestException>();
        }
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class QueryAllSuiteArtifacts : SuiteArtifactRepositoryTests
    {
        [Fact]
        public async Task Should_return_all_suite_artifacts()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();

            // Act
            var result = await repository.QueryAllSuiteArtifacts(TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeEmpty();
        }
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class QuerySuiteArtifactBundles : SuiteArtifactRepositoryTests
    {
        private readonly Version _hmVersion = new(1, 1, 0);

        [Fact]
        public async Task Should_return_only_signed_artifacts()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();

            // Act
            var result = await repository.QuerySuiteArtifactBundles(_hmVersion, false, TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_include_unsigned_artifacts_if_flag_is_set()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();

            // Act
            var result = await repository.QuerySuiteArtifactBundles(_hmVersion, true, TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeEmpty();
        }
    }

    public sealed class ArchitectureNameFilter : SuiteArtifactRepositoryTests
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

            var queryResult = Substitute.For<IArtifactQueryResult>();
            queryResult.Artifacts.Returns([]);
            _artifactRepository.Query(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(queryResult);
        }

        private SuiteArtifactRepository CreateRepository(Architecture osArchitecture)
            => new(_artifactRepository,
                new FileSystem(),
                Microsoft.Extensions.Options.Options.Create(new ArtifactRepositoryOptions()),
                ArtifactPlatformTheoryData.Create("LINUX", osArchitecture),
                Substitute.For<ILogger<SuiteArtifactRepository>>());

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.SuitePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_query_suite_artifact_bundles_by_platform(Architecture osArchitecture, string packageArchitecture)
        {
            // Arrange
            var repository = CreateRepository(osArchitecture);

            // Act
            await repository.QuerySuiteArtifactBundles(new Version(1, 0, 0), cancellationToken: TestContext.Current.CancellationToken);

            // Assert - vicione-suite_1.0.3_amd64_1.1.0.json
            _queryBuilder.Received(1).AndNameMatches($"*_{packageArchitecture}*");
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.UnsupportedSuitePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public async Task Should_throw_when_querying_suite_artifact_bundles_for_an_architecture_without_suite_packages(Architecture osArchitecture)
        {
            // Arrange
            var repository = CreateRepository(osArchitecture);

            // Act
            var act = () => repository.QuerySuiteArtifactBundles(new Version(1, 0, 0), cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<PlatformNotSupportedException>();
        }
    }
}
