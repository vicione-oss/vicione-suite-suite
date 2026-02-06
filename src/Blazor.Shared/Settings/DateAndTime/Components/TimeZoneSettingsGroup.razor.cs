using Blazor.Shared.Settings.DateAndTime.Helpers;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Settings.DateAndTime.Components;

public sealed partial class TimeZoneSettingsGroup : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Uri _timeZoneMapBackgroundImageSrc = TimeZoneImageHelper.GetTimeZoneImageUri("bg.png");

    private bool _disposed;

    private bool _shouldRender = true;

    private string? _selectedTimeZoneId;

    private bool _timeZoneSelectionLoading = true;
    private readonly List<TimeZoneDescriptor> _allTimeZoneDescriptors = [];
    private TimeZoneDescriptor? _selectedTimeZoneDescriptor;

    [Inject] private ITimeZoneDescriptorProvider TimeZoneDescriptorProvider { get; set; } = default!;

    [Parameter] public string SelectedTimeZoneId { get; set; } = Constants.DefaultTimeZoneId;
    [Parameter] public EventCallback<string> SelectedTimeZoneIdChanged { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        try
        {
            var allTimeZoneDescriptors = await TimeZoneDescriptorProvider.GetAll(_cancellationTokenSource.Token);
            _allTimeZoneDescriptors.AddRange(allTimeZoneDescriptors.OrderBy(Constants.OrderByKeySelector));

            _timeZoneSelectionLoading = false;

            _shouldRender = true;

        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (string.CompareOrdinal(SelectedTimeZoneId, _selectedTimeZoneId) != 0)
        {
            _selectedTimeZoneId = SelectedTimeZoneId;

            try
            {
                _selectedTimeZoneDescriptor = await TimeZoneDescriptorProvider.GetTimeZoneDescriptor(SelectedTimeZoneId,
                   _cancellationTokenSource.Token);

                _shouldRender = true;
            }
            catch (OperationCanceledException)
            {
                // Nothing to do here, return gracefully
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false))
            return;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
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

    private async Task NotifySelectedTimeZoneIdChanged()
    {
        if (SelectedTimeZoneIdChanged.HasDelegate)
            await SelectedTimeZoneIdChanged.InvokeAsync(SelectedTimeZoneId);
    }
}
