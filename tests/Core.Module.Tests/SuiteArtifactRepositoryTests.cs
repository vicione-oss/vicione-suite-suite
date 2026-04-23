using System.IO.Abstractions;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using Core.Artifacts;
using Core.Artifacts.Extensions;
using Core.Module.Contracts;
using Core.Tests.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Sdk.Testing.Client;
using Semver;
using Xunit;

namespace Core.Module.Tests;

public class SuiteArtifactRepositoryTests
{
    private static ServiceProvider CreateServiceProvider(ArtifactRepositoryOptions? apiOptions = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(apiOptions ?? SystemTestSettings.ArtifactApiOptions);
        var optionsProvider = Substitute.For<IArtifactRepositoryOptionsProvider>();
        optionsProvider.GetOptions().Returns(options.Value);

        return new ServiceCollection()
            .AddSingleton<HttpClient>()
            .AddSingleton(options)
            .AddSingleton<IFileSystem>(new FileSystem())
            .AddSingleton(Substitute.For<ILogger<SuiteArtifactRepository>>)
            .AddSingleton<SuiteArtifactRepository>()
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
            var options = SystemTestSettings.ArtifactApiOptions;
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
            var options = SystemTestSettings.ArtifactApiOptions;
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

        [Fact]
        public async Task Should_return_architecture_filter()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<SuiteArtifactRepository>();

            // Act
            var result = repository.GetOSArchitectureFilter();

            // Assert - vicione-suite_1.0.3_arm64_1.1.0.json
            var expected = $"*_{RuntimeInformation.OSArchitecture}*";

            string.Compare(result, expected, StringComparison.OrdinalIgnoreCase).Should().Be(0);
        }
    }
}
