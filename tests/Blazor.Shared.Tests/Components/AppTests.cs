using Blazor.Shared.Components;

namespace Blazor.Shared.Tests.Components;

public class AppTests
{
    public sealed class IsAccountPath : AppTests
    {
        [Theory]
        [InlineData("account")]
        [InlineData("account/login")]
        [InlineData("Account/Login")]
        [InlineData("account/change-password?returnUrl=%2F")]
        public void Should_match_account_pages(string baseRelativePath)
        {
            // Act
            var result = App.IsAccountPath(baseRelativePath);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("accounts")]
        [InlineData("accounting/invoices")]
        [InlineData("settings/account-overview")]
        [InlineData("cluster?returnUrl=/account/login")]
        public void Should_not_match_other_pages(string baseRelativePath)
        {
            // Act
            var result = App.IsAccountPath(baseRelativePath);

            // Assert
            result.Should().BeFalse();
        }
    }
}
