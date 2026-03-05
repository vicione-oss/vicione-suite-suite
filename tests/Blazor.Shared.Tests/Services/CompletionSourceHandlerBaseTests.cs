using Blazor.Shared.Services;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Shared.Tests.Services;

public class CompletionSourceHandlerBaseTests
{
    private sealed record TestCommand : ICommand
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    /// <summary>
    /// Minimal concrete subclass that exposes the protected mechanics for testing.
    /// </summary>
    private sealed class TestableHandler(IUiMediator mediator) : CompletionSourceHandlerBase<ISaveResult>(mediator)
    {
        protected override ISaveResult CreateSuccessResult() => new SaveSuccessResult();

        protected override ISaveResult CreateErrorResult(string errorMessage, int? errorCode = null)
            => new SaveErrorResult(errorMessage, errorCode);

        // SendAndWaitForCompletion overloads

        public Task<ISaveResult> InvokeSendAndWaitForCompletion(TestCommand command, CancellationToken cancellationToken = default)
            => SendAndWaitForCompletion(command, cancellationToken);

        public Task<ISaveResult> InvokeSendAndWaitForCompletion(TestCommand command, Func<ErrorInfo, ISaveResult> errorOccurred, CancellationToken cancellationToken = default)
            => SendAndWaitForCompletion(command, errorOccurred, cancellationToken);

        public Task<ISaveResult> InvokeSendAndWaitForCompletionAfterwards(TestCommand command, Func<CancellationToken, Task>? afterSend = null, CancellationToken cancellationToken = default)
            => SendAndWaitForCompletionAfterwards(command, afterSend, cancellationToken);

        // Simulate backend event responses

        public bool SimulateSuccess(Guid correlationId) => CompleteWithSuccess(correlationId);

        public bool SimulateError(Guid correlationId, ErrorInfo? errorInfo) => CompleteWithError(correlationId, errorInfo);
    }

    public class SendAndWaitForCompletion
    {
        [Fact]
        public async Task Returns_SaveSuccessResult_when_backend_signals_success()
        {
            // Arrange
            var command = new TestCommand();
            var mediator = Substitute.For<IUiMediator>();
            using var sut = new TestableHandler(mediator);

            mediator.When(m => m.Send(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()))
                .Do(callinfo => sut.SimulateSuccess(callinfo.Arg<TestCommand>().CorrelationId));

            // Act
            var result = await sut.InvokeSendAndWaitForCompletion(command, TestContext.Current.CancellationToken);

            // Assert
            await mediator.Received().Send(Arg.Is<TestCommand>(c => c.CorrelationId == command.CorrelationId), Arg.Any<CancellationToken>());
            Assert.IsType<SaveSuccessResult>(result);
        }

        [Fact]
        public async Task Returns_SaveErrorResult_when_backend_signals_error()
        {
            // Arrange
            var command = new TestCommand();
            var mediator = Substitute.For<IUiMediator>();
            using var sut = new TestableHandler(mediator);
            var errorInfo = new ErrorInfo(42, "Something went wrong");

            mediator.When(m => m.Send(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()))
                .Do(callinfo => sut.SimulateError(callinfo.Arg<TestCommand>().CorrelationId, errorInfo));

            // Act
            var result = await sut.InvokeSendAndWaitForCompletion(command, TestContext.Current.CancellationToken);

            // Assert
            await mediator.Received().Send(Arg.Is<TestCommand>(c => c.CorrelationId == command.CorrelationId), Arg.Any<CancellationToken>());
            Assert.IsType<SaveErrorResult>(result);
        }
    }

    public class SendAndWaitForCompletion_WithCustomErrorHandler
    {
        [Fact]
        public async Task Does_not_invoke_custom_error_handler_when_backend_signals_success()
        {
            // Arrange
            var command = new TestCommand();
            var mediator = Substitute.For<IUiMediator>();
            using var sut = new TestableHandler(mediator);
            var customErrorHandlerInvoked = false;

            mediator.When(m => m.Send(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()))
                .Do(callinfo => sut.SimulateSuccess(callinfo.Arg<TestCommand>().CorrelationId));

            // Act
            var result = await sut.InvokeSendAndWaitForCompletion(command, _ =>
            {
                customErrorHandlerInvoked = true;
                return new SaveErrorResult("custom error");
            }, TestContext.Current.CancellationToken);

            // Assert
            Assert.IsType<SaveSuccessResult>(result);
            Assert.False(customErrorHandlerInvoked);
        }

        [Fact]
        public async Task Invokes_custom_error_handler_with_ErrorInfo_when_backend_signals_error()
        {
            // Arrange
            var command = new TestCommand();
            var mediator = Substitute.For<IUiMediator>();
            using var sut = new TestableHandler(mediator);
            var errorInfo = new ErrorInfo(42, "Something went wrong");
            ErrorInfo? capturedErrorInfo = null;

            mediator.When(m => m.Send(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()))
                .Do(callinfo => sut.SimulateError(callinfo.Arg<TestCommand>().CorrelationId, errorInfo));

            // Act
            var result = await sut.InvokeSendAndWaitForCompletion(command, info =>
            {
                capturedErrorInfo = info;
                return new SaveErrorResult("custom error");
            }, TestContext.Current.CancellationToken);

            // Assert
            Assert.IsType<SaveErrorResult>(result);
            Assert.Equal(errorInfo, capturedErrorInfo);
        }
    }

    public class SendAndWaitForCompletionAfterwards
    {
        [Fact]
        public async Task Invokes_afterSend_and_returns_SaveSuccessResult_when_backend_signals_success()
        {
            // Arrange
            var command = new TestCommand();
            var mediator = Substitute.For<IUiMediator>();
            using var sut = new TestableHandler(mediator);
            var afterSendInvoked = false;

            mediator.When(m => m.Send(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()))
                .Do(callinfo => sut.SimulateSuccess(callinfo.Arg<TestCommand>().CorrelationId));

            // Act
            var result = await sut.InvokeSendAndWaitForCompletionAfterwards(command, ct =>
            {
                afterSendInvoked = true;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            // Assert
            Assert.IsType<SaveSuccessResult>(result);
            Assert.True(afterSendInvoked);
        }

        [Fact]
        public async Task Returns_SaveErrorResult_when_afterSend_throws()
        {
            // Arrange
            var command = new TestCommand();
            var mediator = Substitute.For<IUiMediator>();
            using var sut = new TestableHandler(mediator);

            mediator.When(m => m.Send(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()))
                .Do(callinfo => sut.SimulateSuccess(callinfo.Arg<TestCommand>().CorrelationId));

            // Act
            var result = await sut.InvokeSendAndWaitForCompletionAfterwards(command,
                _ => throw new InvalidOperationException("afterSend failed"),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.IsType<SaveErrorResult>(result);
        }
    }
}
