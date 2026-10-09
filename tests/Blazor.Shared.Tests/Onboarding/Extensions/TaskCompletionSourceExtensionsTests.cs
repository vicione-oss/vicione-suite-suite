using Blazor.Shared.Onboarding.Extensions;
using Sdk.Client.Wizards.Models;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests.Onboarding.Extensions;

public sealed class TaskCompletionSourceExtensionsTests
{
    private readonly TaskCompletionSource<ErrorInfo?> _taskCompletionSource = new();

    [Fact]
    public async Task Should_return_success_when_command_completes_without_error()
    {
        // Arrange
        _taskCompletionSource.SetResult(null);

        // Act
        var result = await _taskCompletionSource.WaitForCommandCompletion(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_map_error_to_save_error_result()
    {
        // Arrange
        _taskCompletionSource.SetResult(new ErrorInfo(42, "command failed"));

        // Act
        var result = await _taskCompletionSource.WaitForCommandCompletion(TestContext.Current.CancellationToken);

        // Assert
        var error = result.Should().BeOfType<SaveErrorResult>().Subject;
        error.Message.Should().Be("command failed");
        error.ErrorCode.Should().Be(42);
    }

    [Fact]
    public async Task Should_use_generic_message_for_error_without_message()
    {
        // Arrange
        _taskCompletionSource.SetResult(new ErrorInfo(42, null));

        // Act
        var result = await _taskCompletionSource.WaitForCommandCompletion(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be(CommonPhrases.AnUnexpectedErrorOccurred);
    }

    [Fact]
    public async Task Should_use_custom_error_mapping()
    {
        // Arrange
        var mappedResult = new SaveErrorResult("mapped");
        _taskCompletionSource.SetResult(new ErrorInfo(42, "command failed"));

        // Act
        var result = await _taskCompletionSource.WaitForCommandCompletion(_ => mappedResult, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(mappedResult);
    }

    [Fact]
    public async Task Should_complete_when_result_arrives_while_waiting()
    {
        // Arrange
        var waitTask = _taskCompletionSource.WaitForCommandCompletion(TestContext.Current.CancellationToken);

        // Act
        _taskCompletionSource.SetResult(null);
        var result = await waitTask;

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }
}
