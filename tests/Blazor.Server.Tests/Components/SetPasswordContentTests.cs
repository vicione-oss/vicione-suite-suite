using System.Security.Claims;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Components;
using Blazor.Shared;
using Blazor.Shared.Services;
using Bunit;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Server.Tests.Components;

public sealed class SetPasswordContentTests
{
    private const string NewPassword = "N3w-Password!";

    private readonly SuiteUser _user = new() { UserName = "oidc.user" };
    private readonly UserManager<SuiteUser> _userManager = TestFactory.CreateUserManager();
    private readonly SignInManager<SuiteUser> _signInManager;
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    public SetPasswordContentTests()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "oidc.user")], "Test"));
        _userManager.GetUserAsync(principal).Returns(_user);
        _userManager.AddPasswordAsync(_user, Arg.Any<string>()).Returns(IdentityResult.Success);

        _signInManager = TestFactory.CreateSignInManager(_userManager);
        _signInManager.Context = new DefaultHttpContext { User = principal };
    }

    [Fact]
    public void Should_send_a_user_who_has_a_password_to_change_password()
    {
        // Arrange
        _userManager.HasPasswordAsync(_user).Returns(true);
        using var ctx = CreateContext();

        // Act
        ctx.Render<SetPasswordContent>(parameters => parameters.Add(p => p.Input, new SetPasswordFormModel()));

        // Assert
        _navigationService.Received(1).RedirectTo(IdentityRoutes.ChangePasswordRoute);
    }

    [Fact]
    public async Task Should_add_the_password_and_refresh_the_sign_in()
    {
        // Arrange
        using var ctx = CreateContext();
        var component = RenderWith(ctx, NewPassword, NewPassword);

        // Act
        await component.Find("form.set-password").SubmitAsync();

        // Assert
        await _userManager.Received(1).AddPasswordAsync(_user, NewPassword);
        await _signInManager.Received(1).RefreshSignInAsync(_user);
        _navigationService.Received(1).RedirectTo("/");
    }

    [Fact]
    public async Task Should_not_add_the_password_when_the_confirmation_differs()
    {
        // Arrange
        using var ctx = CreateContext();
        var component = RenderWith(ctx, NewPassword, "Other-Password!1");

        // Act
        await component.Find("form.set-password").SubmitAsync();

        // Assert
        await _userManager.DidNotReceive().AddPasswordAsync(Arg.Any<SuiteUser>(), Arg.Any<string>());
        _navigationService.DidNotReceive().RedirectTo(Arg.Any<string>());
    }

    [Fact]
    public async Task Should_show_why_the_password_is_rejected()
    {
        // Arrange
        var tooShort = new IdentityErrorDescriber().PasswordTooShort(12);
        _userManager.AddPasswordAsync(_user, NewPassword).Returns(IdentityResult.Failed(tooShort));
        using var ctx = CreateContext();
        var component = RenderWith(ctx, NewPassword, NewPassword);

        // Act
        await component.Find("form.set-password").SubmitAsync();

        // Assert
        component.Markup.Should().Contain(tooShort.Description);
        await _signInManager.DidNotReceive().RefreshSignInAsync(Arg.Any<SuiteUser>());
        _navigationService.DidNotReceive().RedirectTo(Arg.Any<string>());
    }

    [Fact]
    public async Task Should_remove_an_expired_expiry_date_in_the_same_save_as_the_password()
    {
        // Arrange
        _user.PasswordExpirationDate = DateTimeOffset.MinValue;
        DateTimeOffset? savedExpiryDate = DateTimeOffset.MinValue;
        _userManager
            .When(manager => manager.AddPasswordAsync(_user, NewPassword))
            .Do(_ => savedExpiryDate = _user.PasswordExpirationDate);
        using var ctx = CreateContext();
        var component = RenderWith(ctx, NewPassword, NewPassword);

        // Act
        await component.Find("form.set-password").SubmitAsync();

        // Assert
        savedExpiryDate.Should().BeNull();
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<SuiteUser>());
    }

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services
            .AddSingleton(_userManager)
            .AddSingleton(_signInManager)
            .AddSingleton(_navigationService);

        return ctx;
    }

    private static IRenderedComponent<SetPasswordContent> RenderWith(
        BunitContext ctx,
        string newPassword,
        string confirmPassword)
        => ctx.Render<SetPasswordContent>(parameters => parameters.Add(
            p => p.Input,
            new SetPasswordFormModel { NewPassword = newPassword, ConfirmPassword = confirmPassword }));
}
