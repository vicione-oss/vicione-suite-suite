using AngleSharp.Dom;
using Blazor.Shared.Onboarding.Components.WizardPages;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Models;
using Blazor.Shared.UserManagement.Services;
using Blazor.Shared.Wizards.Extensions;
using Bunit;
using Core.Shared.HostManagement;
using Core.Shared.HostManagement.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Configuration;
using HostManagement.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Infrastructure;
using Sdk.Client.NavTiles.Services;
using Sdk.Client.Wizards.Components;
using Sdk.Client.Wizards.Services;
using Sdk.Instance;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using HostnameValidatorTexts = Blazor.Shared.Onboarding.Services.Validators.Localization.HostnameValidator;
using OnboardingWizardBody = Blazor.Shared.Wizards.Components.WizardBody<Blazor.Shared.Onboarding.Models.IOnboardingWizardContext>;

namespace Blazor.Shared.Tests.Onboarding;

/// <summary>
/// Drives the wizard as registered by <see cref="Shared.Onboarding.Extensions.IServiceCollectionExtensions.AddOnboarding"/>
/// with the real engine and handlers; pages are stubbed, as a real page re-renders its container endlessly under bUnit.
/// </summary>
/// <remarks>Page markup is covered by the tests in <c>Components/WizardPages</c>.</remarks>
public sealed class OnboardingWizardTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly IOnboardingStateStore _onboardingStateStore = Substitute.For<IOnboardingStateStore>();
    private readonly ITimeZoneDescriptorProvider _timeZoneDescriptorProvider = Substitute.For<ITimeZoneDescriptorProvider>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly IUiMediator _mediator;
    private readonly OnboardingState _onboardingState;

    private SystemConfiguration _systemConfiguration = SystemConfigurations.CreateDhcp();
    private IEventConsumer<CrossInstanceConfigurationChanged>? _crossInstanceConfigurationChangedConsumer;
    private bool _exited;

    public OnboardingWizardTests()
    {
        IUiMediator? mediator = null;
        IInstanceInformationProvider? instanceInformationProvider = null;

        StubPage<WelcomeWizardPage>();
        StubPage<HostnameWizardPage>();
        StubPage<TimeZoneWizardPage>();
        StubPage<NetworkWizardPage>();
        StubPage<SummaryWizardPage>();

        _ctx.SetupBlazorSharedSettings(setup =>
        {
            mediator = setup.ClientMediator;
            instanceInformationProvider = setup.InstanceInformationProvider;

            setup.Services
                .AddControlPanelInfrastructure()
                .AddWizards()
                .AddOnboarding();
        }).SetLocalServices();

        _mediator = mediator!;
        _onboardingState = new OnboardingState { InstanceId = instanceInformationProvider!.Local.Id };

        _ctx.Services
            .AddSingleton(_onboardingStateStore)
            .AddSingleton(Substitute.For<INavTileRegistry<SharedClientModule>>())
            .AddSingleton(_timeZoneDescriptorProvider)
            .AddKeyedSingleton(Sdk.Constants.ClientTimeProviderServiceKey, _timeProvider)
            .AddSingleton(Substitute.For<IUserService>())
            .AddSingleton(Substitute.For<IAdministratorInitialPasswordProvider>());

        _onboardingStateStore
            .GetOnboardingStateAsync(_onboardingState.InstanceId, Arg.Any<CancellationToken>())
            .Returns(_onboardingState);

        _timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        _timeZoneDescriptorProvider
            .GetTimeZoneDescriptor(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => CreateTimeZoneDescriptor(x.Arg<string>()));
        _timeZoneDescriptorProvider
            .GetAll(Arg.Any<CancellationToken>())
            .Returns([CreateTimeZoneDescriptor(TimeZoneInfo.Utc.Id)]);

        _mediator
            .Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(_ => new GetHostMgmtSystemConfigurationResponse { Configuration = _systemConfiguration.Clone() });

        _mediator.Register(Arg.Do<IEventConsumer<CrossInstanceConfigurationChanged>>(c => _crossInstanceConfigurationChangedConsumer = c));
        _mediator
            .When(x => x.Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(x => _crossInstanceConfigurationChangedConsumer?.Consume(
                ClientContextFactory.Create(new CrossInstanceConfigurationChanged(x.Arg<SetCrossInstanceConfiguration>().CorrelationId,
                    new CrossInstanceConfiguration())),
                CancellationToken.None));
    }

    public ValueTask DisposeAsync()
        => _ctx.DisposeAsync();

    private void StubPage<TPage>() where TPage : IComponent
        => _ctx.ComponentFactories.AddStub<TPage>(parameters => builder =>
        {
            parameters.TryGetValue(nameof(WizardPage<WizardPageState>.OnAfterRenderCycle), out var onAfterRenderCycle);

            builder.OpenComponent<RenderCycleNotifier>(0);
            builder.AddComponentParameter(1, nameof(RenderCycleNotifier.Page), typeof(TPage).Name);
            builder.AddComponentParameter(2, nameof(RenderCycleNotifier.OnAfterRenderCycle), onAfterRenderCycle);
            builder.CloseComponent();
        });

    private static TimeZoneDescriptor CreateTimeZoneDescriptor(string timeZoneId)
        => new(timeZoneId, timeZoneId, TimeSpan.Zero, new Uri("https://localhost/flag.svg"));

    private IRenderedComponent<OnboardingWizardBody> RenderWizard()
        => _ctx.Render<OnboardingWizardBody>(builder => builder
            .Add(c => c.Title, "Onboarding")
            .Add(c => c.OnExit, () => _exited = true));

    private static IElement FindButton(IRenderedComponent<OnboardingWizardBody> wizard, string text)
        => wizard.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private static void ClickWhenEnabled(IRenderedComponent<OnboardingWizardBody> wizard, string text)
    {
        wizard.WaitForAssertion(() => FindButton(wizard, text).IsDisabled().Should().BeFalse());

        FindButton(wizard, text).Click();
    }

    private static IReadOnlyList<string> GetStepTitles(IRenderedComponent<OnboardingWizardBody> wizard)
        => [.. wizard.FindAll(".wizard-stepper > div").Select(step => step.Children[1].TextContent)];

    private static string? GetActivePage(IRenderedComponent<OnboardingWizardBody> wizard)
        => wizard.Find("[data-page]").GetAttribute("data-page");

    [Fact]
    public void Should_list_the_registered_pages_in_order()
    {
        // Act
        var wizard = RenderWizard();

        // Assert
        wizard.WaitForAssertion(() => GetStepTitles(wizard).Should().Equal(
            CommonVocabulary.Welcome,
            TechnicalTerms.Hostname,
            CommonVocabulary.TimeZone,
            CommonVocabulary.Network,
            CommonVocabulary.Summary));
    }

    [Fact]
    public void Should_stay_on_page_and_show_error_when_save_fails()
    {
        // Arrange
        _systemConfiguration = SystemConfigurations.CreateDhcp("-edge");
        var wizard = RenderWizard();

        ClickWhenEnabled(wizard, CommonVocabulary.Next);
        wizard.WaitForAssertion(() => GetActivePage(wizard).Should().Be(nameof(HostnameWizardPage)));

        // Act
        ClickWhenEnabled(wizard, CommonVocabulary.Next);

        // Assert
        var expectedError = string.Format(ValidationMessages.Culture, HostnameValidatorTexts.MustNotStartWithHypenCharacter,
            TechnicalTerms.Hostname);

        wizard.WaitForAssertion(() => wizard.Markup.Should().Contain(expectedError));
        GetActivePage(wizard).Should().Be(nameof(HostnameWizardPage));
        _exited.Should().BeFalse();
    }

    [Fact]
    public void Should_complete_onboarding_when_finishing_the_wizard()
    {
        // Arrange
        var wizard = RenderWizard();

        for (var i = 0; i < 4; i++)
            ClickWhenEnabled(wizard, CommonVocabulary.Next);

        wizard.WaitForAssertion(() => GetActivePage(wizard).Should().Be(nameof(SummaryWizardPage)));

        // Act
        ClickWhenEnabled(wizard, CommonVocabulary.FinishVerb);

        // Assert
        wizard.WaitForAssertion(() => _exited.Should().BeTrue());

        _onboardingState.Completed.Should().BeTrue();
        _mediator.Received(1).Send(Arg.Is<SetCrossInstanceConfiguration>(c => c.TimeZoneId == TimeZoneInfo.Utc.Id),
            Arg.Any<CancellationToken>());
    }

    private sealed class RenderCycleNotifier : ComponentBase
    {
        private bool _notified;

        [Parameter] public string Page { get; set; } = string.Empty;
        [Parameter] public EventCallback<WizardPageAfterRenderCycleEventArgs> OnAfterRenderCycle { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-page", Page);
            builder.CloseElement();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (_notified)
                return;

            _notified = true;

            await OnAfterRenderCycle.InvokeAsync(new WizardPageAfterRenderCycleEventArgs(false));
        }
    }
}
