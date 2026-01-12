using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.Services;

namespace Blazor.Shared.Tests.MessageBanner;

internal sealed class TestMessageBannerMediator : IMessageBannerMediator
{
    public event Action<IMessage>? MessageBannerShown;
    public event Action? MessageBannerClosed;

    public void CloseMessageBanner() => MessageBannerClosed?.Invoke();
    public void MinimizeMessageBanner() { }
    public void RestoreMessageBanner() { }
    public void ShowMessageBanner(IMessage message) => MessageBannerShown?.Invoke(message);
}
