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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Xunit;

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
    public void Component_should_render()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx()
            .AddAuthorization();

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
            Arg.Any<GetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new GetCrossInstanceConfigurationResponse(new CrossInstanceConfiguration()));

        ctx.Services.AddProfile();

        _userProfileMock.GetUsers(Arg.Any<UserName?>(), Arg.Any<CancellationToken>())
            .Returns([new UserProfile { UserName = new UserName("MyDummy") }]);

        // Act
        var component = ctx.Render<ProfileNotificationElementFlyoutContent>();

        // Assert
        Assert.NotNull(component);
    }
}
