using JiTChat.Client.Contracts;
using JiTChat.Public.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Authorization;
using Sdk.Client.Extensions;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.Services;

namespace JiTChat.Client.NotificationArea;

public sealed partial class JiTChatNotificationElementFlyoutContent : ComponentBase, INotificationElementFlyoutContent, IAsyncDisposable
{
    private EditContext? _editContext;
    private IJSObjectReference? _jsModuleReference;
    private static string Policy => ModulePolicyProvider.GetPolicy<JiTChatClientModule>();

    [Inject]
    public IJiTChatService JiTChatService { get; set; } = default!;

    [Inject]
    public IJsInterop JsInterop { get; set; } = default!;

    [Inject]
    public ILogger<JiTChatNotificationElementFlyoutContent> Logger { get; set; } = default!;

    [CascadingParameter]
    public required Task<AuthenticationState> AuthState { get; set; }

    public string MessageText { get; set; } = string.Empty;

    public string Username { get; private set; } = "Anonymous";

    protected override async Task OnInitializedAsync()
    {
        JiTChatService.OnMessagePublished += MessagePublished;
        JiTChatService.MarkIncomingMessagesAsRead = true;

        _editContext = new EditContext(this);
        Username = (await AuthState).User.Identity?.Name ?? "Anonymous";

        _jsModuleReference = await JsInterop.IncludeModuleScript<JiTChatClientModule>("jit-chat-notification-element-flyout-content.js");
    }

    private void MessagePublished(ChatMessage arg)
    {
        InvokeAsync(StateHasChanged);
        InvokeAsync(ScrollToBottom);
    }

    private async Task ScrollToBottom()
    {
        if (_jsModuleReference is not null)
        {
            var jitChat = await _jsModuleReference.InvokeAsync<IJSObjectReference?>("init");
            if (jitChat is not null)
                await jitChat.InvokeAsync<IJSObjectReference>("scrollToBottom");
        }
    }

    private async Task SubmitMessage()
    {
        await JiTChatService.SendMessage(Username, MessageText);
        MessageText = string.Empty;
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
