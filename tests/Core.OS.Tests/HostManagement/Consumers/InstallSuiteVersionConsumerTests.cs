using AwesomeAssertions;
using Core.Module;
using Core.Module.Contracts;
using Core.OS.HostManagement.Consumers;
using Core.OS.Modules;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Backend.Modules;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class InstallSuiteVersionConsumerTests
{
    private readonly ISuiteArtifactRepository _repository = Substitute.For<ISuiteArtifactRepository>();
    private readonly IWorkspaceProvider<SystemBackendModule> _wsProvider = Substitute.For<IWorkspaceProvider<SystemBackendModule>>();

    [Fact]
    public async Task Should_send_install_started_event()
    {
        // Arrange
        var packageName = "vicione-suite_1.0.2_amd64_1.1.0.deb";
        var signatureName = "vicione-suite_1.0.2_amd64_1.1.0.deb.minisig";

        var packageFilePath = $"/path/to/downloaded/{packageName}";
        var signatureFilePath = $"/path/to/downloaded/{signatureName}";

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<InstallSuiteVersionConsumer>();
            cfg.AddMockPipeClientSystemConfiguration();
            cfg.AddSingleton(_repository);
            cfg.AddSingleton(_wsProvider);
        });
        var command = new InstallSuiteVersion(packageName, signatureName);

        _repository.DownloadAndValidate(Arg.Any<string>(), packageName, signatureName, Arg.Any<CancellationToken>())
            .Returns(new SuitePackageDownloadResult(packageFilePath, signatureFilePath));

        // Act
        await tester.TestCommand<InstallSuiteVersion, InstallSuiteVersionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<InstallSuiteVersionStarted>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_error_event_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<InstallSuiteVersionConsumer>();
            cfg.AddMockPipeClientSystemConfiguration();
            cfg.AddSingleton(_repository);
            cfg.AddSingleton(_wsProvider);
        });

        var command = new InstallSuiteVersion("vicione-suite_1.0.2_win-x64_1.1.0.deb", "");

        _repository.DownloadAndValidate(Arg.Any<string>(), Arg.Any<string>(), "", Arg.Any<CancellationToken>())
            .ThrowsAsync<InvalidOperationException>();

        // Act
        await tester.TestCommand<InstallSuiteVersion, InstallSuiteVersionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<InstallSuiteVersionError>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
