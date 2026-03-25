using JiTChat.Client.Contracts;
using JiTChat.Public.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Authorization;
using Sdk.Client.Extensions;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace JiTChat.Client.NotificationArea;

public sealed partial class JiTChatNotificationElementFlyoutContent : ComponentBase, INotificationElementFlyoutContent, IAsyncDisposable
{
    private static readonly string Policy = ModulePolicyProvider.GetPolicy<JiTChatClientModule>();

    private static readonly ButtonSize SendButtonSize = ButtonSize.Small;

    private readonly string _sendButtonIconCssClass =
        MonochromeIconName.SendSolid.GetCssClasses(SendButtonSize.ToMonochromeIconSize()).ToSpaceSeparated();

    private IJSObjectReference? _jsModuleReference;

    [Inject]
    public IJiTChatService JiTChatService { get; set; } = default!;

    [Inject]
    public IJsInterop JsInterop { get; set; } = default!;

    [Inject]
    public ILogger<JiTChatNotificationElementFlyoutContent> Logger { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    [CascadingParameter]
    public required Task<AuthenticationState> AuthState { get; set; }
    
    internal string MessageText { get; set; } = string.Empty;
    private string Username { get; set; } = "Anonymous";

    protected override async Task OnInitializedAsync()
    {
        JiTChatService.OnMessagePublished += MessagePublished;
        JiTChatService.MarkIncomingMessagesAsRead = true;

        Username = (await AuthState).User.Identity?.Name ?? "Anonymous";

        _jsModuleReference = await JsInterop.IncludeModuleScript<JiTChatClientModule>("jit-chat-notification-element-flyout-content.js");
    }

    private void MessagePublished(ChatMessage arg)
    {
        if (arg.User == Username)
            MessageText = string.Empty;

        InvokeAsync(StateHasChanged);
        InvokeAsync(ScrollToBottom);
    }

    private async Task ScrollToBottom()
    {
        if (_jsModuleReference is not null)
        {
            var jitChat = await _jsModuleReference.InvokeConstructorAsync("JitChat");
            await jitChat.InvokeVoidAsync("scrollToBottom");
        }
    }

    private Task MessageTextEnterPressed()
        => SubmitMessage(MessageText);

    private Task SubmitButtonPressed()
        => SubmitMessage(MessageText);

    private async Task SubmitMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        await JiTChatService.SendMessage(Username, text);
    }

    private string OnDraw()
    {
        InvokeAsync(JiTChatService.MarkAllMessagesAsRead);
        InvokeAsync(ScrollToBottom);

        return string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        JiTChatService.MarkIncomingMessagesAsRead = false;

        await _jsModuleReference.TryDisposeAsync(Logger);
    }
}
