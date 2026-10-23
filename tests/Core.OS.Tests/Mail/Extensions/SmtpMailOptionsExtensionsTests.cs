using Core.OS.Mail;
using Core.OS.Mail.Extensions;

namespace Core.OS.Tests.Mail.Extensions;

public class SmtpMailOptionsExtensionsTests
{
    private static readonly SmtpMailOptions CompleteOptions = new()
    {
        ServerAddress = "mail.example.com",
        ServerPort = 587,
        FromUserName = "Suite",
        FromAddress = "suite@example.com",
        Password = "secret"
    };

    public static TheoryData<SmtpMailOptions> IncompleteOptions =>
    [
        CompleteOptions with { ServerAddress = null! },
        CompleteOptions with { ServerPort = 0 },
        CompleteOptions with { FromUserName = "" },
        CompleteOptions with { FromAddress = " " },
        CompleteOptions with { Password = null! }
    ];

    [Fact]
    public void Should_be_configured_when_every_value_is_set()
        => CompleteOptions.IsConfigured().Should().BeTrue();

    [Theory]
    [MemberData(nameof(IncompleteOptions), DisableDiscoveryEnumeration = true)]
    public void Should_not_be_configured_when_a_value_is_missing(SmtpMailOptions options)
        => options.IsConfigured().Should().BeFalse();
}
