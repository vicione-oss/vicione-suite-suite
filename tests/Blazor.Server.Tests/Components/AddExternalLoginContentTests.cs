using AngleSharp.Dom;
using Blazor.Server.Backend.Components;
using Blazor.Shared;
using Blazor.Shared.Profile.Localization;
using Bunit;
using Core.Shared.Security;
using Core.Shared.UserManagement.Configuration;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Server.Tests.Components;

public sealed class AddExternalLoginContentTests
{
    private const string AntiforgeryFieldName = "__RequestVerificationToken";

    private readonly IExternalAuthenticationSettings _externalAuthenticationSettings =
        Substitute.For<IExternalAuthenticationSettings>();

    [Fact]
    public void Should_post_the_provider_with_an_antiforgery_token_to_the_linking_endpoint()
    {
        // Arrange
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(true);
        using var ctx = CreateContext();

        // Act
        var component = ctx.Render<AddExternalLoginContent>();

        // Assert
        var linkForm = FormSubmittedBy(component, component.Find("button.with-icon"));
        linkForm.GetAttribute("method").Should().Be("post");
        linkForm.GetAttribute("action").Should().Be(IdentityRoutes.LinkExternalLoginRoute);
        linkForm.QuerySelector("input[name=provider]")!.GetAttribute("value")
            .Should().Be(ProviderConstants.DefaultProviderName);
        linkForm.QuerySelector($"input[name={AntiforgeryFieldName}]").Should().NotBeNull();
    }

    [Fact]
    public void Should_offer_only_cancelling_when_no_provider_is_configured()
    {
        // Arrange
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(false);
        using var ctx = CreateContext();

        // Act
        var component = ctx.Render<AddExternalLoginContent>();

        // Assert
        component.Markup.Should().Contain(ExternalIdProviders.NoProviderConfigured);
        var cancelForm = FormSubmittedBy(component, component.FindAll("button").Should().ContainSingle().Subject);
        cancelForm.GetAttribute("method").Should().Be("get");
        cancelForm.GetAttribute("action").Should().Be("/");
    }

    private BunitContext CreateContext()
    {
        var antiforgery = Substitute.For<AntiforgeryStateProvider>();
        antiforgery.GetAntiforgeryToken().Returns(new AntiforgeryRequestToken("token", AntiforgeryFieldName));

        var ctx = new BunitContext();
        ctx.Services
            .AddSingleton(_externalAuthenticationSettings)
            .AddSingleton(antiforgery);

        return ctx;
    }

    private static IElement FormSubmittedBy(IRenderedComponent<AddExternalLoginContent> component, IElement button)
        => component.Find($"form#{button.GetAttribute("form")}");
}
