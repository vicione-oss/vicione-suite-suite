using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.Services;

public class ExternalAccountServiceTest
{
    [Fact]
    public async Task Should_return_null_when_no_external_accounts_exist()
    {
        // Arrange
        var sut = CreateSubjectUnderTest();

        // Act
        var externalAccount = await sut.GetExternalUserAccount("test");
        
        // Assert
        Assert.Null(externalAccount);
    }
    
    [Fact]
    public async Task Should_return_return_the_only_existing_account()
    {
        // Arrange
        var sut = CreateSubjectUnderTest(new UserLoginInfo("test", "test",  "test"));

        // Act
        var externalAccount = await sut.GetExternalUserAccount("test");
        
        // Assert
        Assert.Equal("test", externalAccount?.ProviderKey);
    }
    
    [Fact]
    public async Task Should_throw_when_there_are_multiple_accounts()
    {
        // Arrange
        var sut = CreateSubjectUnderTest(new UserLoginInfo("test", "test",  "test"),
            new UserLoginInfo("test2", "test2",  "test2"));

        // Act
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.GetExternalUserAccount("test"));
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
        
        userManager.FindByNameAsync(Arg.Any<string>())
            .Returns(Task.FromResult<SuiteUser?>(new SuiteUser
            {
                UserName = "test"
            }));
        
        userManager.GetLoginsAsync(Arg.Any<SuiteUser>())
            .Returns(Task.FromResult<IList<UserLoginInfo>>(userLoginInfo.ToList()));

        return new ExternalAccountService(userManager);
    }
}
