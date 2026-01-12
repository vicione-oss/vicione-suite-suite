using Blazor.Shared.Extensions;
using Blazor.Wasm.Client.Localization;
using Core.Shared.Instance.HealthCheck;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;

namespace Blazor.Wasm.Client;

public sealed class BlazorWasmClientModule : ClientModule
{
    public override Action<IServiceCollection, HostingModel> ConfigureServices
        => (services, hostingModel) =>
        {
            if (hostingModel != HostingModel.BlazorWasm)
                throw new InvalidOperationException();

            services.AddBlazorShared();

            services.AddSingleton<MasterHealthInfo>();

            services.AddSingleton<IMasterHealthInfo>(s =>
            {
                var info = s.GetRequiredService<MasterHealthInfo>();
                info.IsMasterReachable = true;

                return info;
            });

            services.AddLocalization<BlazorWasmClientModule, Localizer>();
        };
}
