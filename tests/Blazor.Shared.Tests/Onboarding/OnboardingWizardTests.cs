using System.Collections.Concurrent;
using AngleSharp.Dom;
using Blazor.Shared.Onboarding.Components.WizardPages;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
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
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Sdk.Client.Infrastructure;
using Sdk.Client.NavTiles.Services;
using Sdk.Client.Services;
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
    private readonly IJsInterop _jsInterop;
    private readonly ConcurrentQueue<TaskCompletionSource<IJSVoidResult>> _pendingSlideEnds = new();

    private SystemConfiguration _systemConfiguration = SystemConfigurations.CreateDhcp();
    private IEventConsumer<CrossInstanceConfigurationChanged>? _crossInstanceConfigurationChangedConsumer;
    private bool _exited;

    public OnboardingWizardTests()
    {
        IUiMediator? mediator = null;
        IInstanceInformationProvider? instanceInformationProvider = null;
        IJsInterop? jsInterop = null;

        StubPage<WelcomeWizardPage>();
        StubPage<HostnameWizardPage>();
        StubPage<TimeZoneWizardPage>();
        StubPage<NetworkWizardPage>();
        StubPage<SummaryWizardPage>();

        _ctx.SetupBlazorSharedSettings(setup =>
        {
            mediator = setup.ClientMediator;
            instanceInformationProvider = setup.InstanceInformationProvider;
            jsInterop = setup.JsInterop;

            setup.Services
                .AddControlPanelInfrastructure()
                .AddWizards()
                .AddOnboarding();
        }).SetLocalServices();

        _mediator = mediator!;
        _jsInterop = jsInterop!;
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

    /// <summary>
    /// Slides end only when the test calls <see cref="EndSlide"/>; without this setup every slide ends at once.
    /// </summary>
    private void SetupCarouselModule()
    {
        var carouselModule = Substitute.For<IJSObjectReference>();
#pragma warning disable CA2012 // NSubstitute only records the call here; the returned ValueTask is never consumed.
        carouselModule.InvokeAsync<IJSVoidResult>("waitForTransitionEnd", Arg.Any<CancellationToken>(), Arg.Any<object?[]?>())
            .Returns(_ =>
            {
                var slideEnd = new TaskCompletionSource<IJSVoidResult>();
                _pendingSlideEnds.Enqueue(slideEnd);

                return new ValueTask<IJSVoidResult>(slideEnd.Task);
            });
#pragma warning restore CA2012
        _jsInterop.IncludeModuleScript<SharedClientModule>("carousel.js", Arg.Any<CancellationToken>()).Returns(carouselModule);
    }

    private void WaitForSlide(IRenderedComponent<OnboardingWizardBody> wizard)
        => wizard.WaitForAssertion(() => _pendingSlideEnds.Should().NotBeEmpty());

    private void EndSlide(IRenderedComponent<OnboardingWizardBody> wizard)
    {
        WaitForSlide(wizard);

        _pendingSlideEnds.TryDequeue(out var slideEnd);
        slideEnd!.SetResult(default!);
    }

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

    private static IReadOnlyList<string?> GetMountedPages(IRenderedComponent<OnboardingWizardBody> wizard)
        => [.. wizard.FindAll("[data-page]").Select(page => page.GetAttribute("data-page"))];

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

    [Fact]
    public void Should_keep_previous_page_while_sliding_to_next_step()
    {
        // Arrange
        SetupCarouselModule();
        var wizard = RenderWizard();

        // Act
        ClickWhenEnabled(wizard, CommonVocabulary.Next);

        // Assert
        wizard.WaitForAssertion(() => GetMountedPages(wizard).Should().Equal(nameof(WelcomeWizardPage), nameof(HostnameWizardPage)));
    }

    [Fact]
    public void Should_remove_previous_page_when_slide_ends()
    {
        // Arrange
        SetupCarouselModule();
        var wizard = RenderWizard();

        ClickWhenEnabled(wizard, CommonVocabulary.Next);

        // Act
        EndSlide(wizard);

        // Assert
        wizard.WaitForAssertion(() => GetMountedPages(wizard).Should().Equal(nameof(HostnameWizardPage)));
    }

    [Fact]
    public void Should_keep_next_page_while_sliding_to_previous_step()
    {
        // Arrange
        SetupCarouselModule();
        var wizard = RenderWizard();

        ClickWhenEnabled(wizard, CommonVocabulary.Next);
        EndSlide(wizard);

        // Act
        ClickWhenEnabled(wizard, CommonVocabulary.BackVerb);

        // Assert
        wizard.WaitForAssertion(() => GetMountedPages(wizard).Should().Equal(nameof(WelcomeWizardPage), nameof(HostnameWizardPage)));
    }

    [Fact]
    public void Should_keep_navigation_enabled_while_page_slides()
    {
        // Arrange
        SetupCarouselModule();
        var wizard = RenderWizard();

        // Act
        ClickWhenEnabled(wizard, CommonVocabulary.Next);

        // Assert
        WaitForSlide(wizard);
        wizard.WaitForAssertion(() =>
        {
            FindButton(wizard, CommonVocabulary.BackVerb).IsDisabled().Should().BeFalse();
            FindButton(wizard, CommonVocabulary.Next).IsDisabled().Should().BeFalse();
        });
        GetMountedPages(wizard).Should().Equal(nameof(WelcomeWizardPage), nameof(HostnameWizardPage));
    }

    [Fact]
    public void Should_save_only_active_page_while_previous_page_slides()
    {
        // Arrange
        SetupCarouselModule();
        var wizard = RenderWizard();

        ClickWhenEnabled(wizard, CommonVocabulary.Next);
        EndSlide(wizard);
        ClickWhenEnabled(wizard, CommonVocabulary.Next);
        WaitForSlide(wizard);

        var hostnameWizardPageState = (HostnameWizardPageState)_ctx.Services
            .GetRequiredService<IWizardPageRegistry<IOnboardingWizardContext>>()
            .Single(item => item.ComponentType == typeof(HostnameWizardPage)).State;

        // The hostname page sliding out would now fail to save.
        hostnameWizardPageState.Hostname = "-edge";

        // Act
        ClickWhenEnabled(wizard, CommonVocabulary.Next);

        // Assert
        var hostnameError = string.Format(ValidationMessages.Culture, HostnameValidatorTexts.MustNotStartWithHypenCharacter,
            TechnicalTerms.Hostname);

        wizard.WaitForAssertion(() => GetMountedPages(wizard).Should().Equal(nameof(TimeZoneWizardPage), nameof(NetworkWizardPage)));
        wizard.Markup.Should().NotContain(hostnameError);
    }

    [Fact]
    public async Task Should_jump_to_first_step_when_reset_moves_more_than_one_step()
    {
        // Arrange
        SetupCarouselModule();
        var wizard = RenderWizard();

        for (var i = 0; i < 3; i++)
        {
            ClickWhenEnabled(wizard, CommonVocabulary.Next);
            EndSlide(wizard);
        }

        wizard.WaitForAssertion(() => GetMountedPages(wizard).Should().Equal(nameof(NetworkWizardPage)));

        var pageRegistry = _ctx.Services.GetRequiredService<IWizardPageRegistry<IOnboardingWizardContext>>();

        // Act
        await wizard.InvokeAsync(() => pageRegistry.Remove(item => item.ComponentType == typeof(HostnameWizardPage)));

        // Assert
        wizard.WaitForAssertion(() => GetMountedPages(wizard).Should().Equal(nameof(WelcomeWizardPage)));
        _pendingSlideEnds.Should().BeEmpty();
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
