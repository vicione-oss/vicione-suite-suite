using Blazor.Shared.Settings.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Settings.Models;

public sealed class ControlPanelEditTests
{
    private const int LongerRunningDelayMs = 5000;

    private static readonly Type HandlerServiceKey = typeof(ControlPanelServiceKey<TestControlPanel>);

    private sealed class TestControlPanel : ControlPanelBase<ControlPanelState>;

    private sealed class OtherTestControlPanel : ControlPanelBase<ControlPanelState>;

    private sealed class TestResetHandler : IControlPanelResetHandler<ControlPanelState>
    {
        public Task Reset(ControlPanelState state, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task Should_tell_the_shared_reset_handler_by_which_it_resets_together_with_other_edits()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControlPanel<SharedClientModule, TestControlPanel, ControlPanelState>()
            .WithSharedHandlers(handlers => handlers.WithResetHandler<TestResetHandler>(), out _);

        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(new ControlPanelState(), serviceProvider, HandlerServiceKey);

        // Assert
        controlPanelEdit.SharedResetGroup.Should().Be(new SharedResetGroup(HandlerServiceKey, typeof(TestResetHandler)));
    }

    [Fact]
    public async Task Should_not_tell_a_shared_reset_handler_for_a_reset_handler_of_its_own()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedScoped<IControlPanelResetHandler<ControlPanelState>, TestResetHandler>(HandlerServiceKey);

        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(new ControlPanelState(), serviceProvider, HandlerServiceKey);

        // Assert
        controlPanelEdit.SharedResetGroup.Should().BeNull();
    }

    [Fact]
    public async Task Should_invoke_save_handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var saveHandler = Substitute.For<IControlPanelSaveHandler<ControlPanelState>>();
        services.AddKeyedScoped(HandlerServiceKey, (_, _) => saveHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);
        controlPanelEdit.Begin();

        // Act
        await controlPanelEdit.Save();

