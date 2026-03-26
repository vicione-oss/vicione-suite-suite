using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.Services;
using Core.Shared.HostManagement.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.MessageBanner.Components;

public sealed class SystemRestartObserver : ComponentBase, IDisposable,
    IEventConsumer<SystemRestartRequired>
{
    private IDisposable? _disposable;

    [Inject] public required IUiMediator Mediator { get; set; }
    [Inject] public required IMessageBannerMediator MessageBannerMediator { get; set; }

    protected override void OnInitialized()
    {
        _disposable = Mediator.Register(this);
    }

    public void Dispose()
    {
        _disposable?.Dispose();

        GC.SuppressFinalize(this);
    }

    public Task Consume(ClientContext<SystemRestartRequired> context, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Type = MessageType.Warning,
            Icon = MonochromeIconName.WarningLight,
        };

        switch (context.Message.Reason)
        {
            case RestartReason.SystemConfiguration:
                message.Title = Localization.MessageBanner.SystemRestartRequiredHeader;
                message.Description = Localization.MessageBanner.SystemRestartRequired;
                break;

            case RestartReason.ModuleConfiguration:
                message.Title = Localization.MessageBanner.SuiteRestartRequiredHeader;
                message.Description = Localization.MessageBanner.SuiteRestartRequired;
                break;

            default:
                return Task.CompletedTask;
        }

        MessageBannerMediator.ShowMessageBanner(message);
        return Task.CompletedTask;
    }
}
