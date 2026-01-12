using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Onboarding.Components.WizardPages;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Onboarding.Services.Validators;
using Blazor.Shared.UserManagement.Extensions;
using Blazor.Shared.Validation.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Wizards.Extensions;

namespace Blazor.Shared.Onboarding.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddOnboarding(this IServiceCollection services)
    {
        services.AddScoped<ITargetConfigurationProvider, TargetConfigurationProvider>();

        services.AddScoped<IOnboardingWizardContext, OnboardingWizardContext>();

        var wizardBuilder = services.AddWizard<IOnboardingWizardContext>();

        wizardBuilder.WithPage<WelcomeWizardPage>()
                .WithAutoDiscovery<WelcomeWizardPageDescriptor>();

        services.AddUsernameValidator()
            .AddPasswordValidator()
            .AddRepeatPasswordValidator();

        wizardBuilder.WithPage<PasswordWizardPage, PasswordWizardPageState>()
            //.WithAutoDiscovery<PasswordWizardPageDescriptor>() no auto discovery to avoid possible attack vectors
            .WithSaveHandler<PasswordWizardPageSaveHandler>();

        services.AddRequiredValidator()
            .AddScoped<IHostnameValidator, HostnameValidator>();

        wizardBuilder.WithPage<HostnameWizardPage, HostnameWizardPageState>()
            .WithAutoDiscovery<HostnameWizardPageDescriptor>()
            .WithResetHandler<HostnameWizardPageResetHandler>()
            .WithSaveHandler<HostnameWizardPageSaveHandler>();

        wizardBuilder.WithPage<TimeZoneWizardPage, TimeZoneWizardPageState>()
            .WithAutoDiscovery<TimeZoneWizardPageDescriptor>()
            .WithResetHandler<TimeZoneWizardPageResetHandler>()
            .WithSaveHandler<TimeZoneWizardPageSaveHandler>();

        services.AddRequiredValidator()
            .AddIpAddressValidator();

        wizardBuilder.WithPage<NetworkWizardPage, NetworkWizardPageState>()
            .WithAutoDiscovery<NetworkWizardPageDescriptor>()
            .WithResetHandler<NetworkWizardPageResetHandler>()
            .WithSaveHandler<NetworkWizardPageSaveHandler>();

        wizardBuilder.WithPage<SummaryWizardPage, SummaryWizardPageState>()
            .WithAutoDiscovery<SummaryWizardPageDescriptor>()
            .WithSaveHandler<SummaryWizardPageSaveHandler>()
            .WithResetHandler<SummaryWizardPageResetHandler>();

        return services;
    }
}
