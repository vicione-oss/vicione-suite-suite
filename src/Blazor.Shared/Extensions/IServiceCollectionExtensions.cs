using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Dialogs.Extensions;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.Module.Extensions;
using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Enums;
using Blazor.Shared.Mqtt.Services;
using Blazor.Shared.NavTiles.Extensions;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.NotificationArea.Extensions;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Profile.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.UserInterface.Extensions;
using Blazor.Shared.UserManagement.Extensions;
using Blazor.Shared.Wizards.Extensions;
using Core.Shared.HostManagement.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Extensions;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.SectionRail.Extensions;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;
using ViciOne.Ui.Blazor.Components.Tooltip.Extensions;
using ViciOne.Ui.Shared.Dx.Components.Scrolling.Extensions;
using ViciOne.Ui.TreeEditor.Builder;

namespace Blazor.Shared.Extensions;

public static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBlazorShared()
        {
            services.AddSharedUiServices();

            services.AddMqttViewer();

            services.AddNotificationElements<SharedClientModule>();

            services.AddBlazorSharedAuthorization()
                .AddDialogs()
                .AddSettings()
                .AddNetwork()
                .AddConnectionManagement()
                .AddInstanceManagement()
                .AddModuleManagement()
                .AddUserManagement()
                .AddUserInterfaceControlPanels();

            services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();

            services
                .AddNavTiles()
                .AddWizards()
                .AddOnboarding();

            return services;
        }

        private IServiceCollection AddSharedUiServices()
        {
            // Common
            services.AddScoped<ILayoutService, LayoutService>()
                .AddTransient<IJsInterop, JsInterop>()
                .AddProfile()
                .AddSystemInformation()
                .AddMessageBanner()
                .AddTooltip()
                .AddNotificationArea()
                .AddScoped<ISuiteControlService, SuiteControlService>()
                .AddScrolling()
                .AddToolbar()
                .AddSingleton<CopyrightYearProvider>();

            return services;
        }

        private IServiceCollection AddMqttViewer()
        {
            services.AddSectionRail<SectionId>();

            services.AddScoped<IMqttService, MqttService>();
            services.AddScoped<MqttViewerComponentService>();
            services.AddKeyedScoped<ITreeBuilder, TreeBuilder>(typeof(MqttTopicTreeServiceKey));
            services.AddScoped<MqttTopicTreeAdapter>();

            return services;
        }
    }
}
