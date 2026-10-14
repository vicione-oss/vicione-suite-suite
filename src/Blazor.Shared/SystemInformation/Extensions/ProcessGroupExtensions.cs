using Blazor.Shared.SystemInformation.Models;
using ViciOne.Ui.Localization.Resources;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Extensions;

public static class ProcessGroupExtensions
{
    extension(ProcessGroup group)
    {
        public string DisplayName => group switch
        {
            ProcessGroup.Suite => TechnicalTerms.Suite,
            ProcessGroup.System => CommonVocabulary.System,
            ProcessGroup.Kernel => Text.GroupKernel,
            _ => Text.GroupOther,
        };
    }
}
