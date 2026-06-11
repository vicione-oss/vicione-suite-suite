using Sdk.Client.ControlPanels.Models;

namespace Blazor.Shared.UserInterface.ControlPanels;

internal class UserInterfaceSaveResult
{
    public ISaveResult? ErrorSaveResult { get; set; }
    public string?  CultureName { get; set; }
    public string?  TimeZoneId { get; set; }
}
