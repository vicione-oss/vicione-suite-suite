using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Hosting;

public class SuitePreparationPipelineTests
{
    private record FailureResult(string Reason) : IPreparationAbortResult;
    private readonly InstanceOptions _instanceOptions = new()
    {
        HomeDirectory = "/home/test",
        CacheDirectory = "/cache/test",
        BackupDirectory = "/backup/test",
        Type = InstanceType.Standalone
    };
    private readonly IFileSystem _fileSystem = NSubstitute.Substitute.For<IFileSystem>();
    private readonly ILoggerFactory _loggerFactory = NSubstitute.Substitute.For<ILoggerFactory>();

    private SuitePreparationContext GetContext() => new(_fileSystem, _instanceOptions, _loggerFactory);

    public sealed class RunAsync : SuitePreparationPipelineTests
    {
        [Fact]
        public async Task Should_return_success_when_no_steps_are_registered()
        {
            // Arrange + Act
            var result = await new SuitePreparationPipeline(GetContext())
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationSuccessResult>();
        }

        [Fact]
        public async Task Should_return_success_when_all_steps_succeed()
        {
            // Arrange + Act
            var result = await new SuitePreparationPipeline(GetContext())
                .Use(() => { })
                .Use((_, _) => Task.CompletedTask)
                .Use((_, _) => Task.FromResult(PreparationResult.Success as IPreparationResult))
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationSuccessResult>();
        }

