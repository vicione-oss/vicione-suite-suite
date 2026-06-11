using AwesomeAssertions;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.UserManagement.Extensions;

public class UserManagerExtensionsTests
{
    private static UserManager<SuiteUser> CreateFakeUserManager()
        => Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),
            null, null, null, null, null, null, null, null);

    public sealed class InvalidateLogins : UserManagerExtensionsTests
    {
        [Fact]
        public async Task Should_update_security_stamp_for_all_users()
        {
            // Arrange
            var user1 = new SuiteUser { UserName = "u1" };
            var user2 = new SuiteUser { UserName = "u2" };
            using var userManager = CreateFakeUserManager();

            userManager.Users.Returns(new[] { user1, user2 }.AsQueryable());

            // Act
            await userManager.InvalidateLogins();

            // Assert
            await userManager.Received(1).UpdateSecurityStampAsync(user1);
            await userManager.Received(1).UpdateSecurityStampAsync(user2);
        }
    }

    public sealed class IsLastSystemAdministrator : UserManagerExtensionsTests
    {
        [Fact]
        public async Task Should_return_false_if_user_is_not_system_administrator()
        {
            // Arrange
            var user = new SuiteUser { UserName = "bob" };
            using var userManager = CreateFakeUserManager();
            userManager.GetRolesAsync(user).Returns(new List<string> { "OtherRole" });

            // Act
            var result = await userManager.IsLastSystemAdministrator(user);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_return_true_if_user_is_last_system_administrator()
        {
            // Arrange
            var user = new SuiteUser { UserName = "admin" };
            using var userManager = CreateFakeUserManager();

            userManager.GetRolesAsync(user).Returns(new List<string> { AuthorizationConstants.AdminRoleName });
            userManager.GetUsersInRoleAsync(AuthorizationConstants.AdminRoleName).Returns(new List<SuiteUser> { user });

            // Act
            var result = await userManager.IsLastSystemAdministrator(user);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task Should_return_false_if_there_are_multiple_system_administrators()
        {
            // Arrange
            var user = new SuiteUser { UserName = "admin1" };
            var other = new SuiteUser { UserName = "admin2" };
            using var userManager = CreateFakeUserManager();
            
            userManager.GetRolesAsync(user).Returns(new List<string> { AuthorizationConstants.AdminRoleName });
            userManager.GetUsersInRoleAsync(AuthorizationConstants.AdminRoleName).Returns(new List<SuiteUser> { user, other });

            // Act
            var result = await userManager.IsLastSystemAdministrator(user);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_throw_if_no_system_admins_exist()
        {
            // Arrange
            var user = new SuiteUser { UserName = "admin" };
            using var userManager = CreateFakeUserManager();

            userManager.GetRolesAsync(user).Returns(new List<string> { AuthorizationConstants.AdminRoleName });
            userManager.GetUsersInRoleAsync(AuthorizationConstants.AdminRoleName).Returns(new List<SuiteUser>());

            // Act
            var act = async () => await userManager.IsLastSystemAdministrator(user);

            // Assert
            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("No System Administrator left");
        }
    }

    public sealed class GetUserById : UserManagerExtensionsTests
    {
        [Fact]
        public async Task Should_return_user_if_found()
        {
            // Arrange
            var user = new SuiteUser
            {
                UserName = "u2",
                Id = "2"
            };
            using var userManager = CreateFakeUserManager();
            userManager.FindByIdAsync("2").Returns(user);

            // Act
            var result = await userManager.GetUserById("2");

            // Assert
            result.Should().Be(user);
        }

        [Fact]
        public async Task Should_throw_if_user_not_found()
        {
            // Arrange
            using var userManager = CreateFakeUserManager();
            userManager.FindByIdAsync("2").Returns(null as SuiteUser);

            // Act
            var userSearch = userManager.GetUserById("2");

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await userSearch);
        }
    }
}
