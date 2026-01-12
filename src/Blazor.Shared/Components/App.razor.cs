using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;

namespace Blazor.Shared.Components
{
    public partial class App
    {
        private string _title = string.Empty;
        private string _redirectText = string.Empty;
        private string _interruptionText = string.Empty;
        private string _waitText = string.Empty;
        private string _wentWrongText = string.Empty;
        private string _administratorText = string.Empty;

        [Inject] private IAppRenderingProvider RenderModeProvider { get; set; } = default!;
        [Inject] private IHostEnvironment Env { get; set; } = default!;

        protected override void OnInitialized()
        {
            _title = Localization.App.Title;
            _redirectText = Localization.App.RedirectText;
            _interruptionText = Localization.App.InterruptionText;
            _waitText = Localization.App.WaitText;
            _wentWrongText = Localization.App.WentWrongText;
            _administratorText = Localization.App.AdministratorText;
        }
    }
}
