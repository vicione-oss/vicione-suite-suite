using Core.OS.DbContext;
using Core.OS.Instance.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Services;

public class OnboardingStateStoreTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    private OnboardingStateStore CreateStore() => new(TestDbContext);

    private void SeedOnboardingState(bool completed, bool showWizardWhenNotCompleted)
    {
        TestDbContext.OnboardingStates.Add(new OnboardingState
        {
            InstanceId = _instanceId,
            Completed = completed,
            ShowWizardWhenNotCompleted = showWizardWhenNotCompleted
        });

        TestDbContext.SaveChanges();
        TestDbContext.ChangeTracker.Clear();
    }

    public sealed class GetOnboardingStateAsync : OnboardingStateStoreTests
    {
        [Fact]
        public async Task Should_return_default_state_when_none_is_stored()
        {
            // Arrange
            using var store = CreateStore();

            // Act
            var state = await store.GetOnboardingStateAsync(_instanceId, TestContext.Current.CancellationToken);

            // Assert
            state.InstanceId.Should().Be(_instanceId);
            state.Completed.Should().BeFalse();
            state.ShowWizardWhenNotCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task Should_return_stored_state()
        {
            // Arrange
            SeedOnboardingState(completed: true, showWizardWhenNotCompleted: false);
            using var store = CreateStore();

            // Act
            var state = await store.GetOnboardingStateAsync(_instanceId, TestContext.Current.CancellationToken);

            // Assert
            state.Completed.Should().BeTrue();
            state.ShowWizardWhenNotCompleted.Should().BeFalse();
        }
    }

    public sealed class GetOnboardingState : OnboardingStateStoreTests
    {
        [Fact]
        public void Should_return_default_state_when_none_is_stored()
        {
            // Arrange
            using var store = CreateStore();

            // Act
            var state = store.GetOnboardingState(_instanceId);

            // Assert
            state.InstanceId.Should().Be(_instanceId);
            state.Completed.Should().BeFalse();
            state.ShowWizardWhenNotCompleted.Should().BeTrue();
        }

        [Fact]
        public void Should_return_stored_state()
        {
            // Arrange
            SeedOnboardingState(completed: true, showWizardWhenNotCompleted: false);
            using var store = CreateStore();

            // Act
            var state = store.GetOnboardingState(_instanceId);

            // Assert
            state.Completed.Should().BeTrue();
            state.ShowWizardWhenNotCompleted.Should().BeFalse();
        }
    }

    public sealed class SetOnboardingStateAsync : OnboardingStateStoreTests
    {
        [Fact]
        public async Task Should_insert_state_when_none_is_stored()
        {
            // Arrange
            using var store = CreateStore();
            var state = new OnboardingState { InstanceId = _instanceId, Completed = true, ShowWizardWhenNotCompleted = false };

            // Act
            await store.SetOnboardingStateAsync(state, TestContext.Current.CancellationToken);

            // Assert
            var stored = await TestDbContext.OnboardingStates.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            stored.Should().BeEquivalentTo(state);
        }

        [Fact]
        public async Task Should_update_stored_state()
        {
            // Arrange
            SeedOnboardingState(completed: false, showWizardWhenNotCompleted: true);
            using var store = CreateStore();
            var state = new OnboardingState { InstanceId = _instanceId, Completed = true, ShowWizardWhenNotCompleted = false };

            // Act
            await store.SetOnboardingStateAsync(state, TestContext.Current.CancellationToken);

            // Assert
            var stored = await TestDbContext.OnboardingStates.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            stored.Should().BeEquivalentTo(state);
        }

        [Fact]
        public async Task Should_return_without_saving_when_cancelled()
        {
            // Arrange
            using var store = CreateStore();
            var state = new OnboardingState { InstanceId = _instanceId, Completed = true };

            // Act
            await store.SetOnboardingStateAsync(state, new CancellationToken(canceled: true));

            // Assert
            (await TestDbContext.OnboardingStates.AnyAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }

        [Fact]
        public async Task Should_return_without_saving_after_dispose()
        {
            // Arrange
            var store = CreateStore();
            store.Dispose();
            var state = new OnboardingState { InstanceId = _instanceId, Completed = true };

            // Act
            await store.SetOnboardingStateAsync(state, TestContext.Current.CancellationToken);

            // Assert
            (await TestDbContext.OnboardingStates.AnyAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }
    }
}
