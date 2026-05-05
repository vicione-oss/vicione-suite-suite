using Blazor.Shared.Components.FileDropZone;
using Blazor.Shared.Settings.Models;
using Core.Shared.Instance.Models;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Extensions;
using Sdk.Client.Services;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsFieldFileUpload : ComponentBase, IAsyncDisposable
{
    private readonly string _chooseButtonIconCssClasses = MonochromeIconName.Folder.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _cancelButtonIconCssClasses = MonochromeIconName.CloseMedium.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SettingsFieldFileUpload>? _dotNetObjectReference;
    private Task? _attachJsTask;
    private IJSObjectReference? _jsObjectReference;

    private bool _disposedAsync;

    private FileDropZone? _fileDropZone;
    private InputFile? _inputFile;
    private InputFile? _inputFileForShowPicker;

    private string? _placeholder;

    private string? _filename;
    private IStreamUploadHandler? _uploadHandler;
    private IStreamUploadResult? _uploadResult;
    private int? _uploadProgress;
    private IUploadTicket? _uploadTicket;
    private readonly SemaphoreSlim _uploadTicketSemaphore = new(1);
    private bool _shouldRender = true;

    private bool _dropIncoming;

    private string ChooseButtonText => _uploadTicket is not null && _uploadProgress is not null
        ? CommonVocabulary.Cancel : CommonVocabulary.Choose;

    private string ChooseButtonIcon => _uploadTicket is not null && _uploadProgress is not null
        ? _cancelButtonIconCssClasses : _chooseButtonIconCssClasses;

    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    [Inject] private ILogger<FileDropZone> Logger { get; set; } = default!;

    /// <summary>
    /// Value rendered into <see href="https://html.spec.whatwg.org/#attr-input-accept">accept</see> attribute of internal input fields
    /// </summary>
    [Parameter]
    public string? Accept { get; set; }

    [Parameter]
    public long MaximumAllowedSize { get; set; } = 500 * 1024;

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter, EditorRequired]
    public string? Filename { get; set; }

    [Parameter]
    public EventCallback<string?> FilenameChanged { get; set; }

    [Parameter, EditorRequired]
    public IUploadTicket? UploadTicket { get; set; }

    [Parameter, EditorRequired]
    public IStreamUploadHandler UploadHandler { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public EventCallback<IUploadTicket> OnUploadStart { get; set; }

    [Parameter]
    public EventCallback<StreamUploadSuccessResult> OnUploadSuccess { get; set; }

    [Parameter]
    public EventCallback<StreamUploadErrorResult> OnUploadError { get; set; }

    [Parameter]
    public EventCallback OnUploadCancel { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var uploadTicket = _uploadTicket;
        var cancelUpload = false;

        if (Placeholder != _placeholder)
        {
            _placeholder = Placeholder;

            _shouldRender = true;
        }

        if (Filename != _filename)
        {
            _filename = Filename;

            cancelUpload = true;
        }

        await _uploadTicketSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            if (UploadTicket != _uploadTicket)
            {
                _uploadTicket = UploadTicket;

                cancelUpload = true;
            }
        }
        finally
        {
            _uploadTicketSemaphore.Release();
        }

        if (UploadHandler != _uploadHandler)
        {
            if (_uploadHandler is not null)
                _uploadHandler.OnProgress -= UploadHandlerProgress;

            _uploadHandler = UploadHandler;
            _uploadHandler.OnProgress += UploadHandlerProgress;

            cancelUpload = true;
        }

        if (cancelUpload)
        {
            uploadTicket?.Cancel();

            _uploadProgress = null;
            _uploadResult = null;

            _shouldRender = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        if (_uploadHandler is not null)
            _uploadHandler.OnProgress -= UploadHandlerProgress;

        _fileDropZone?.SetInputFileElementReference(null);

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await _uploadTicketSemaphore.WaitAsync();
        try
        {
            if (_uploadTicket is IDisposable disposableUploadTicket)
                disposableUploadTicket.Dispose();
        }
        finally
        {
            _uploadTicketSemaphore.Release();
        }

        _uploadTicketSemaphore.Dispose();

        await RemoveJsAsync();

        if (_dotNetObjectReference is not null)
        {
            _dotNetObjectReference.Dispose();
            _dotNetObjectReference = null;
        }

        await _jsModule.TryDisposeAsync(Logger);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_attachJsTask is null)
        {
            _attachJsTask = AttachJsAsync();

            await _attachJsTask;
        }

        _fileDropZone?.SetInputFileElementReference(_inputFile?.Element);
    }

    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;

            return true;
        }

        return false;
    }

    private async Task FileActionButtonClick()
    {
        // handle cancel upload if in progress, otherwise show file picker
        if (_uploadTicket is not null && _uploadProgress is not null)
        {
            try
            {
                _uploadTicket.Cancel();
                //_uploadTicket = null;
                _uploadProgress = null;
                _uploadResult = null;
                _filename = null;
                _shouldRender = true;
            }
            finally
            {
                _uploadTicketSemaphore.Release();
            }
            return;
        }

        // show file picker dialog via JS interop
        if (_jsObjectReference is not null)
        {
            try
            {
                await _jsObjectReference.InvokeVoidAsync("showFilePicker", _inputFileForShowPicker?.Element);
            }
            catch (JSDisconnectedException)
            {
                // https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability#javascript-interop-calls-without-a-circuit
            }
            catch (Exception exception)
            {
                JsShowFilePickerFailed(Logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = $"showFilePicker() failed")]
    private static partial void JsShowFilePickerFailed(ILogger logger, Exception ex);

    private async Task FileDropped(InputFileChangeEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(Accept))
        {
            var acceptTokens = Accept.Split(',');

            var fileExtension = Path.GetExtension(args.File.Name);

            if (!acceptTokens.Any(acceptToken => string.Equals(acceptToken, fileExtension, StringComparison.OrdinalIgnoreCase)))
                return;
        }

        await UploadFile(args.File);
    }

    private async Task FilePicked(InputFileChangeEventArgs args)
        => await UploadFile(args.File);

    private async Task UploadFile(IBrowserFile browserFile)
    {
        try
        {
            IUploadTicket? uploadTicket;

            await _uploadTicketSemaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                _uploadTicket?.Cancel();

                _uploadTicket = new UploadTicket();

                uploadTicket = _uploadTicket;
                _shouldRender = true;
            }
            finally
            {
                _uploadTicketSemaphore.Release();
            }

            if (OnUploadStart.HasDelegate)
                await OnUploadStart.InvokeAsync(uploadTicket);

            using var readStream = browserFile.OpenReadStream(MaximumAllowedSize, uploadTicket.CancellationToken);

            _filename = browserFile.Name;
            _uploadProgress = null;
            _uploadResult = null;
            _shouldRender = true;

            if (FilenameChanged.HasDelegate)
                await FilenameChanged.InvokeAsync(_filename);

            _uploadResult = await UploadHandler.Execute(readStream, _filename, uploadTicket.CancellationToken);

            if (_uploadResult is StreamUploadSuccessResult successResult)
            {
                if (OnUploadSuccess.HasDelegate)
                    await OnUploadSuccess.InvokeAsync(successResult);
            }
            else if (_uploadResult is StreamUploadErrorResult errorResult)
            {
                if (OnUploadError.HasDelegate)
                    await OnUploadError.InvokeAsync(errorResult);
            }
            else
            {
                throw new NotSupportedException("Result type is not supported");
            }

            _shouldRender = true;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            if (OnUploadCancel.HasDelegate)
                await OnUploadCancel.InvokeAsync();
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed, return gracefully
        }
        catch (Exception ex)
        {
            if (OnUploadError.HasDelegate)
                await OnUploadError.InvokeAsync(new StreamUploadErrorResult(ex.Message));

            _shouldRender = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task AttachJsAsync()
    {
        if (_jsObjectReference is not null)
            return;

        _jsModule ??= await JsInterop.IncludeModuleScript<SharedClientModule>("settings-field-file-upload.js");

        _dotNetObjectReference ??= DotNetObjectReference.Create(this);

        _jsObjectReference = await _jsModule!.InvokeConstructorAsync("SettingsFieldFileUpload");
    }

    private Task RemoveJsAsync()
        => DisposeJsAttachResultAsync();

    private async Task DisposeJsAttachResultAsync()
    {
        await _jsObjectReference.TryDisposeAsync(Logger);

        _jsObjectReference = null;
    }

    private async Task UploadHandlerProgress(IStreamUploadProgress progress)
    {
        var uploadProgress = _uploadProgress;

        _uploadProgress = (int)Math.Floor(progress.BytesUploaded * 100.0 / progress.BytesTotal);

        if (_uploadProgress != uploadProgress)
        {
            _shouldRender = true;

            await InvokeAsync(StateHasChanged);
        }
    }

    private void FileDropZoneDragEnter()
        => SetDropIncoming(true);

    private void FileDropZoneDragLeave()
        => SetDropIncoming(false);

    private void FileDropZoneDrop()
        => SetDropIncoming(false);

    private void SetDropIncoming(bool value)
    {
        if (_dropIncoming != value)
        {
            _dropIncoming = value;

            _shouldRender = true;

            InvokeAsync(StateHasChanged);
        }
    }
}
