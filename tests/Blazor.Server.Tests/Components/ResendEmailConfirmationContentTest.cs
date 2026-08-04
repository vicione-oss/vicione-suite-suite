using System.Reflection;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Components;
using Blazor.Shared;
using Blazor.Shared.Services;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Sdk.Backend.Messaging;

namespace Blazor.Server.Tests.Components;

public sealed class ResendEmailConfirmationContentTest
{
    private const string TestEmail = "user@example.com";

    private readonly UserManager<SuiteUser> _userManager = TestFactory.CreateUserManager();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly ISuiteMediator _mediator = Substitute.For<ISuiteMediator>();

    public ResendEmailConfirmationContentTest()
    {
        _navigationService.NavManager.Returns(new TestNavigationManager());
    }

    [Fact]
    public async Task Should_redirect_without_sending_email_when_user_is_not_found()
    {
        // Arrange
        _userManager.FindByEmailAsync(TestEmail).Returns((SuiteUser?)null);
        var sut = CreateSubjectUnderTest(TestEmail);

        // Act
        await sut.ResendEmail();

        // Assert
        _navigationService.Received(1).RedirectTo(IdentityRoutes.EmailConfirmationRoute);
        await _mediator.DidNotReceive().Send(Arg.Any<SendVerifyEmailAddressLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_redirect_without_sending_email_when_email_is_already_confirmed()
    {
        // Arrange
        var user = new SuiteUser
        {
            Id = "user-1",
            Email = TestEmail
        };
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        var sut = CreateSubjectUnderTest(TestEmail);

        // Act
        await sut.ResendEmail();

        // Assert
        _navigationService.Received(1).RedirectTo(IdentityRoutes.EmailConfirmationRoute);
        await _mediator.DidNotReceive().Send(Arg.Any<SendVerifyEmailAddressLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_verify_email_address_link_when_user_exists_and_email_is_not_confirmed()
    {
        // Arrange
        var user = new SuiteUser
        {
            Id = "user-1",
            Email = TestEmail
        };
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(false);
        _userManager.GenerateEmailConfirmationTokenAsync(user).Returns("confirmation-token");
        var sut = CreateSubjectUnderTest(TestEmail);

        // Act
        await sut.ResendEmail();

        // Assert
        await _mediator.Received(1)
            .Send(Arg.Is<SendVerifyEmailAddressLink>(cmd => cmd!.UserId == user.Id), Arg.Any<CancellationToken>());
        _navigationService.Received(1).RedirectTo(IdentityRoutes.EmailConfirmationRoute);
    }

    private ResendEmailConfirmationContent CreateSubjectUnderTest(string email)
    {
        var component = new ResendEmailConfirmationContent();
        SetPrivateProperty(component, "UserManager", _userManager);
        SetPrivateProperty(component, "NavigationService", _navigationService);
        SetPrivateProperty(component, "Mediator", _mediator);
        SetPrivateProperty(component, "Input", new ResendEmailConfirmationFormModel { Email = email });
        return component;
    }

    private static void SetPrivateProperty(object target, string propertyName, object value)
    {
        typeof(ResendEmailConfirmationContent)
            .GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(target, value);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
