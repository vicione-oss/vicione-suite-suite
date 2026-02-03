using System.Text;
using Sdk.Instance;

namespace Blazor.Shared.SystemInformation.Extensions;

public static class IInstanceInformationProviderExtensions
{
    public static async Task<string> GetSystemInformationMarkdown(this IInstanceInformationProvider informationProvider)
    {
        const char nl = '\n'; // force LF for GitLab

        var systemTypeString = string.IsNullOrEmpty(informationProvider.Local.SystemType)
            ? string.Empty
            : $" ({informationProvider.Local.SystemType})";

        var modules = await informationProvider.GetInstalledModules();

        var sb = new StringBuilder(512);

        // Info table (no visible header, but valid GitLab table)
        sb.Append("|Field|Value|").Append(nl)
            .Append("|---|---|").Append(nl)
            .Append("| Suite | v").Append(EscapeMd(informationProvider.Local.Version)).Append(" |").Append(nl)
            .Append("| Type | ").Append(informationProvider.Local.Type.ToString()).Append(" |").Append(nl)
            .Append("| Sn. | ").Append(EscapeMd($"{informationProvider.Local.SerialNumber}{systemTypeString}")).Append(" |").Append(nl);

        sb.Append(nl).Append("**Modules**").Append(nl).Append(nl);

        if (modules.Count == 0)
        {
            sb.Append("_no modules loaded_").Append(nl);
        }
        else
        {
            sb.Append("|Name|Version|")
                .Append(nl)
                .Append("|---|---|")
                .Append(nl);

            foreach (var m in modules)
            {
                sb.Append("| ")
                    .Append(EscapeMd(m.Name))
                    .Append(" | v")
                    .Append(EscapeMd(m.Version))
                    .Append(" |")
                    .Append(nl);
            }
        }

        return sb.ToString();

        static string EscapeMd(string? value)
            => string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
