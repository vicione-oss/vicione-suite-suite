using System.Text.RegularExpressions;
using System.Web;
using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Authentication;

/// <summary>
/// Smoke tests for linking an account to an external ID provider from the profile settings.
/// <para>
/// The provider is gitlab.com, as in docs/integration-testing.md: the Suite reads the provider's
/// discovery document before it redirects, so the authority has to be a real, reachable provider.
/// The client ID is registered nowhere, which is enough, because the test ends once the browser has
/// reached the provider.
/// </para>
/// <para>
/// A stored provider changes the instance's content security policy for every later test, so each
/// test removes it again.
/// </para>
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed partial class ExternalLoginLinkingSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    private const string Authority = "https://gitlab.com";

    private const string UnregisteredClientId = "e2e-unregistered-client";

    [GeneratedRegex(@"^https://gitlab\.com/")]
    private static partial Regex ProviderUrl();

    [Fact]
    public async Task Linking_right_after_configuring_a_provider_sends_the_shown_redirect_uri()
    {
        var settings = new SettingsPage(Page);

        await new LoginPage(Page).SignIn(TestUsers.UserName, TestUsers.Password);
        await ConfigureProvider(settings);

        try
        {
            var shownRedirectUri = await new OpenIdProviderPanel(Page).RedirectUri();

            await settings.OpenPanel(ExternalIdProvidersPanel.Category, ExternalIdProvidersPanel.PanelTitle);
            await new ExternalIdProvidersPanel(Page).AddExternalLogin();
            var authorizationRequest = await Page.RunAndWaitForRequestAsync(
                () => new AddExternalLoginPage(Page).Link(),
                ProviderUrl());

            await Expect(Page).ToHaveURLAsync(ProviderUrl());
            RedirectUriOf(authorizationRequest).Should().Be(shownRedirectUri);
        }
        finally
        {
            await RemoveProvider(settings);
        }
    }

    [Fact]
    public async Task Linking_from_a_tab_opened_before_the_provider_changed_reaches_the_provider()
    {
        var settings = new SettingsPage(Page);

        await new LoginPage(Page).SignIn(TestUsers.UserName, TestUsers.Password);
        var earlierTab = await Context.NewPageAsync();
        await earlierTab.RunAndWaitForWebSocketAsync(() => earlierTab.GotoAsync("/"));
        await ConfigureProvider(settings);

        try
        {
            var earlierTabSettings = new SettingsPage(earlierTab);

            await earlierTabSettings.Open();
            await earlierTabSettings.OpenPanel(ExternalIdProvidersPanel.Category, ExternalIdProvidersPanel.PanelTitle);
            await new ExternalIdProvidersPanel(earlierTab).AddExternalLogin();
            await earlierTab.RunAndWaitForRequestAsync(() => new AddExternalLoginPage(earlierTab).Link(), ProviderUrl());

            await Expect(earlierTab).ToHaveURLAsync(ProviderUrl());
        }
        finally
        {
            await RemoveProvider(settings);
        }
    }

    private async Task ConfigureProvider(SettingsPage settings)
    {
        await settings.Open();
        await settings.OpenPanel(OpenIdProviderPanel.Category, OpenIdProviderPanel.PanelTitle);
        await new OpenIdProviderPanel(Page).EnterProvider(Authority, UnregisteredClientId);
        await settings.ConfirmAndExpectSaved();
    }

    private static string? RedirectUriOf(IRequest authorizationRequest)
        => HttpUtility.ParseQueryString(new Uri(authorizationRequest.Url).Query)["redirect_uri"];

    private async Task RemoveProvider(SettingsPage settings)
    {
        await Page.GotoAsync("/");
        await settings.Open();
        await settings.OpenPanel(OpenIdProviderPanel.Category, OpenIdProviderPanel.PanelTitle);
        await new OpenIdProviderPanel(Page).ClearProvider();
        await settings.ConfirmAndExpectSaved();
    }
}
