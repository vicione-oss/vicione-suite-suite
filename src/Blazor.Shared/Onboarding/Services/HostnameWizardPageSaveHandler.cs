using Blazor.Shared.Onboarding.Services.Validators;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed partial class HostnameWizardPageSaveHandler(IHostnameValidator hostnameValidator,
    ITargetConfigurationProvider targetConfigurationProvider)
        : IWizardPageSaveHandler<HostnameWizardPageState>
{
    public async Task<ISaveResult> Save(HostnameWizardPageState state, CancellationToken cancellationToken)
    {
        var field = TechnicalTerms.Hostname;

        if (string.IsNullOrWhiteSpace(state.Hostname))
            return new SaveErrorResult(string.Format(ValidationMessages.Culture, ValidationMessages.FieldIsRequired, field));

        if (state.Hostname.Length > state.HostnameMaximumLength)
            return new SaveErrorResult(string.Format(ValidationMessages.Culture, ValidationMessages.FieldMustNotHaveMoreThanXCharacters, field, state.HostnameMaximumLength));

        if (!hostnameValidator.Validate(state.Hostname, field, out var errorMessage))
            return new SaveErrorResult(errorMessage);

        var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);
        targetConfiguration.Hostname = state.Hostname;

        return new SaveSuccessResult();
    }
}
