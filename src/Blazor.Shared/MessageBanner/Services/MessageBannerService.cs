using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.Models;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.Services;

internal sealed class MessageBannerService : IMessageBannerService, IDisposable
{
    private readonly IMessageBannerMediator _messageBannerMediator;

    public event Action? MessageBannerClosed;

    public MessageBannerService(IMessageBannerMediator messageBannerMediator)
    {
        _messageBannerMediator = messageBannerMediator;
        _messageBannerMediator.MessageBannerClosed += MessageBannerMediator_MessageBannerClosed;
    }

    public void Dispose()
    {
        _messageBannerMediator.MessageBannerClosed -= MessageBannerMediator_MessageBannerClosed;

        GC.SuppressFinalize(this);
    }

    public void ShowMessageBanner(MessageType type, string? description)
    {
        var message = new Message
        {
            Type = type,
            Icon = type.ToIcon(),
            Title = type.ToTitle(),
            Description = description
        };

        _messageBannerMediator.ShowMessageBanner(message);
    }

    public void CloseMessageBanner()
        => _messageBannerMediator.CloseMessageBanner();

    private void MessageBannerMediator_MessageBannerClosed() => MessageBannerClosed?.Invoke();
}
