using System.Collections.Concurrent;
using System.Globalization;
using Blazor.Shared.Settings.DateAndTime.Helpers;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Models;
using TimeZoneConverter;
using TimeZoneNames;

namespace Blazor.Shared.Settings.DateAndTime.Services;

internal sealed class TimeZoneDescriptorProvider : ITimeZoneDescriptorProvider
{
    private readonly ConcurrentDictionary<string, TimeZoneDescriptor> _timeZoneDescriptorMap = [];

    private IDictionary<string, string>? _displayNames;
    private bool _allTimeZoneDescriptorsCached;

    private IDictionary<string, string> DisplayNames => _displayNames ??= TZNames.GetDisplayNames(
        CultureInfo.CurrentUICulture.Name, useIanaZoneIds: true);

    public Task<TimeZoneDescriptor?> GetTimeZoneDescriptor(string timeZoneId, CancellationToken cancellationToken = default)
    {
        if (_timeZoneDescriptorMap.TryGetValue(timeZoneId, out var timeZoneDescriptor))
            return Task.FromResult<TimeZoneDescriptor?>(timeZoneDescriptor);

        return Task.Run<TimeZoneDescriptor?>(() =>
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZoneInfo))
                return null;

            if (!HasKnownId(timeZoneInfo))
                return null;

            var newDescriptor = CreateTimeZoneDescriptor(timeZoneInfo);

            _timeZoneDescriptorMap.TryAdd(timeZoneId, newDescriptor);

            return newDescriptor;
        }, cancellationToken);
    }

    public Task<IEnumerable<TimeZoneDescriptor>> GetAll(CancellationToken cancellationToken = default)
    {
        if (_allTimeZoneDescriptorsCached)
            return Task.FromResult<IEnumerable<TimeZoneDescriptor>>(_timeZoneDescriptorMap.Values);

        return Task.Run<IEnumerable<TimeZoneDescriptor>>(() =>
        {
            var timeZoneDescriptors = TimeZoneInfo.GetSystemTimeZones()
                .Where(HasKnownId)
                .Select(CreateTimeZoneDescriptor);

            foreach (var timeZoneDescriptor in timeZoneDescriptors)
                _timeZoneDescriptorMap.TryAdd(timeZoneDescriptor.TimeZoneId, timeZoneDescriptor);

            _allTimeZoneDescriptorsCached = true;

            return _timeZoneDescriptorMap.Values;
        }, cancellationToken);
    }

    /// <summary>
    /// Predicate to filter time zone IDs that can be used with the TimeZoneNames package
    /// </summary>
    /// <remarks>
    /// We need to filter time zones to ensure display name is always localized.
    ///
    /// A disadvantage of this approach is that the provider will not return all
    /// .NET time zones available, but it will cover at least the well known time zones.
    /// </remarks>
    private bool HasKnownId(TimeZoneInfo timeZoneInfo)
        => DisplayNames.ContainsKey(timeZoneInfo.Id) || TZConvert.TryWindowsToIana(timeZoneInfo.Id, out _);

    private TimeZoneDescriptor CreateTimeZoneDescriptor(TimeZoneInfo timeZoneInfo)
    {
        if (!DisplayNames.TryGetValue(timeZoneInfo.Id, out var displayName))
        {
            var ianaTimeZoneId = TZConvert.WindowsToIana(timeZoneInfo.Id);

            displayName = DisplayNames[ianaTimeZoneId];
        }

        return new()
        {
            TimeZoneId = timeZoneInfo.Id,
            DisplayName = displayName,
            BaseUtcOffset = timeZoneInfo.BaseUtcOffset,
            ImageUrl = GetTimeZoneImageSrc(timeZoneInfo)
        };
    }

    private static Uri GetTimeZoneImageSrc(TimeZoneInfo timeZoneInfo)
    {
        var filename = $"timezone_{timeZoneInfo.BaseUtcOffset.TotalHours.ToString("0.##", CultureInfo.InvariantCulture)}.png";

        return TimeZoneImageHelper.GetTimeZoneImageUri(filename);
    }
}
