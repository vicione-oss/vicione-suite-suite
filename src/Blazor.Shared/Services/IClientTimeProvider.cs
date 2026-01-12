namespace Blazor.Shared.Services;

public interface IClientTimeProvider
{
    TimeZoneInfo LocalTimeZone { get; }
    Task Initialize(CancellationToken cancellationToken = default);
}
