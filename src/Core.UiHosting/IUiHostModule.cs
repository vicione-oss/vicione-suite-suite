using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Core.UiHosting;

public interface IUiHostModule
{
    string ModuleId { get; }

    void LoadUiDependencies(IServiceCollection services, IUiHostEnvironment uiEnvironment);

    void ConfigureIdentity(IdentityBuilder builder);

    void ConfigureUiServices(IServiceCollection services, IUiHostEnvironment uiEnvironment, Action<string, Exception>? errorOccured = null);

    void UseSecurity(IApplicationBuilder app, bool useHeaderForwarding);

    void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env, IUiHostEnvironment uiEnvironment);
}
