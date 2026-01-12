using Blazor.Shared.Popup.Extensions;
using Blazor.Shared.Wizard.Builders;
using Blazor.Shared.Wizard.Components;
using Blazor.Shared.Wizard.Factories;
using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Shared.Wizard.Extensions;

public static class IServiceCollectionExtensions
{
    public static IWizardBuilder<TContext> AddWizard<TContext>(this IServiceCollection services)
    {
        services.AddScoped<IWizardPageEditService<TContext>, WizardPageEditService<TContext>>();
        services.AddScoped<IWizardState<TContext>, WizardState<TContext>>();
        services.AddWizardPageRegistry<TContext>();
        services.TryAddScoped<IWizardStepFactory, WizardStepFactory>();

        services.AddLoadingIndicationPlacementBehavior();

        var builder = new WizardBuilder<TContext>(services);

        return builder;
    }

    private static IServiceCollection AddWizardPageRegistry<TContext>(this IServiceCollection services)
    {
        services.TryAddScoped<IWizardPageRegistry<TContext>>(serviceProvider =>
        {
            var registry = new WizardPageRegistry<TContext>();

            Func<IWizardPageDescriptor, IWizardPageState, IWizardPageRegistryItem> addMethodDelegate =
                registry.Add<WizardPage<IWizardPageState>, IWizardPageState>;

            var addMethodInfo = addMethodDelegate.Method.GetGenericMethodDefinition();

            var wizardPageInfos = serviceProvider.GetKeyedServices<WizardPageInfo>(typeof(TContext));
            foreach (var i in wizardPageInfos)
            {
                var descriptorType = typeof(IWizardPageDescriptor);

                if (serviceProvider.GetKeyedServices(descriptorType, i.KeyedServiceKey).FirstOrDefault() is not IWizardPageDescriptor descriptor)
                    continue;

                var state = (IWizardPageState)serviceProvider.GetRequiredKeyedService(i.StateType, i.KeyedServiceKey);

                addMethodInfo.MakeGenericMethod(i.ComponentType, i.StateType).Invoke(registry, [descriptor, state]);
            }

            return registry;
        });

        return services;
    }

    internal static IServiceCollection AddWizardPageDescriptor(this IServiceCollection services,
        WizardPageInfo wizardPageInfo)
    {
        var descriptorInterfaceType = typeof(IWizardPageDescriptor);

        if (descriptorInterfaceType.IsAssignableFrom(wizardPageInfo.DescriptorType))
            services.AddKeyedScoped(descriptorInterfaceType, wizardPageInfo.KeyedServiceKey, wizardPageInfo.DescriptorType);

        return services;
    }

    internal static IServiceCollection AddWizardPageState(this IServiceCollection services, WizardPageInfo wizardPageInfo)
        => services.AddKeyedScoped(wizardPageInfo.StateType, wizardPageInfo.KeyedServiceKey);
}
