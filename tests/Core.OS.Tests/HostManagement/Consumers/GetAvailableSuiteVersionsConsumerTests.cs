using AwesomeAssertions;
using Core.Module;
using Core.Module.Contracts;
using Core.OS.Instance.Consumers;
using Core.Shared.HostManagement;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Semver;
using Xunit;
using static Core.OS.Tests.TestFactory;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class GetAvailableSuiteVersionsConsumerTests
{
    private const string Architecture = "amd64";

    private readonly ISuiteArtifactRepository _repository = Substitute.For<ISuiteArtifactRepository>();

    private readonly SuiteBundleVersion _bundle104 = new("1.0.4", new Version(1, 1, 1));
    private readonly SuiteBundleVersion _bundle106 = new("1.0.6", new Version(1, 1, 1));
    private readonly SuiteBundleVersion _bundle110 = new("1.1.0", new Version(1, 2, 1));
    private readonly SuiteBundleVersion _bundle110dev = new("1.1.0-ci324324", new Version(1, 2, 0));
    private readonly SuiteBundleVersion _bundle117 = new("1.1.7", new Version(1, 2, 1));

    private MassTransitTester SetupTester(string installedSuiteVersion) =>
        new(cfg =>
        {
            cfg.AddConsumer<GetAvailableSuiteVersionsConsumer>();
            cfg.AddSingleton(_repository);
            cfg.AddSingleton(s =>
            {
                var info = Substitute.For<IInstanceInformation>();
                info.Version.Returns(installedSuiteVersion);

                var infoProvider = Substitute.For<IInstanceInformationProvider>();
                infoProvider.Local.Returns(info);

                return infoProvider;
            });
        });

    [Fact]
    public async Task Should_return_suite_versions_ordered_by_version_ascending()
    {
        // Arrange
        await using var tester = SetupTester("1.0.4");
        var request = new GetAvailableSuiteVersions();

        _repository.QuerySuiteArtifactBundles(Arg.Any<Version>(), false, Arg.Any<CancellationToken>())
            .Returns([
                CreateSuiteArtifactBundle(_bundle117.SuiteVersion, _bundle117.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle104.SuiteVersion, _bundle104.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle110.SuiteVersion, _bundle110.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle106.SuiteVersion, _bundle106.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle110dev.SuiteVersion, _bundle110dev.HostManagementVersion),
            ]);

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Select(k => SemVersion.Parse(k.Version)).Should().BeInAscendingOrder(SemVersion.SortOrderComparer);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_only_return_version_greater_or_equal_to_installed_version()
    {
        // Arrange
        var installedVersion = "1.1.0";
        await using var tester = SetupTester(installedVersion);
        var request = new GetAvailableSuiteVersions();

        _repository.QuerySuiteArtifactBundles(Arg.Any<Version>(), false, Arg.Any<CancellationToken>())
            .Returns([
                CreateSuiteArtifactBundle(_bundle117.SuiteVersion, _bundle117.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle104.SuiteVersion, _bundle104.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle110dev.SuiteVersion, _bundle110dev.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle106.SuiteVersion, _bundle106.HostManagementVersion),
            ]);

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Should().ContainSingle(k => k.Version == installedVersion);
        response.Versions.Should().ContainSingle(k => k.Version == "1.1.7");
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_allow_ci_versions_for_valid_major_minor_patch_packages()
    {
        // Arrange
        var installedVersion = "1.1.0";
        await using var tester = SetupTester(installedVersion);
        var request = new GetAvailableSuiteVersions();

        _repository.QuerySuiteArtifactBundles(Arg.Any<Version>(), false, Arg.Any<CancellationToken>())
            .Returns([
                CreateSuiteArtifactBundle(_bundle117.SuiteVersion, _bundle117.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle110dev.SuiteVersion, _bundle110dev.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle104.SuiteVersion, _bundle104.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle110.SuiteVersion, _bundle110.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle106.SuiteVersion, _bundle106.HostManagementVersion),
            ]);

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Should().HaveCount(3, "1.1.0, 1.1.0dev, 1.1.7");
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_installed_suite_version_if_artifact_version_exists()
    {
        // Arrange
        var installedVersion = "1.0.4";
        await using var tester = SetupTester(installedVersion);

        var request = new GetAvailableSuiteVersions();

        _repository.QuerySuiteArtifactBundles(Arg.Any<Version>(), false, Arg.Any<CancellationToken>())
            .Returns([
                CreateSuiteArtifactBundle(_bundle104.SuiteVersion, _bundle104.HostManagementVersion),
                CreateSuiteArtifactBundle(_bundle110.SuiteVersion, _bundle110.HostManagementVersion),
            ]);

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Should().HaveCount(2);
        response.Versions.Should().ContainSingle(k => k.Version == "1.0.4");
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_add_installed_suite_version_if_no_matching_artifact_version_exists()
    {
        // Arrange
        var installedVersion = "1.0.7";
        await using var tester = SetupTester(installedVersion);
        var request = new GetAvailableSuiteVersions();

        _repository.QuerySuiteArtifactBundles(Arg.Any<Version>(), false, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Should().ContainSingle(k => k.Version == installedVersion);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_response_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = SetupTester("1.1.1");
        var request = new GetAvailableSuiteVersions();

        _repository.QuerySuiteArtifactBundles(Arg.Any<Version>(), false, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provoked Excpetion"));

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Should().BeEmpty();
        response.RequestError.Should().NotBeNull();
    }

    private static SuiteArtifactBundle CreateSuiteArtifactBundle(string suiteVersion, Version hostMgmtVersion)
    {
        return new()
        {
            Architecture = Architecture,
            Version = suiteVersion,
            HostManagementVersion = hostMgmtVersion,
            Package = new Artifact
            {
                Name = $"vicione-suite_{suiteVersion.Replace("-ci", "~")}_{Architecture}.deb",
                Path = "suites",
                Repository = "vicione-suite"
            },
            PackageSignature = new Artifact
            {
                Name = $"vicione-suite_{suiteVersion.Replace("-ci", "~")}_{Architecture}.deb.minisig",
                Path = "suites",
                Repository = "vicione-suite"
            },
        };
    }

    private record SuiteBundleVersion(string SuiteVersion, Version HostManagementVersion);
}
