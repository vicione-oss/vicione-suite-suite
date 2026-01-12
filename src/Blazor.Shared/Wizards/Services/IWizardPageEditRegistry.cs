using Blazor.Shared.Wizards.Models;
using Sdk.Client.Services;

namespace Blazor.Shared.Wizards.Services;

internal interface IWizardPageEditRegistry : IRegistry<IWizardPageEdit>
{
    void Add(IWizardPageEdit item);
}
