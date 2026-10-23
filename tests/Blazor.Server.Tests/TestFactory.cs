using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blazor.Server.Tests;

internal class TestFactory
{
    public static UserManager<SuiteUser> CreateUserManager()
        => Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),                          // store
            Substitute.For<IOptions<IdentityOptions>>(),                      // optionsAccessor
            Substitute.For<IPasswordHasher<SuiteUser>>(),                     // passwordHasher
            Array.Empty<IUserValidator<SuiteUser>>(),                         // userValidators
            Array.Empty<IPasswordValidator<SuiteUser>>(),                     // passwordValidators
            Substitute.For<ILookupNormalizer>(),                              // keyNormalizer
            new IdentityErrorDescriber(),                                     // errors
            Substitute.For<IServiceProvider>(),                               // services
            Substitute.For<ILogger<UserManager<SuiteUser>>>()
            );

    public static SignInManager<SuiteUser> CreateSignInManager(UserManager<SuiteUser> userManager)
        => Substitute.For<SignInManager<SuiteUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<SuiteUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<SuiteUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<SuiteUser>>());
}
