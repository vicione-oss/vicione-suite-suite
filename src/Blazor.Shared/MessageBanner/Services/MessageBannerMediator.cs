using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.NotificationArea;
using Sdk.Client.NotificationArea.Attributes;

namespace Blazor.Shared.MessageBanner.Services;

internal sealed class MessageBannerMediator(MessageBannerDialogState dialogState,
    [FromKeyedServices<SharedClientModule, MessageBannerNotificationElement>] MessageBannerNotificationElementState notificationElementState,
    MessageBannerNotificationElementIconState notificationElementIconState) : IMessageBannerMediator
{
    private readonly MessageBannerDialogState _dialogState = dialogState;
    private readonly MessageBannerNotificationElementState _notificationElementState = notificationElementState;
    private readonly MessageBannerNotificationElementIconState _notificationElementIconState = notificationElementIconState;
    private IMessage _message = new Message();

    public event Action<IMessage>? MessageBannerShown;
    public event Action? MessageBannerClosed;

    public void ShowMessageBanner(IMessage message)
    {
        _message = message;

        HideNotificationElement();
        ShowDialog(_message);

        MessageBannerShown?.Invoke(_message);
    }

    public void CloseMessageBanner()
    {
        HideDialog();
        HideNotificationElement();

        MessageBannerClosed?.Invoke();
    }

    public void RestoreMessageBanner()
    {
        HideNotificationElement();
        ShowDialog(_message);
    }

    public void MinimizeMessageBanner()
    {
        HideDialog();
        ShowNotificationElement();
    }

    private void ShowNotificationElement()
    {
        _notificationElementIconState.BeginUpdate();
        try
        {
            _notificationElementIconState.Icon = _message.Icon;
            _notificationElementIconState.MessageType = _message.Type;
        }
        finally
        {
            _notificationElementIconState.EndUpdate();
        }

        _notificationElementState.BeginUpdate();
        try
        {
            _notificationElementState.Title = _message.Title;
            _notificationElementState.Visible = true;
        }
        finally
        {
            _notificationElementState.EndUpdate();
        }
    }

    private void HideNotificationElement()
        => _notificationElementState.Visible = false;

    private void ShowDialog(IMessage message)
    {
        _dialogState.BeginUpdate();
        try
        {
            _dialogState.Message = message;
            _dialogState.Visible = true;
        }
        finally
        {
            _dialogState.EndUpdate();
        }
    }

    private void HideDialog()
        => _dialogState.Visible = false;
}
