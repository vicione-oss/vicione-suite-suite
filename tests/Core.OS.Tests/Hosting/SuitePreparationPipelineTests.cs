using AwesomeAssertions;
using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Xunit;

namespace Core.OS.Tests.Hosting;

public class SuitePreparationPipelineTests
{
    private record FailureResult(string Reason) : IPreparationAbortResult;

    public sealed class RunAsync : SuitePreparationPipelineTests
    {
        [Fact]
        public async Task Should_return_success_when_no_steps_are_registered()
        {
            // Arrange + Act
            var result = await new SuitePreparationPipeline()
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationSuccessResult>();
        }

        [Fact]
        public async Task Should_return_success_when_all_steps_succeed()
        {
            // Arrange + Act
            var result = await new SuitePreparationPipeline()
                .Use(() => { })
                .Use(_ => Task.CompletedTask)
                .Use(_ => Task.FromResult(PreparationResult.Success as IPreparationResult))
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
            var result = await new SuitePreparationPipeline()
                .Use(() => { })
                .Use(_ => Task.FromResult(failure as IPreparationResult))
                .Use(_ => { step3Executed = true; return Task.FromResult(PreparationResult.Success as IPreparationResult); })
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
            var result = await new SuitePreparationPipeline()
                .Use(_ => { executedSteps.Add(1); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
                .Use(_ => { executedSteps.Add(2); return Task.FromResult(new FailureResult("failed") as IPreparationResult); })
                .Use(_ => { executedSteps.Add(3); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
                .Use(_ => { executedSteps.Add(4); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
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
            var result = await new SuitePreparationPipeline()
                .Use(_ => { executedSteps.Add(1); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
                .Use(_ => { executedSteps.Add(2); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
                .Use(_ => { executedSteps.Add(3); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
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
            await new SuitePreparationPipeline()
                .Use(ct => { receivedTokens.Add(ct); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
                .Use(ct => { receivedTokens.Add(ct); return Task.FromResult(PreparationResult.Success as IPreparationResult); })
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
            var result = await new SuitePreparationPipeline()
                .Use(_ => Task.FromResult(new FailureResult(expectedReason) as IPreparationResult))
                .RunAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeAssignableTo<IPreparationAbortResult>()
                .Which.Reason.Should().Be(expectedReason);
        }
    }
}
