using Blazor.Shared.Instance.ControlPanels.Instances;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Core.Shared.Instance.Requests;
using Core.Shared.Mail.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using SmtpTexts = Blazor.Shared.Instance.ControlPanels.Instances.Localization.InstanceControlPanel;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Instances;

public class InstanceControlPanelTests
{
    private const int SmtpSettingsPageIndex = 1;

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetInstances, GetInstancesResponse>(
                Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));
        });
        ctx.Services.AddSingleton(Substitute.For<IBackendLogService>());
        ctx.Services.AddSingleton(Substitute.For<IClientTimeProvider>());

        ctx.SetupControlPanelServices();
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddInstanceControlPanel();

        // The panel has pages, and the real provider needs the registry item that only the settings container cascades.
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());

        // Act
        var component = ctx.Render<InstanceControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        component.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_load_instance_information_if_state_has_id()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetInstances, GetInstancesResponse>(
                Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));

            mediator = setup.ClientMediator;
        });
        ctx.Services.AddSingleton(Substitute.For<IBackendLogService>());
        ctx.Services.AddSingleton(Substitute.For<IClientTimeProvider>());

        var state = new InstanceControlPanelState() { InstanceId = Guid.NewGuid() };

        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddInstanceControlPanel();
        ctx.SetupControlPanelServices();

        // The panel has pages, and the real provider needs the registry item that only the settings container cascades.
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());

        var resetHandler = ctx.Services.GetRequiredKeyedService<IControlPanelResetHandler<InstanceControlPanelState>>(typeof(ControlPanelServiceKey<InstanceControlPanel>));

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        var component = ctx.Render<InstanceControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        component.Should().NotBeNull();
        mediator.Should().NotBeNull();
        await mediator.Received().Request<GetInstances, GetInstancesResponse>(
            Arg.Is<GetInstances>(k => k!.InstanceId == state.InstanceId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_show_the_smtp_settings_under_their_field_names()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        state.BeginSmtpSettingsLoad();
        state.CompleteSmtpSettingsLoad(new SmtpSettings("mail.corp.example", 587, "vicione-master", "noreply@corp.example", "ViciOne Suite", PasswordSet: true, MailSendingEnabled: true), failed: false);

        using var ctx = new BunitContext();

        // Act
        var component = RenderSmtpSettingsPage(ctx, state);

        // Assert
        ReadSettingsFields(component).Should().Equal(
            (TechnicalTerms.Server, "mail.corp.example"),
            (TechnicalTerms.Port, "587"),
            (TechnicalTerms.Username, "vicione-master"),
            (SmtpTexts.SmtpSenderName, "ViciOne Suite"),
            (SmtpTexts.SmtpSenderAddress, "noreply@corp.example"),
            (CommonVocabulary.Password, SmtpTexts.SmtpPasswordSet),
            (CommonVocabulary.Status, SmtpTexts.SmtpMailSendingEnabled));
        component.FindAll(".mail-sending-enabled").Should().ContainSingle();
    }

    [Fact]
    public void Should_leave_unset_smtp_settings_empty()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        state.BeginSmtpSettingsLoad();
        state.CompleteSmtpSettingsLoad(new SmtpSettings("mail.corp.example", 0, " ", null, null, PasswordSet: false, MailSendingEnabled: false), failed: false);

        using var ctx = new BunitContext();

        // Act
        var component = RenderSmtpSettingsPage(ctx, state);

        // Assert
        ReadSettingsFields(component).Should().Equal(
            (TechnicalTerms.Server, "mail.corp.example"),
            (TechnicalTerms.Port, string.Empty),
            (TechnicalTerms.Username, string.Empty),
            (SmtpTexts.SmtpSenderName, string.Empty),
            (SmtpTexts.SmtpSenderAddress, string.Empty),
            (CommonVocabulary.Password, SmtpTexts.SmtpPasswordNotSet),
            (CommonVocabulary.Status, SmtpTexts.SmtpMailSendingDisabled));
        component.FindAll(".mail-sending-disabled").Should().ContainSingle();
    }

    [Fact]
    public void Should_show_the_smtp_settings_fields_as_loading_until_they_are_loaded()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        state.BeginSmtpSettingsLoad();

        using var ctx = new BunitContext();

        // Act
        var component = RenderSmtpSettingsPage(ctx, state);

        // Assert
        component.FindComponents<SettingsField>().Should().HaveCount(7)
            .And.OnlyContain(field => field.Instance.IsLoading);
        component.FindAll(".mail-sending-enabled, .mail-sending-disabled").Should().BeEmpty();
    }

    [Fact]
    public void Should_show_a_placeholder_when_the_instance_has_no_smtp_settings()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        state.BeginSmtpSettingsLoad();
        state.CompleteSmtpSettingsLoad(smtpSettings: null, failed: false);

        using var ctx = new BunitContext();

        // Act
        var component = RenderSmtpSettingsPage(ctx, state);

        // Assert
        component.FindComponents<SettingsField>().Should().BeEmpty();
        component.Markup.Should().Contain(SmtpTexts.SmtpSettingsNotConfigured);
    }

    [Fact]
    public void Should_show_an_error_when_the_smtp_settings_could_not_be_loaded()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        state.BeginSmtpSettingsLoad();
        state.CompleteSmtpSettingsLoad(smtpSettings: null, failed: true);

        using var ctx = new BunitContext();

        // Act
        var component = RenderSmtpSettingsPage(ctx, state);

        // Assert
        component.FindComponents<SettingsField>().Should().BeEmpty();
        component.Markup.Should().Contain(SmtpTexts.SmtpSettingsFetchError);
    }

    private static IRenderedComponent<InstanceControlPanel> RenderSmtpSettingsPage(BunitContext ctx, InstanceControlPanelState state)
    {
        ctx.SetupSuiteServices(_ => { });
        ctx.Services.AddSingleton(Substitute.For<IBackendLogService>());
        ctx.Services.AddSingleton(Substitute.For<IClientTimeProvider>());

        ctx.SetupControlPanelServices();
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddInstanceControlPanel();

        // RenderControlPanelPage drives which page is active, which needs a substitute to steer.
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());

        return ctx.RenderControlPanelPage<InstanceControlPanel, InstanceControlPanelState>(state, SmtpSettingsPageIndex);
    }

    private static IEnumerable<(string Label, string Value)> ReadSettingsFields(IRenderedComponent<InstanceControlPanel> component)
        => component.FindAll(".settings-field")
            .Select(field => (field.QuerySelector(".label")!.TextContent.Trim(), field.QuerySelector(".content-wrapper")!.TextContent.Trim()));
}
