using Blazor.Shared.SystemInformation.Models;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Extensions;

public static class SuiteProcessRoleExtensions
{
    extension(SuiteProcessRole role)
    {
        /// <summary>
        /// <c>null</c> for processes that do not belong to the suite.
        /// </summary>
        public string? DisplayName => role switch
        {
            SuiteProcessRole.Suite => Text.RoleSuite,
            SuiteProcessRole.EngineHost => Text.RoleEngineHost,
            _ => null,
        };
    }
}
