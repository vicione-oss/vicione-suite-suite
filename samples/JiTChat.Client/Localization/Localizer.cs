using System.Diagnostics.CodeAnalysis;
using Sdk.Client.Modules.Localization;

namespace JiTChat.Client.Localization;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI container")]
internal sealed class Localizer : IClientModuleLocalizer<JiTChatClientModule>
{
    public string? GetDescription() => Common.ModuleDescription;
    public string GetTitle() => Common.ModuleTitle;
}
