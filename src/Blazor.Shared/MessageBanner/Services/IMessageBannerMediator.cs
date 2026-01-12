using Blazor.Shared.MessageBanner.Models;

namespace Blazor.Shared.MessageBanner.Services;

public interface IMessageBannerMediator
{
    event Action<IMessage>? MessageBannerShown;
    event Action? MessageBannerClosed;

    void ShowMessageBanner(IMessage message);
    void CloseMessageBanner();
    void RestoreMessageBanner();
    void MinimizeMessageBanner();
}
