using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;

namespace Core.OS.E2E.Tests.Security;

/// <summary>
/// Asks the browser what it made of the <c>Permissions-Policy</c> the suite sends. Pinning the
/// header only shows that the string reached the wire, and a browser drops a policy it cannot parse
/// as a whole. Only Chromium enforces the header, and <c>document.featurePolicy</c> is its one
/// interface to the policy it applied.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class PermissionsPolicyTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    private const string AllowedFeatures =
        "features => features.filter(feature => document.featurePolicy.allowsFeature(feature))";

    private static readonly string[] DisabledFeatures =
        ["camera", "microphone", "geolocation", "display-capture", "payment", "usb", "serial", "hid", "bluetooth"];

    /// <summary>Passkey sign-in and registration, and the copy buttons.</summary>
    private static readonly string[] FeaturesTheSuiteUses =
        ["publickey-credentials-get", "publickey-credentials-create", "clipboard-write"];

    [Fact]
    public async Task Only_the_features_the_suite_uses_stay_enabled()
    {
        await new LoginPage(Page).Goto();

        var allowed = await Page.EvaluateAsync<string[]>(
            AllowedFeatures, (string[])[.. DisabledFeatures, .. FeaturesTheSuiteUses]);

        allowed.Should().Equal(FeaturesTheSuiteUses);
    }
}
