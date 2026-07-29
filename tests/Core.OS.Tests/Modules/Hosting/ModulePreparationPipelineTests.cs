using System.IO.Abstractions;
using AwesomeAssertions;
using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance;
using Core.OS.Modules.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Modules.Hosting;

public class ModulePreparationPipelineTests
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
    private readonly IArtifactRepositoryStore _repositoryStore = NSubstitute.Substitute.For<IArtifactRepositoryStore>();
    private readonly ILoggerFactory _loggerFactory = NSubstitute.Substitute.For<ILoggerFactory>();

    private ModulePreparationContext GetContext()
        => new(WebApplication.CreateBuilder([]), _fileSystem, _instanceOptions, _repositoryStore, _loggerFactory);

    public sealed class RunAsync : ModulePreparationPipelineTests
    {
        [Fact]
        public async Task Should_return_success_when_no_steps_are_registered()
        {
            // Arrange + Act
            using var pipeline = new ModulePreparationPipeline(GetContext());
            var result = await pipeline.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationSuccessResult>();
        }

        [Fact]
        public async Task Should_return_success_when_all_steps_succeed()
        {
            // Arrange + Act
            using var pipeline = new ModulePreparationPipeline(GetContext());
            var result = await pipeline
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
            using var pipeline = new ModulePreparationPipeline(GetContext());
            var result = await pipeline
                .Use((_, _) => Task.CompletedTask)
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
        public async Task Should_pass_cancellation_token_to_each_step()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var receivedTokens = new List<CancellationToken>();

            // Act
            using var pipeline = new ModulePreparationPipeline(GetContext());
            await pipeline
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
        public async Task Should_convert_a_thrown_exception_into_a_module_host_abort_result()
        {
            // Act
            using var pipeline = new ModulePreparationPipeline(GetContext());
            var result = await pipeline
                .Use((_, _) => Task.FromException<IPreparationResult>(new InvalidOperationException("boom")))
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<ModuleHostPreparationResult>()
                .Which.Reason.Should().Contain("boom");
        }

        [Fact]
        public async Task Should_not_execute_any_steps_after_a_throwing_step()
        {
            // Arrange
            var executedSteps = new List<int>();

            // Act
            using var pipeline = new ModulePreparationPipeline(GetContext());
            var result = await pipeline
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
            result.Should().BeOfType<ModuleHostPreparationResult>();
            executedSteps.Should().BeEquivalentTo([1]);
        }

        [Fact]
        public async Task Should_rethrow_operation_cancelled_exception_rather_than_converting_it()
        {
            // Arrange
            using var pipeline = new ModulePreparationPipeline(GetContext());
            pipeline.Use((_, ct) => throw new OperationCanceledException(ct));

            // Act
            var act = async () => await pipeline.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
