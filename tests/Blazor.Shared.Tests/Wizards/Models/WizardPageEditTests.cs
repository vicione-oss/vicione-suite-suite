using Blazor.Shared.Wizards.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Tests.Wizards.Models;

public sealed class WizardPageEditTests
{
    [Fact]
    public async Task Should_return_canceled_result_and_keep_running_when_save_is_canceled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var saveHandler = Substitute.For<IWizardPageSaveHandler<TestWizardPageState>>();
        saveHandler.Save(Arg.Any<TestWizardPageState>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        services.AddScoped(_ => saveHandler);

        await using var serviceProvider = services.BuildServiceProvider();

        await using var wizardPageEdit = new WizardPageEdit<TestWizardPageState>(new TestWizardPageState(), serviceProvider);
        wizardPageEdit.Begin();

        // Act
        var saveResult = await wizardPageEdit.Save();

        // Assert
        saveResult.Should().BeOfType<SaveCanceledResult>();
        wizardPageEdit.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task Should_return_canceled_result_when_saving_a_disposed_edit()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Substitute.For<IWizardPageSaveHandler<TestWizardPageState>>());

        await using var serviceProvider = services.BuildServiceProvider();

        var wizardPageEdit = new WizardPageEdit<TestWizardPageState>(new TestWizardPageState(), serviceProvider);
        wizardPageEdit.Begin();
        await wizardPageEdit.DisposeAsync();

        // Act
        var saveResult = await wizardPageEdit.Save();

        // Assert
        saveResult.Should().BeOfType<SaveCanceledResult>();
    }

    public sealed class TestWizardPageState : WizardPageState;
}
