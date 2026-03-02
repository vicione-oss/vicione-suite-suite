using Blazor.Shared.Authorization.Extensions;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Extensions;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;

namespace Blazor.Shared.UserManagement.Components;

public sealed partial class AuthenticationCookieUpdater : ComponentBase, IAsyncDisposable, IEventConsumer<UserUpdatedEvent>
{
    private IDisposable? _userUpdatedEventRegistration;
    private IJSObjectReference? _jsModuleReference;
    private IJSObjectReference? _jsAttachResult;
    private bool _disposedAsync;

    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject] public required IUiMediator UiMediator { get; set; }
    [Inject] public required IJsInterop JsInterop { get; set; }
    [Inject] public required INonceStore NonceStore { get; set; }
    [Inject] public required ILogger<AuthenticationCookieUpdater> Logger { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _userUpdatedEventRegistration = UiMediator.Register(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        _userUpdatedEventRegistration?.Dispose();

        await DisposeJsInteropAsync().ConfigureAwait(false);
    }

    private async Task DisposeJsInteropAsync()
    {
        await _jsAttachResult.TryDisposeAsync(Logger);
        await _jsModuleReference.TryDisposeAsync(Logger);
    }

    public async Task Consume(ClientContext<UserUpdatedEvent> context, CancellationToken cancellationToken)
    {
        var user = await AuthenticationStateProvider.GetUser();
        var userProfile = context.Message.UserProfile;

        if (!user.IsAssociatedWith(userProfile))
            return;

        if (!userProfile.IsAuthorizationChanged(context.Message.UserProfileBefore))
            return;

        var nonce = await NonceStore.Create(cancellationToken);

        _jsModuleReference ??= await JsInterop.IncludeModuleScript<SharedClientModule>("authentication-cookie-updater.js", cancellationToken);
        if (_jsModuleReference is not null)
        {
            _jsAttachResult ??= await _jsModuleReference.InvokeAsync<IJSObjectReference?>("attach", cancellationToken);
            if (_jsAttachResult is not null)
            {
                var updateResult = await _jsAttachResult.InvokeAsync<bool>("updateAuthenticationCookie", nonce.Value);
                if (!updateResult)
                    RefreshingAuthenticationFailed(Logger);
            }
        }
        else
        {
            JSInteropInitializationFailed(Logger);
        }
    }

    [LoggerMessage(1, LogLevel.Error, "JSInterop initialization failed")]
    private static partial void JSInteropInitializationFailed(ILogger<AuthenticationCookieUpdater> logger);

    [LoggerMessage(2, LogLevel.Error, "Refreshing authentication failed")]
    private static partial void RefreshingAuthenticationFailed(ILogger<AuthenticationCookieUpdater> logger);

    [LoggerMessage(3, LogLevel.Error, "Disposing jsAttachResult failed")]
    private static partial void DisposingJsAttachResultFailed(ILogger<AuthenticationCookieUpdater> logger, Exception ex);
}
