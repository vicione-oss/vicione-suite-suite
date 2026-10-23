using System.Security.Claims;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Components;
using Blazor.Shared;
using Blazor.Shared.Services;
using Bunit;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Server.Tests.Components;

public sealed class ChangePasswordContentTests
{
    private const string UserName = "oidc.user";

    private readonly SuiteUser _user = new() { UserName = UserName };
    private readonly ClaimsPrincipal _principal =
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, UserName)], "Test"));
    private readonly UserManager<SuiteUser> _userManager = TestFactory.CreateUserManager();
    private readonly SignInManager<SuiteUser> _signInManager;
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    public ChangePasswordContentTests()
    {
        _userManager.FindByNameAsync(UserName).Returns(_user);
        _userManager.GetUserAsync(_principal).Returns(_user);
        _userManager.HasPasswordAsync(_user).Returns(false);

        _signInManager = TestFactory.CreateSignInManager(_userManager);
        _signInManager.Context = new DefaultHttpContext { User = _principal };
    }

    [Fact]
    public void Should_send_a_signed_in_user_without_password_to_set_password()
    {
        // Arrange
        _signInManager.IsSignedIn(_principal).Returns(true);
        using var ctx = CreateContext();

        // Act
        Render(ctx);

        // Assert
        _navigationService.Received(1).RedirectTo(IdentityRoutes.SetPasswordRoute);
    }

    [Fact]
    public void Should_keep_a_signed_out_visitor_on_the_page()
    {
        // Arrange
        _signInManager.IsSignedIn(Arg.Any<ClaimsPrincipal>()).Returns(false);
        using var ctx = CreateContext();

        // Act
        Render(ctx);

        // Assert
        _navigationService.DidNotReceive().RedirectTo(Arg.Any<string>());
    }

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services
            .AddSingleton(_userManager)
            .AddSingleton(_signInManager)
            .AddSingleton(_navigationService)
            .AddSingleton(Substitute.For<ILoginDesignService>());

        return ctx;
    }

    private static void Render(BunitContext ctx)
        => ctx.Render<ChangePasswordContent>(parameters => parameters
            .Add(p => p.Input, new ChangePasswordFormModel())
            .Add(p => p.User, UserName));
}
