using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Models;

namespace Blazor.Shared.Settings.DateAndTime.Services;

internal interface ITimeZoneDescriptorProvider
{
    Task<TimeZoneDescriptor?> GetTimeZoneDescriptor(string timeZoneId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TimeZoneDescriptor>> GetAll(CancellationToken cancellationToken = default);
}
