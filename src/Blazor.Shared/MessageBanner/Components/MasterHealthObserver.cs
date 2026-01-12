using Blazor.Shared.Enums;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using Sdk.Instance.HealthCheck.Events;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.Components;

public sealed class MasterHealthObserver : ComponentBase, IDisposable, IEventConsumer<MasterHealthInfoChanged>
{
    private Message? _message;
    private bool _canCloseDialog;
    private IDisposable? _disposable;
    private bool? _isReachable;

    [Inject] public required IUiMediator Mediator { get; set; }
    [Inject] public required IMessageBannerMediator MessageBannerMediator { get; set; }

    protected override void OnInitialized()
    {
        _disposable = Mediator.Register(this);

        MessageBannerMediator.MessageBannerShown += MessageBannerShown;
        MessageBannerMediator.MessageBannerClosed += MessageBannerClosed;
    }

    public void Dispose()
    {
        MessageBannerMediator.MessageBannerClosed -= MessageBannerClosed;
        MessageBannerMediator.MessageBannerShown -= MessageBannerShown;

        _disposable?.Dispose();

        GC.SuppressFinalize(this);
    }

    private void MessageBannerShown(IMessage message)
    {
        if (message != _message)
        {
            // dialog does not display own message anymore, therefore we should not close it when master is reachable again
            _canCloseDialog = false;
        }
    }

    private void MessageBannerClosed() => _canCloseDialog = false;

    public Task Consume(ClientContext<MasterHealthInfoChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.IsMasterReachable != _isReachable)
        {
            _isReachable = context.Message.IsMasterReachable;

            if (_isReachable == false)
            {
                _message ??= new Message();
                _message.Type = MessageType.Warning;
                _message.Icon = SvgIcon.Offline;
                _message.Title = MessageType.Warning.ToTitle();
                _message.Description = Localization.MessageBanner.MasterNotReachable;

                MessageBannerMediator.ShowMessageBanner(_message);

                _canCloseDialog = true;
            }
            else if (_canCloseDialog)
            {
                MessageBannerMediator.CloseMessageBanner();

                _canCloseDialog = false;
            }
        }

        return Task.CompletedTask;
    }
}
