using Core.OS.DbContext;
using Core.OS.Instance.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Services;

public class NonceStoreTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton<IApplicationDbContext>(_ => TestDbContext)
            .AddSingleton<NonceStore>()
            .BuildServiceProvider();

    public sealed class GetNonce : NonceStoreTests
    {
        [Fact]
        public async Task Should_return_nonce_when_found()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var store = serviceProvider.GetRequiredService<NonceStore>();

            var nonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            TestDbContext.Nonces.Add(nonce);
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var result = await store.GetNonce(nonce.Value, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(nonce);
        }

        [Fact]
        public async Task Should_return_null_when_not_found()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var store = serviceProvider.GetRequiredService<NonceStore>();

            var nonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };

            // Act
            var result = await store.GetNonce(nonce.Value, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }
    }

    public sealed class Create : NonceStoreTests
    {
        [Fact]
        public async Task Should_add_and_save_nonce()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var store = serviceProvider.GetRequiredService<NonceStore>();

            // Act
            var result = await store.Create(TestContext.Current.CancellationToken);

            // Assert
            result.Value.Should().NotBe(Guid.Empty);

            TestDbContext.Nonces.Should().NotBeEmpty();
        }
    }

    public sealed class Delete : NonceStoreTests
    {
        [Fact]
        public async Task Should_remove_nonce_and_return_true_when_one_row_affected()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var store = serviceProvider.GetRequiredService<NonceStore>();

            var nonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            TestDbContext.Nonces.Add(nonce);
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var result = await store.Delete(nonce, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            TestDbContext.Nonces.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_false_when_nonce_does_not_exist()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var store = serviceProvider.GetRequiredService<NonceStore>();
            var nonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };

            // Act
            var result = await store.Delete(nonce, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
        }
    }

    public sealed class DeletedOrphaned : NonceStoreTests
    {
        [Fact]
        public async Task Should_remove_and_save_orphaned_nonces()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var store = serviceProvider.GetRequiredService<NonceStore>();

            var oldNonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) };
            var newNonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            TestDbContext.Nonces.Add(oldNonce);
            TestDbContext.Nonces.Add(newNonce);
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            await store.DeletedOrphaned(TestContext.Current.CancellationToken);

            // Assert
            TestDbContext.Nonces.Should().Contain(newNonce);
            TestDbContext.Nonces.Should().NotContain(oldNonce);
        }

        [Fact]
        public async Task Should_not_remove_when_no_orphaned()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var store = serviceProvider.GetRequiredService<NonceStore>();

            var newNonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            TestDbContext.Nonces.Add(newNonce);
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            await store.DeletedOrphaned(TestContext.Current.CancellationToken);

            // Assert
            TestDbContext.Nonces.Should().Contain(newNonce);
        }
    }
}
