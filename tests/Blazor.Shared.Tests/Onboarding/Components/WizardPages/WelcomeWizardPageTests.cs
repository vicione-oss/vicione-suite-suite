using Blazor.Shared.Onboarding.Components.WizardPages;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Wizards.Services;
using WelcomeWizardPageTexts = Blazor.Shared.Onboarding.Components.WizardPages.Localization.WelcomeWizardPage;

namespace Blazor.Shared.Tests.Onboarding.Components.WizardPages;

public sealed class WelcomeWizardPageTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly List<IWizardPageRegistryItem> _registeredPages = [];

    public WelcomeWizardPageTests()
    {
        _ctx.SetupBlazorSharedSettings(setup => setup.Services.AddControlPanelInfrastructure()).SetLocalServices();

        var registry = Substitute.For<IWizardPageRegistry<IOnboardingWizardContext>>();
        registry.GetEnumerator().Returns(_ => _registeredPages.GetEnumerator());
        _ctx.Services.AddSingleton(registry);
    }

    public ValueTask DisposeAsync()
        => _ctx.DisposeAsync();

    private void RegisterPage<TComponent>()
    {
        var item = Substitute.For<IWizardPageRegistryItem>();
        item.ComponentType.Returns(typeof(TComponent));

        _registeredPages.Add(item);
    }

    private IRenderedComponent<WelcomeWizardPage> Render()
        => _ctx.Render<WelcomeWizardPage>(builder => builder.Add(c => c.State, new WizardPageState()));

    [Fact]
    public void Should_list_password_task_when_password_page_is_registered()
    {
        // Arrange
        RegisterPage<WelcomeWizardPage>();
        RegisterPage<PasswordWizardPage>();

        // Act
        var component = Render();

        // Assert
        component.Markup.Should().Contain(WelcomeWizardPageTexts.InitialTask1);
        component.Markup.Should().Contain(WelcomeWizardPageTexts.InitialTask2);
    }

    [Fact]
    public void Should_omit_password_task_when_password_page_is_not_registered()
    {
        // Arrange
        RegisterPage<WelcomeWizardPage>();
        RegisterPage<HostnameWizardPage>();

        // Act
        var component = Render();

        // Assert
        component.Markup.Should().NotContain(WelcomeWizardPageTexts.InitialTask1);
        component.Markup.Should().Contain(WelcomeWizardPageTexts.InitialTask2);
    }
}
