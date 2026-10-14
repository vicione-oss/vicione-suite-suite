using Blazor.Shared.SystemInformation.Models;

namespace Blazor.Shared.SystemInformation.Extensions;

public static class OverviewStatusExtensions
{
    extension(OverviewStatus status)
    {
        /// <summary>
        /// The name in the <c>status-*</c> CSS classes of the process overview.
        /// </summary>
        public string CssName => status switch
        {
            OverviewStatus.Failed => "failed",
            OverviewStatus.Warning => "warning",
            OverviewStatus.Inactive => "neutral",
            _ => "normal",
        };
    }

    public static OverviewStatus StatusFor(double? value, double? threshold)
        => value > threshold ? OverviewStatus.Warning : OverviewStatus.Normal;
}