        [Fact]
        public async Task Should_stop_at_first_failed_step_and_return_its_result()
        {
            // Arrange
            var failure = new FailureResult("step 2 failed");
            var step3Executed = false;

            // Act
            var result = await new SuitePreparationPipeline(GetContext())
                .Use(() => { })
                .Use((_, _) => Task.FromResult(failure as IPreparationResult))
                .Use((_, _) =>
                {
                    step3Executed = true;
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeSameAs(failure);
            step3Executed.Should().BeFalse();
        }

        [Fact]
        public async Task Should_not_execute_any_steps_after_a_failure()
        {
            // Arrange
            var executedSteps = new List<int>();

            // Act
            var result = await new SuitePreparationPipeline(GetContext())
                .Use((_, _) =>
                {
                    executedSteps.Add(1);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .Use((_, _) =>
                {
                    executedSteps.Add(2);
                    return Task.FromResult(new FailureResult("failed") as IPreparationResult);
                })
                .Use((_, _) =>
                {
                    executedSteps.Add(3);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .Use((_, _) =>
                {
                    executedSteps.Add(4);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationAbortResult>();
            executedSteps.Should().BeEquivalentTo([1, 2]);
        }

        [Fact]
        public async Task Should_execute_all_steps_when_all_succeed()
        {
            // Arrange
            var executedSteps = new List<int>();

            // Act
            var result = await new SuitePreparationPipeline(GetContext())
                .Use((_, _) =>
                {
                    executedSteps.Add(1);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .Use((_, _) =>
                {
                    executedSteps.Add(2);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .Use((_, _) =>
                {
                    executedSteps.Add(3);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationSuccessResult>();
            executedSteps.Should().BeEquivalentTo([1, 2, 3]);
        }

        [Fact]
        public async Task Should_pass_cancellation_token_to_each_step()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var receivedTokens = new List<CancellationToken>();

            // Act
            await new SuitePreparationPipeline(GetContext())
                .Use((_, ct) =>
                {
                    receivedTokens.Add(ct);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .Use((_, ct) =>
                {
                    receivedTokens.Add(ct);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .RunAsync(cts.Token);

            // Assert
            receivedTokens.Should().AllSatisfy(t => t.Should().Be(cts.Token));
        }

        [Fact]
        public async Task Should_return_failure_result_containing_the_reason()
        {
            // Arrange
            const string expectedReason = "something went wrong";

            // Act
            var result = await new SuitePreparationPipeline(GetContext())
                .Use((_, _) => Task.FromResult(new FailureResult(expectedReason) as IPreparationResult))
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationAbortResult>()
                .Which.Reason.Should().Be(expectedReason);
        }

        [Fact]
        public async Task Should_convert_a_thrown_exception_into_an_abort_result()
        {
            // Act
            var result = await new SuitePreparationPipeline(GetContext())
                .Use((_, _) => Task.FromException<IPreparationResult>(new InvalidOperationException("boom")))
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationAbortResult>()
                .Which.Reason.Should().Contain("boom");
        }

        [Fact]
        public async Task Should_not_execute_any_steps_after_a_throwing_step()
        {
            // Arrange
            var executedSteps = new List<int>();

            // Act
            var result = await new SuitePreparationPipeline(GetContext())
                .Use((_, _) =>
                {
                    executedSteps.Add(1);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .Use((_, _) => throw new InvalidOperationException("boom"))
                .Use((_, _) =>
                {
                    executedSteps.Add(3);
                    return Task.FromResult(PreparationResult.Success as IPreparationResult);
                })
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationAbortResult>();
            executedSteps.Should().BeEquivalentTo([1]);
        }

        [Fact]
        public async Task Should_rethrow_operation_cancelled_exception_rather_than_converting_it()
        {
            // Arrange
            var pipeline = new SuitePreparationPipeline(GetContext())
                .Use((_, ct) => throw new OperationCanceledException(ct));

            // Act
            var act = async () => await pipeline.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
    }

    /// <summary>
    /// Migrated from the former <c>WebApplicationBuilderExtensionsTests.PrepareSuite</c>.
    /// These tests exercise the individual filesystem preparation steps end-to-end through the pipeline.
    /// </summary>
    public sealed class Steps
    {
        private readonly MockFileSystem _fileSystem = new();
        private readonly ILoggerFactory _loggerFactory = NSubstitute.Substitute.For<ILoggerFactory>();
        private readonly InstanceOptions _instanceOptions;

        public Steps()
        {
            _loggerFactory.CreateLogger(NSubstitute.Arg.Any<string>()).Returns(NSubstitute.Substitute.For<ILogger>());

            _instanceOptions = new InstanceOptions
            {
                HomeDirectory = _fileSystem.Path.GetFullPath("AppData"),
                CacheDirectory = _fileSystem.Path.GetFullPath("Cache"),
                BackupDirectory = _fileSystem.Path.GetFullPath("Backup"),
                Type = InstanceType.Standalone,
            };

            _fileSystem.AddDirectory(_instanceOptions.CacheDirectory);
            _fileSystem.AddDirectory(_instanceOptions.HomeDirectory);
        }

        private Task<IPreparationResult> PrepareSuite(WebApplicationBuilder builder, CancellationToken cancellationToken)
            => new SuitePreparationPipeline(new SuitePreparationContext(_fileSystem, _instanceOptions, _loggerFactory))
                .UseInstanceId()
                .UseDeviceImageCleanup()
                .UseResetFile()
                .UseRestore()
                .UseVersionDowngradeCheck()
                .UseRecoveryMode(builder)
                .RunAsync(cancellationToken);

        [Fact]
        public async Task Should_ensure_instance_file_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            // Act
            await PrepareSuite(builder, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_return_downgrade_preparation_result_when_persisted_version_is_higher()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            // persist a higher version than running suite to simulate downgrade
            var parts = SuiteVersionUtils.GetSuiteVersion().Split('.');
            var major = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var minor = int.Parse(parts[1], CultureInfo.InvariantCulture) + 1;
            var patch = int.Parse(parts[2], CultureInfo.InvariantCulture);
            var higherVersion = $"{major}.{minor}.{patch}";

            await _fileSystem.WriteDataVersionFile(_instanceOptions, higherVersion, TestContext.Current.CancellationToken);

            // Act
            var result = await PrepareSuite(builder, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<VersionDowngradePreparationResult>();
            var downgrade = (VersionDowngradePreparationResult)result!;
            downgrade.DowngradeInformation.DataVersion.Should().Be(higherVersion);
            downgrade.DowngradeInformation.CurrentVersion.Should().Be(SuiteVersionUtils.GetSuiteVersion());
        }

        [Fact]
        public async Task Should_delete_left_free_device_image()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var cacheDirectory = _fileSystem.GetRootedCacheDirectory(_instanceOptions);
            var deviceImageFile = _fileSystem.Path.Combine(cacheDirectory, Shared.Constants.SystemModuleId, Shared.Constants.DeviceImageFileName);

            SetupTestFiles(_fileSystem, cacheDirectory);
            _fileSystem.AddEmptyFile(deviceImageFile);

            // Act
            await PrepareSuite(builder, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(deviceImageFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_clear_instance_directories_if_reset_file_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var homeDirectory = _fileSystem.GetRootedHomeDirectory(_instanceOptions);
            var cacheDirectory = _fileSystem.GetRootedCacheDirectory(_instanceOptions);
            var backupDirectory = _fileSystem.GetRootedBackupDirectory(_instanceOptions);

            SetupTestFiles(_fileSystem, homeDirectory);
            SetupTestFiles(_fileSystem, cacheDirectory);
            SetupTestFiles(_fileSystem, backupDirectory);

            _fileSystem.WriteResetFile(_instanceOptions);

            // Act
            await PrepareSuite(builder, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.Directory.GetDirectories(homeDirectory).Should().BeEmpty();
            _fileSystem.Directory.GetDirectories(cacheDirectory).Should().BeEmpty();
            _fileSystem.Directory.GetFiles(backupDirectory).Should().BeEmpty();
            _fileSystem.ResetFileExists(_instanceOptions).Should().BeFalse();
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_clear_instance_directories_and_artifact_sources_if_reset_file_exists_and_folder_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var homeDirectory = _fileSystem.GetRootedHomeDirectory(_instanceOptions);
            var backupDirectory = _fileSystem.GetRootedBackupDirectory(_instanceOptions);
            var reposSourceFile = _fileSystem.Path.Combine(homeDirectory, "repo-sources.json");

            _fileSystem.AddEmptyFile(reposSourceFile);
            SetupTestFiles(_fileSystem, homeDirectory);
            SetupTestFiles(_fileSystem, backupDirectory);

            _fileSystem.WriteResetFile(_instanceOptions);

            // Act
            await PrepareSuite(builder, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.Directory.GetDirectories(homeDirectory).Should().BeEmpty();
            _fileSystem.Directory.GetFiles(backupDirectory).Should().BeEmpty();
            _fileSystem.File.Exists(reposSourceFile).Should().BeFalse();

            _fileSystem.ResetFileExists(_instanceOptions).Should().BeFalse();
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_not_touch_instance_directories_if_reset_file_not_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var homeDirectory = _fileSystem.GetRootedHomeDirectory(_instanceOptions);
            var cacheDirectory = _fileSystem.GetRootedCacheDirectory(_instanceOptions);
            var backupDirectory = _fileSystem.GetRootedBackupDirectory(_instanceOptions);

            SetupTestFiles(_fileSystem, homeDirectory);
            SetupTestFiles(_fileSystem, cacheDirectory);
            SetupTestFiles(_fileSystem, backupDirectory);

            // Act
            await PrepareSuite(builder, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.Directory.GetDirectories(homeDirectory).Should().NotBeEmpty();
            _fileSystem.Directory.GetDirectories(cacheDirectory).Should().NotBeEmpty();
            _fileSystem.Directory.GetFiles(backupDirectory).Should().NotBeEmpty();
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        private static void SetupTestFiles(MockFileSystem fileSystem, string targetPath)
        {
            var folder = fileSystem.Path.Combine(targetPath, "folder");
            fileSystem.AddDirectory(folder);

            var subFolder = fileSystem.Path.Combine(targetPath, "subfolder");
            fileSystem.AddDirectory(subFolder);

            fileSystem.AddEmptyFile(fileSystem.Path.Combine(targetPath, "root.json"));
            fileSystem.AddEmptyFile(fileSystem.Path.Combine(folder, "a.file"));
            fileSystem.AddEmptyFile(fileSystem.Path.Combine(subFolder, "some.file"));
        }
    }
}
