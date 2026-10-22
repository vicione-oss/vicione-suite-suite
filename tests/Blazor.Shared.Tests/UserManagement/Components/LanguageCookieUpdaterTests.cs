using Blazor.Shared.UserManagement.Components;
using Blazor.Shared.UserManagement.Services;
using Bunit;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Tests.UserManagement.Components;

public sealed class LanguageCookieUpdaterTests
{
    private const string SignedInUserName = "Admin";

    private readonly ILanguageCookieReader _languageCookieReader = Substitute.For<ILanguageCookieReader>();
    private readonly IUiMediator _uiMediator = Substitute.For<IUiMediator>();
    private readonly INonceStore _nonceStore = Substitute.For<INonceStore>();

    [Theory]
    [InlineData("de-DE", null)]
    [InlineData("en-US", "de-DE")]
    public async Task Should_leave_a_mismatched_language_cookie_unchanged_during_static_rendering(
        string cookieLanguage, string? storedLanguage)
    {
        // Arrange
        await using var ctx = CreateContext(cookieLanguage, storedLanguage);
        ctx.SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();
        var initialUri = navigationManager.Uri;

        // Act
        ctx.Render<LanguageCookieUpdater>();

        // Assert
        navigationManager.Uri.Should().Be(initialUri);
    }

    [Theory]
    [InlineData("de-DE", null, "Remove")]
    [InlineData("en-US", "de-DE", "Update")]
    public async Task Should_correct_a_mismatched_language_cookie_during_interactive_rendering(
        string cookieLanguage, string? storedLanguage, string expectedAction)
    {
        // Arrange
        await using var ctx = CreateContext(cookieLanguage, storedLanguage);
        ctx.SetRendererInfo(new RendererInfo("Server", isInteractive: true));

        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();

        // Act
        ctx.Render<LanguageCookieUpdater>();

        // Assert
        navigationManager.ToBaseRelativePath(navigationManager.Uri)
            .Should().StartWith($"{Constants.UpdateLanguageCookieRoute}/{expectedAction}?");
    }

    private BunitContext CreateContext(string cookieLanguage, string? storedLanguage)
    {
        var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized(SignedInUserName);

        _languageCookieReader.GetCookieValue().Returns($"c={cookieLanguage}|uic={cookieLanguage}");

        _uiMediator.Request<GetUsers, GetUsersResponse>(Arg.Any<GetUsers>(), Arg.Any<CancellationToken>())
            .Returns(new GetUsersResponse([
                new UserProfile { UserName = new UserName(SignedInUserName), Language = storedLanguage }
            ]));
        _uiMediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
                Arg.Any<GetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new GetCrossInstanceConfigurationResponse(new CrossInstanceConfiguration()));

        _nonceStore.Create(Arg.Any<CancellationToken>())
            .Returns(new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow });

        ctx.Services
            .AddSingleton(_languageCookieReader)
            .AddSingleton(_uiMediator)
            .AddSingleton(_nonceStore);

        return ctx;
    }
}