        // Assert
        await saveHandler.Received(1).Save(controlPanelState, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_return_canceled_result_and_keep_running_when_save_is_canceled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var saveHandler = Substitute.For<IControlPanelSaveHandler<ControlPanelState>>();
        saveHandler.Save(Arg.Any<ControlPanelState>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        services.AddKeyedScoped(HandlerServiceKey, (_, _) => saveHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(new ControlPanelState(), serviceProvider, HandlerServiceKey);
        controlPanelEdit.Begin();

        // Act
        var saveResult = await controlPanelEdit.Save();

        // Assert
        saveResult.Should().BeOfType<SaveCanceledResult>();
        controlPanelEdit.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task Should_return_canceled_result_when_saving_a_disposed_edit()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedScoped(HandlerServiceKey, (_, _) => Substitute.For<IControlPanelSaveHandler<ControlPanelState>>());

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(new ControlPanelState(), serviceProvider, HandlerServiceKey);
        controlPanelEdit.Begin();
        await controlPanelEdit.DisposeAsync();

        // Act
        var saveResult = await controlPanelEdit.Save();

        // Assert
        saveResult.Should().BeOfType<SaveCanceledResult>();
    }

    [Fact]
    public async Task Should_invoke_cancel_handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var cancelHandler = Substitute.For<IControlPanelCancelHandler<ControlPanelState>>();
        services.AddKeyedScoped(HandlerServiceKey, (_, _) => cancelHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);
        controlPanelEdit.Begin();

        // Act
        await controlPanelEdit.Cancel();

        // Assert
        await cancelHandler.Received(1).Cancel(controlPanelState, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_invoke_reset_handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var resetHandler = Substitute.For<IControlPanelResetHandler<ControlPanelState>>();
        services.AddKeyedScoped(HandlerServiceKey, (_, _) => resetHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);

        // Act
        await controlPanelEdit.Reset();

        // Assert
        await resetHandler.Received(1).Reset(controlPanelState, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_invoke_handlers_of_another_control_panel_of_the_same_state()
    {
        // Arrange
        var otherHandlerServiceKey = typeof(ControlPanelServiceKey<OtherTestControlPanel>);

        var otherSaveHandler = Substitute.For<IControlPanelSaveHandler<ControlPanelState>>();
        var otherCancelHandler = Substitute.For<IControlPanelCancelHandler<ControlPanelState>>();
        var otherResetHandler = Substitute.For<IControlPanelResetHandler<ControlPanelState>>();

        var saveHandlerWithoutKey = Substitute.For<IControlPanelSaveHandler<ControlPanelState>>();
        var cancelHandlerWithoutKey = Substitute.For<IControlPanelCancelHandler<ControlPanelState>>();
        var resetHandlerWithoutKey = Substitute.For<IControlPanelResetHandler<ControlPanelState>>();

        var services = new ServiceCollection()
            .AddLogging()
            .AddKeyedScoped(otherHandlerServiceKey, (_, _) => otherSaveHandler)
            .AddKeyedScoped(otherHandlerServiceKey, (_, _) => otherCancelHandler)
            .AddKeyedScoped(otherHandlerServiceKey, (_, _) => otherResetHandler)
            .AddScoped(_ => saveHandlerWithoutKey)
            .AddScoped(_ => cancelHandlerWithoutKey)
            .AddScoped(_ => resetHandlerWithoutKey);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);

        // Act
        await controlPanelEdit.Reset();

        controlPanelEdit.Begin();
        var saveResult = await controlPanelEdit.Save();

        await controlPanelEdit.Cancel();

        // Assert
        saveResult.Should().BeOfType<SaveErrorResult>();

        otherResetHandler.ReceivedCalls().Should().BeEmpty();
        otherSaveHandler.ReceivedCalls().Should().BeEmpty();
        otherCancelHandler.ReceivedCalls().Should().BeEmpty();

        resetHandlerWithoutKey.ReceivedCalls().Should().BeEmpty();
        saveHandlerWithoutKey.ReceivedCalls().Should().BeEmpty();
        cancelHandlerWithoutKey.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_cancel_save_handler_on_dispose()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddKeyedScoped<IControlPanelSaveHandler<ControlPanelState>, LongerRunningControlPanelSaveHandler>(HandlerServiceKey);

        await using var serviceProvider = services.BuildServiceProvider();

        var saveHandler = (LongerRunningControlPanelSaveHandler)serviceProvider.GetRequiredKeyedService<IControlPanelSaveHandler<ControlPanelState>>(HandlerServiceKey);
        var controlPanelState = new ControlPanelState();

        var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);
        controlPanelEdit.Begin();

        var saveTask = controlPanelEdit.Save();
        var disposeAsyncTask = controlPanelEdit.DisposeAsync().AsTask();

        // Act
        await Task.WhenAll(saveTask, disposeAsyncTask);

        // Assert
        saveHandler.OperationCanceledExceptionOccurred.Should().BeTrue();
    }

    [Fact]
    public async Task Should_cancel_cancel_handler_on_dispose()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddKeyedScoped<IControlPanelCancelHandler<ControlPanelState>, LongerRunningControlPanelCancelHandler>(HandlerServiceKey);

        await using var serviceProvider = services.BuildServiceProvider();

        var cancelHandler = (LongerRunningControlPanelCancelHandler)serviceProvider.GetRequiredKeyedService<IControlPanelCancelHandler<ControlPanelState>>(HandlerServiceKey);
        var controlPanelState = new ControlPanelState();

        var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);
        controlPanelEdit.Begin();

        var cancelTask = controlPanelEdit.Cancel();
        var disposeAsyncTask = controlPanelEdit.DisposeAsync().AsTask();

        // Act
        await Task.WhenAll(cancelTask, disposeAsyncTask);

        // Assert
        cancelHandler.OperationCanceledExceptionOccurred.Should().BeTrue();
    }

    [Fact]
    public async Task Should_cancel_reset_handler_on_dispose()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddKeyedScoped<IControlPanelResetHandler<ControlPanelState>, LongerRunningControlPanelResetHandler>(HandlerServiceKey);

        await using var serviceProvider = services.BuildServiceProvider();

