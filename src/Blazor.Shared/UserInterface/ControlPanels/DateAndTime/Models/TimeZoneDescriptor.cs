namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Models;

public readonly record struct TimeZoneDescriptor(
    string TimeZoneId,
    string DisplayName,
    TimeSpan BaseUtcOffset,
    Uri ImageUrl);
