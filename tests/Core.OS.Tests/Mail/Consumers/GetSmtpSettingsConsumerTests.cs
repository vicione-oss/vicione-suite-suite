using Core.OS.Mail;
using Core.OS.Mail.Consumers;
using Core.Shared.Mail;
using Core.Shared.Mail.Contracts;
using Core.Shared.Mail.Requests;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Mail.Consumers;

public sealed class GetSmtpSettingsConsumerTests
{
    private static Action<IBusRegistrationConfigurator> ConfigureServices(IOptions<SmtpMailOptions> options, bool mailSendingEnabled = false)
        => cfg =>
        {
            var mailSenderStatus = Substitute.For<IMailSenderStatus>();
            mailSenderStatus.IsConfigured().Returns(mailSendingEnabled);

            cfg.AddConsumer<GetSmtpSettingsConsumer>();
            cfg.AddSingleton(options);
            cfg.AddSingleton(mailSenderStatus);
        };

    /// <summary>
    /// Binds the options the way the suite does, so a missing section turns into default values as it does there.
    /// </summary>
    private static IOptions<SmtpMailOptions> BindOptions(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddOptions<SmtpMailOptions>().Bind(configuration.GetSection(SmtpMailOptions.ConfigSection));

        return services.BuildServiceProvider().GetRequiredService<IOptions<SmtpMailOptions>>();
    }

    [Fact]
    public async Task Should_map_all_settings_from_smtp_section()
    {
        // Arrange
        var options = BindOptions(new()
        {
            ["Smtp:ServerAddress"] = "smtp.example.com",
            ["Smtp:ServerPort"] = "587",
            ["Smtp:UserName"] = "suite",
            ["Smtp:FromAddress"] = "suite@example.com",
            ["Smtp:FromName"] = "ViciOne Suite",
            ["Smtp:Password"] = "secret",
        });

        await using var tester = new MassTransitTester(ConfigureServices(options, mailSendingEnabled: true));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetSmtpSettingsResponse, GetSmtpSettings>(new GetSmtpSettings());

        // Assert
        response.Settings.Should().Be(new SmtpSettings("smtp.example.com", 587, "suite", "suite@example.com", "ViciOne Suite", PasswordSet: true, MailSendingEnabled: true));
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_no_settings_when_smtp_section_is_missing()
    {
        // Arrange
        var options = BindOptions(new() { ["Other:Key"] = "value" });

        await using var tester = new MassTransitTester(ConfigureServices(options));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetSmtpSettingsResponse, GetSmtpSettings>(new GetSmtpSettings());

        // Assert
        response.Settings.Should().BeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_no_settings_when_smtp_section_has_no_values()
    {
        // Arrange
        var options = BindOptions(new()
        {
            ["Smtp:ServerAddress"] = "",
            ["Smtp:ServerPort"] = "0",
            ["Smtp:Password"] = " ",
        });

        await using var tester = new MassTransitTester(ConfigureServices(options));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetSmtpSettingsResponse, GetSmtpSettings>(new GetSmtpSettings());

        // Assert
        response.Settings.Should().BeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_map_only_present_values_when_smtp_section_is_partial()
    {
        // Arrange
        var options = BindOptions(new() { ["Smtp:ServerAddress"] = "smtp.example.com" });

        await using var tester = new MassTransitTester(ConfigureServices(options));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetSmtpSettingsResponse, GetSmtpSettings>(new GetSmtpSettings());

        // Assert
        response.Settings.Should().Be(new SmtpSettings("smtp.example.com", 0, null, null, null, PasswordSet: false, MailSendingEnabled: false));
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_not_read_the_removed_from_user_name_key()
    {
        // Arrange
        var options = BindOptions(new()
        {
            ["Smtp:ServerAddress"] = "smtp.example.com",
            ["Smtp:FromUserName"] = "suite",
        });

        await using var tester = new MassTransitTester(ConfigureServices(options));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetSmtpSettingsResponse, GetSmtpSettings>(new GetSmtpSettings());

        // Assert
        response.Settings.Should().NotBeNull();
        response.Settings!.UserName.Should().BeNull();
    }

    [Fact]
    public async Task Should_report_mail_sending_as_disabled_when_the_mail_sender_is_not_configured()
    {
        // Arrange
        var options = BindOptions(new()
        {
            ["Smtp:ServerAddress"] = "smtp.example.com",
            ["Smtp:Password"] = "secret",
        });

        await using var tester = new MassTransitTester(ConfigureServices(options, mailSendingEnabled: false));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetSmtpSettingsResponse, GetSmtpSettings>(new GetSmtpSettings());

        // Assert
        response.Settings.Should().NotBeNull();
        response.Settings!.PasswordSet.Should().BeTrue();
        response.Settings.MailSendingEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_return_error_response_on_failure()
    {
        // Arrange
        var options = Substitute.For<IOptions<SmtpMailOptions>>();
        options.Value.Throws(new InvalidOperationException("test failure"));

        await using var tester = new MassTransitTester(ConfigureServices(options));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetSmtpSettingsResponse, GetSmtpSettings>(new GetSmtpSettings());

        // Assert
        response.Settings.Should().BeNull();
        response.RequestError.Should().NotBeNull();
        response.RequestError!.ErrorCode.Should().Be(MailErrorCodes.GetSmtpSettingsFailed);
        response.RequestError.Message.Should().Be("test failure");
    }

    [Fact]
    public void Should_carry_no_password_value_in_smtp_settings()
    {
        // Act
        var stringPropertyNames = typeof(SmtpSettings).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name);

        // Assert
        stringPropertyNames.Should().NotContain(name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }
}
