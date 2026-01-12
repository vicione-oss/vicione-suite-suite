using AwesomeAssertions;
using Blazor.Shared.Settings.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Settings.Models;

public sealed class ControlPanelEditTests
{
    private const int LongerRunningDelayMs = 5000;

    [Fact]
    public async Task Should_invoke_save_handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var saveHandler = Substitute.For<IControlPanelSaveHandler<ControlPanelState>>();
        services.AddScoped(_ => saveHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider);
        controlPanelEdit.Begin();

        // Act
        await controlPanelEdit.Save();

        // Assert
        await saveHandler.Received(1).Save(controlPanelState, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_invoke_cancel_handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var cancelHandler = Substitute.For<IControlPanelCancelHandler<ControlPanelState>>();
        services.AddScoped(_ => cancelHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider);
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
        services.AddScoped(_ => resetHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider);

        // Act
        await controlPanelEdit.Reset();

        // Assert
        await resetHandler.Received(1).Reset(controlPanelState, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispose_should_result_in_cancellation_of_save_handler()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<IControlPanelSaveHandler<ControlPanelState>, LongerRunningControlPanelSaveHandler>();

        await using var serviceProvider = services.BuildServiceProvider();

        var saveHandler = (LongerRunningControlPanelSaveHandler)serviceProvider.GetRequiredService<IControlPanelSaveHandler<ControlPanelState>>();
        var controlPanelState = new ControlPanelState();

        var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider);
        controlPanelEdit.Begin();

        var saveTask = controlPanelEdit.Save();
        var disposeAsyncTask = controlPanelEdit.DisposeAsync().AsTask();

        // Act
        await Task.WhenAll(saveTask, disposeAsyncTask);

        // Assert
        saveHandler.OperationCanceledExceptionOccurred.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_should_result_in_cancellation_of_cancel_handler()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<IControlPanelCancelHandler<ControlPanelState>, LongerRunningControlPanelCancelHandler>();

        await using var serviceProvider = services.BuildServiceProvider();

        var cancelHandler = (LongerRunningControlPanelCancelHandler)serviceProvider.GetRequiredService<IControlPanelCancelHandler<ControlPanelState>>();
        var controlPanelState = new ControlPanelState();

        var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider);
        controlPanelEdit.Begin();

        var cancelTask = controlPanelEdit.Cancel();
        var disposeAsyncTask = controlPanelEdit.DisposeAsync().AsTask();

        // Act
        await Task.WhenAll(cancelTask, disposeAsyncTask);

        // Assert
        cancelHandler.OperationCanceledExceptionOccurred.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_should_result_in_cancellation_of_reset_handler()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<IControlPanelResetHandler<ControlPanelState>, LongerRunningControlPanelResetHandler>();

        await using var serviceProvider = services.BuildServiceProvider();

        var resetHandler = (LongerRunningControlPanelResetHandler)serviceProvider.GetRequiredService<IControlPanelResetHandler<ControlPanelState>>();
        var controlPanelState = new ControlPanelState();
        var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider);

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
    public async Task Executing_an_operation_should_result_in_cancellation_of_another_running_operation(
        OperationKind resetOperationKind, OperationKind saveOperationKind, OperationKind cancelOperationKind)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging();

        if (resetOperationKind == OperationKind.LongerRunning)
            services.AddScoped<IControlPanelResetHandler<ControlPanelState>, LongerRunningControlPanelResetHandler>();
        else if (resetOperationKind == OperationKind.Substitute)
            services.AddScoped(_ => Substitute.For<IControlPanelResetHandler<ControlPanelState>>());

        if (saveOperationKind == OperationKind.LongerRunning)
            services.AddScoped<IControlPanelSaveHandler<ControlPanelState>, LongerRunningControlPanelSaveHandler>();
        else if (saveOperationKind == OperationKind.Substitute)
            services.AddScoped(_ => Substitute.For<IControlPanelSaveHandler<ControlPanelState>>());

        if (cancelOperationKind == OperationKind.LongerRunning)
            services.AddScoped<IControlPanelCancelHandler<ControlPanelState>, LongerRunningControlPanelCancelHandler>();
        else if (cancelOperationKind == OperationKind.Substitute)
            services.AddScoped(_ => Substitute.For<IControlPanelCancelHandler<ControlPanelState>>());

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelState = new ControlPanelState();

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, serviceProvider);
        controlPanelEdit.Begin();

        ILongerRunningControlPanelHandler? longerRunningHandler = null;
        var controlPanelEditTasks = new List<Task>();

        if (resetOperationKind == OperationKind.LongerRunning)
        {
            longerRunningHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ControlPanelState>>()
                as ILongerRunningControlPanelHandler;

            controlPanelEditTasks.Add(controlPanelEdit.Reset());
        }

        if (saveOperationKind == OperationKind.LongerRunning)
        {
            longerRunningHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ControlPanelState>>()
                as ILongerRunningControlPanelHandler;

            controlPanelEditTasks.Add(controlPanelEdit.Save());
        }

        if (cancelOperationKind == OperationKind.LongerRunning)
        {
            longerRunningHandler = serviceProvider.GetRequiredService<IControlPanelCancelHandler<ControlPanelState>>()
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
