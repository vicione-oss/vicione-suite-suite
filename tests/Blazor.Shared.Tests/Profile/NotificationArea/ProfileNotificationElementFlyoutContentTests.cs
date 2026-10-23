using Blazor.Shared.Localization;
using Blazor.Shared.Profile.Extensions;
using Blazor.Shared.Profile.NotificationArea;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.UserManagement.Services;
using Blazor.Shared.Validation.Services.Validators;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sdk.Client.Infrastructure;
using FlyoutLocalization = Blazor.Shared.Profile.NotificationArea.Localization.ProfileNotificationElementFlyoutContent;

namespace Blazor.Shared.Tests.Profile.NotificationArea;

public sealed class ProfileNotificationElementFlyoutContentTests
{
    private readonly IHostEnvironment _hostEnvironmentMock = Substitute.For<IHostEnvironment>();
    private readonly INavigationService _navigationMock = Substitute.For<INavigationService>();
    private readonly IUserService _userProfileMock = Substitute.For<IUserService>();
    private readonly ITimeZoneDescriptorProvider _timeZoneDescriptorProviderMock = Substitute.For<ITimeZoneDescriptorProvider>();
    private readonly IClientTimeProvider _clientTimeProviderMock = Substitute.For<IClientTimeProvider>();
    private readonly IUiMediator _uiMediator = Substitute.For<IUiMediator>();
    private readonly IEmailValidator _emailValidator = Substitute.For<IEmailValidator>();
    private readonly IPhoneNumberValidator _phoneNumberValidator = Substitute.For<IPhoneNumberValidator>();

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = MockContext();

        // Act
        var component = ctx.Render<ProfileNotificationElementFlyoutContent>();

        // Assert
        Assert.Contains("MyDummy", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_offer_a_user_without_password_to_set_one()
    {
        // Arrange
        using var ctx = MockContext(hasPassword: false);
        var component = ctx.Render<ProfileNotificationElementFlyoutContent>();

        // Act
        component.Find("button.set-password-button").Click();

        // Assert
        component.Find("button.set-password-button").TextContent.Should().Contain(PasswordTerms.SetPassword);
        new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath
            .Should().Be(IdentityRoutes.SetPasswordRoute);
    }

    [Fact]
    public void Should_offer_a_user_with_password_to_change_it()
    {
        // Arrange
        using var ctx = MockContext(hasPassword: true);
        var component = ctx.Render<ProfileNotificationElementFlyoutContent>();

        // Act
        component.Find("button.change-password-button").Click();

        // Assert
        component.Find("button.change-password-button").TextContent.Should().Contain(FlyoutLocalization.ChangePassword);
        new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath
            .Should().Be(IdentityRoutes.ChangePasswordRoute);
    }

    private BunitContext MockContext(bool hasPassword = true)
    {
        var ctx = new BunitContext();
        ctx.SetupBlazorUiComponents(setup => setup.UseNavigationManager = false)
            .AddAuthorization()
            .SetAuthorized("MyDummy");

        ctx.Services
            .AddSingleton(_hostEnvironmentMock)
            .AddSingleton(_userProfileMock)
            .AddSingleton(_navigationMock)
            .AddSingleton(_timeZoneDescriptorProviderMock)
            .AddSingleton(_clientTimeProviderMock)
            .AddSingleton(_uiMediator)
            .AddSingleton(_emailValidator)
            .AddSingleton(_phoneNumberValidator);

        _uiMediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
                Arg.Any<GetCrossInstanceConfiguration>(),
                Arg.Any<CancellationToken>())
            .Returns(new GetCrossInstanceConfigurationResponse(new CrossInstanceConfiguration()));

        ctx.Services.AddProfile();

        _userProfileMock.GetUsers(Arg.Any<UserName?>(), Arg.Any<CancellationToken>())
            .Returns([new UserProfile { UserName = new UserName("MyDummy"), HasPassword = hasPassword }]);
        return ctx;
    }
}
