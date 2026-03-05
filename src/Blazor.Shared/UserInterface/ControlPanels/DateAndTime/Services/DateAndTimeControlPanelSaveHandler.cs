using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;

internal sealed class DateAndTimeControlPanelSaveHandler : ControlPanelSaveHandlerBase<DateAndTimeControlPanelState>,
            IEventConsumer<CrossInstanceConfigurationChanged>,
            IEventConsumer<CrossInstanceConfigurationError>
{
    private readonly ITimeZoneDescriptorProvider _timeZoneDescriptorProvider;

    public DateAndTimeControlPanelSaveHandler(ITimeZoneDescriptorProvider timeZoneDescriptorProvider, IUiMediator mediator) : base(mediator)
    {
        _timeZoneDescriptorProvider = timeZoneDescriptorProvider;

        Register<CrossInstanceConfigurationChanged>(this);
        Register<CrossInstanceConfigurationError>(this);
    }

    public override async Task<ISaveResult> Save(DateAndTimeControlPanelState state, CancellationToken cancellationToken)
    {
        var timeZoneDescriptor = await _timeZoneDescriptorProvider.GetTimeZoneDescriptor(state.SelectedTimeZoneId, cancellationToken);
        if (timeZoneDescriptor is null)
            return new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneDescriptor);

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneDescriptor.Value.TimeZoneId, out _))
            return new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneInfo);

        var command = new SetCrossInstanceConfiguration(null, state.SelectedTimeZoneId);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationChanged> context, CancellationToken cancellationToken)
    {
        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationError> context, CancellationToken cancellationToken = default)
    {
        CompleteWithError(context.Message.CorrelationId, context.Message.Error);

        return Task.CompletedTask;
    }
}