        var resetHandler = (LongerRunningControlPanelResetHandler)serviceProvider.GetRequiredKeyedService<IControlPanelResetHandler<ControlPanelState>>(HandlerServiceKey);
        var controlPanelState = new ControlPanelState();
        var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);

        var resetTask = controlPanelEdit.Reset();
        var disposeAsyncTask = controlPanelEdit.DisposeAsync().AsTask();

        // Act
        await Task.WhenAll(resetTask, disposeAsyncTask);

        // Assert
        resetHandler.OperationCanceledExceptionOccurred.Should().BeTrue();
    }

    public enum OperationKind
    {
        LongerRunning,
        Substitute,
        None
    }

    [Theory]
    [InlineData(OperationKind.LongerRunning, OperationKind.Substitute, OperationKind.None)]
    [InlineData(OperationKind.LongerRunning, OperationKind.None, OperationKind.Substitute)]
    [InlineData(OperationKind.None, OperationKind.LongerRunning, OperationKind.Substitute)]
    [InlineData(OperationKind.None, OperationKind.Substitute, OperationKind.LongerRunning)]
    [InlineData(OperationKind.Substitute, OperationKind.None, OperationKind.LongerRunning)]
    [InlineData(OperationKind.Substitute, OperationKind.LongerRunning, OperationKind.None)]
    public async Task Should_cancel_another_running_operation_when_executing_an_operation(
        OperationKind resetOperationKind, OperationKind saveOperationKind, OperationKind cancelOperationKind)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging();

        if (resetOperationKind == OperationKind.LongerRunning)
            services.AddKeyedScoped<IControlPanelResetHandler<ControlPanelState>, LongerRunningControlPanelResetHandler>(HandlerServiceKey);
        else if (resetOperationKind == OperationKind.Substitute)
            services.AddKeyedScoped(HandlerServiceKey, (_, _) => Substitute.For<IControlPanelResetHandler<ControlPanelState>>());

        if (saveOperationKind == OperationKind.LongerRunning)
            services.AddKeyedScoped<IControlPanelSaveHandler<ControlPanelState>, LongerRunningControlPanelSaveHandler>(HandlerServiceKey);
        else if (saveOperationKind == OperationKind.Substitute)
            services.AddKeyedScoped(HandlerServiceKey, (_, _) => Substitute.For<IControlPanelSaveHandler<ControlPanelState>>());

        if (cancelOperationKind == OperationKind.LongerRunning)
            services.AddKeyedScoped<IControlPanelCancelHandler<ControlPanelState>, LongerRunningControlPanelCancelHandler>(HandlerServiceKey);
        else if (cancelOperationKind == OperationKind.Substitute)
            services.AddKeyedScoped(HandlerServiceKey, (_, _) => Substitute.For<IControlPanelCancelHandler<ControlPanelState>>());

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider, HandlerServiceKey);
        controlPanelEdit.Begin();

        ILongerRunningControlPanelHandler? longerRunningHandler = null;
        var controlPanelEditTasks = new List<Task>();

        if (resetOperationKind == OperationKind.LongerRunning)
        {
            longerRunningHandler = serviceProvider.GetRequiredKeyedService<IControlPanelResetHandler<ControlPanelState>>(HandlerServiceKey)
                as ILongerRunningControlPanelHandler;

            controlPanelEditTasks.Add(controlPanelEdit.Reset());
        }

        if (saveOperationKind == OperationKind.LongerRunning)
        {
            longerRunningHandler = serviceProvider.GetRequiredKeyedService<IControlPanelSaveHandler<ControlPanelState>>(HandlerServiceKey)
                as ILongerRunningControlPanelHandler;

            controlPanelEditTasks.Add(controlPanelEdit.Save());
        }

        if (cancelOperationKind == OperationKind.LongerRunning)
        {
            longerRunningHandler = serviceProvider.GetRequiredKeyedService<IControlPanelCancelHandler<ControlPanelState>>(HandlerServiceKey)
                as ILongerRunningControlPanelHandler;

            controlPanelEditTasks.Add(controlPanelEdit.Cancel());
        }

        if (resetOperationKind == OperationKind.Substitute)
            controlPanelEditTasks.Add(controlPanelEdit.Reset());

        if (saveOperationKind == OperationKind.Substitute)
            controlPanelEditTasks.Add(controlPanelEdit.Save());

        if (cancelOperationKind == OperationKind.Substitute)
            controlPanelEditTasks.Add(controlPanelEdit.Cancel());

        // Act
        await Task.WhenAll(controlPanelEditTasks);

        // Assert
        longerRunningHandler.Should().NotBeNull();
        longerRunningHandler.OperationCanceledExceptionOccurred.Should().BeTrue();
    }


    private interface ILongerRunningControlPanelHandler
    {
        bool OperationCanceledExceptionOccurred { get; }
    }

    private sealed class LongerRunningControlPanelSaveHandler : IControlPanelSaveHandler<ControlPanelState>,
        ILongerRunningControlPanelHandler
    {
        public bool OperationCanceledExceptionOccurred { get; private set; }

        public async Task<ISaveResult> Save(ControlPanelState state, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(LongerRunningDelayMs, cancellationToken);

                return new SaveSuccessResult();
            }
            catch (OperationCanceledException e)
            {
                OperationCanceledExceptionOccurred = true;

                return new SaveErrorResult(e.Message);
            }
        }
    }

    private sealed class LongerRunningControlPanelCancelHandler : IControlPanelCancelHandler<ControlPanelState>,
        ILongerRunningControlPanelHandler
    {
        public bool OperationCanceledExceptionOccurred { get; private set; }

        public async Task Cancel(ControlPanelState state, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(LongerRunningDelayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                OperationCanceledExceptionOccurred = true;
            }
        }
    }

    private sealed class LongerRunningControlPanelResetHandler : IControlPanelResetHandler<ControlPanelState>,
        ILongerRunningControlPanelHandler
    {
        public bool OperationCanceledExceptionOccurred { get; private set; }

        public async Task Reset(ControlPanelState state, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(LongerRunningDelayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                OperationCanceledExceptionOccurred = true;
            }
        }
    }
}
