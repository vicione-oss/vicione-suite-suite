using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Tests.UserManagement.Services;

public sealed class ExternalAccountServiceTest
{
    [Fact]
    public async Task Should_return_null_when_no_external_accounts_exist()
    {
        // Arrange
        var user = new SuiteUser { UserName = "test" };
        using var sut = CreateSubjectUnderTest();

        // Act
        var externalAccount = await sut.GetExternalUserAccount(user);

        // Assert
        externalAccount.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_return_the_only_existing_account()
    {
        // Arrange
        var user = new SuiteUser { UserName = "test" };
        using var sut = CreateSubjectUnderTest(new UserLoginInfo("test", "test",  "test"));

        // Act
        var externalAccount = await sut.GetExternalUserAccount(user);

        // Assert
        externalAccount?.ProviderKey.Should().Be("test");
    }

    [Fact]
    public async Task Should_throw_when_there_are_multiple_accounts()
    {
        // Arrange
        var user = new SuiteUser { UserName = "test" };
        using var sut = CreateSubjectUnderTest(new UserLoginInfo("test", "test",  "test"),
            new UserLoginInfo("test2", "test2",  "test2"));

        // Act
        var act = async () => await sut.GetExternalUserAccount(user);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }


    private static ExternalAccountService CreateSubjectUnderTest(
        params UserLoginInfo[] userLoginInfo)
    {
        var userManager = Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<SuiteUser>>(),
            Substitute.For<IEnumerable<IUserValidator<SuiteUser>>>(),
            Substitute.For<IEnumerable<IPasswordValidator<SuiteUser>>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<SuiteUser>>>());

        userManager.GetLoginsAsync(Arg.Any<SuiteUser>())
            .Returns(Task.FromResult<IList<UserLoginInfo>>(userLoginInfo.ToList()));

        var mediator = Substitute.For<IUiMediator>();

        return new ExternalAccountService(mediator, userManager);
    }
}
