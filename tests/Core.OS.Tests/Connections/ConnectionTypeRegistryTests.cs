using Core.OS.Connections;
using Sdk.Connections.Contracts;

namespace Core.OS.Tests.Connections;

public class ConnectionTypeRegistryTests
{
    private readonly IConnectionSerializer _connectionSerializer = Substitute.For<IConnectionSerializer>();
    private readonly IConnection _connection = Substitute.For<IConnection>();

    public sealed class Register : ConnectionTypeRegistryTests
    {
        [Fact]
        public void Should_register_new_connection_type_successfully()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();
            var test = Substitute.For<IConnectionTest>();

            // Act
            registry.Register<IConnection, IConnectionSerializer>(
                "myType",
                () => _connection,
                _connectionSerializer,
                test);

            // Assert
            registry.GetConnectionTypes().Should().Contain("myType");
        }

        [Fact]
        public void Should_throw_if_id_already_registered()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();
            registry.Register<IConnection, IConnectionSerializer>("dup", () => _connection, _connectionSerializer, null);

            // Act
            Action act = () => registry.Register<IConnection, IConnectionSerializer>("dup", () => _connection, _connectionSerializer, null);

            // Assert
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("A component with ID 'dup' is already registered.");
        }
    }

    public sealed class GetConnectionTypes : ConnectionTypeRegistryTests
    {
        [Fact]
        public void Should_return_all_registered_keys()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();
            var serializer = Substitute.For<IConnectionSerializer>();
            registry.Register<IConnection, IConnectionSerializer>("t1", () => Substitute.For<IConnection>(), serializer, null);
            registry.Register<IConnection, IConnectionSerializer>("t2", () => Substitute.For<IConnection>(), serializer, null);

            // Act
            var types = registry.GetConnectionTypes();

            // Assert
            types.Should().BeEquivalentTo(["t1", "t2"]);
        }
    }

    public sealed class TryCreateConnection : ConnectionTypeRegistryTests
    {
        [Fact]
        public void Should_return_true_and_create_connection_when_id_exists()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();
            var connection = Substitute.For<IConnection>();
            registry.Register<IConnection, IConnectionSerializer>("c1", () => connection, Substitute.For<IConnectionSerializer>(), null);

            // Act
            var success = registry.TryCreateConnection("c1", out var created);

            // Assert
            success.Should().BeTrue();
            created.Should().Be(connection);
        }

        [Fact]
        public void Should_return_false_and_null_when_id_not_found()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();

            // Act
            var success = registry.TryCreateConnection("missing", out var created);

            // Assert
            success.Should().BeFalse();
            created.Should().BeNull();
        }
    }

    public sealed class TryGetConnectionSerializer : ConnectionTypeRegistryTests
    {
        [Fact]
        public void Should_return_true_and_serializer_when_found()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();
            var serializer = Substitute.For<IConnectionSerializer>();
            registry.Register<IConnection, IConnectionSerializer>("s1", () => Substitute.For<IConnection>(), serializer, null);

            // Act
            var success = registry.TryGetConnectionSerializer("s1", out var found);

            // Assert
            success.Should().BeTrue();
            found.Should().Be(serializer);
        }

        [Fact]
        public void Should_return_false_and_null_when_not_found()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();

            // Act
            var success = registry.TryGetConnectionSerializer("x", out var found);

            // Assert
            success.Should().BeFalse();
            found.Should().BeNull();
        }
    }

    public sealed class TryGetConnectionTest : ConnectionTypeRegistryTests
    {
        [Fact]
        public void Should_return_true_and_test_when_found()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();
            var test = Substitute.For<IConnectionTest>();
            registry.Register<IConnection, IConnectionSerializer>("t1", () => _connection, _connectionSerializer, test);

            // Act
            var success = registry.TryGetConnectionTest("t1", out var found);

            // Assert
            success.Should().BeTrue();
            found.Should().Be(test);
        }

        [Fact]
        public void Should_return_false_and_null_when_missing_or_null_in_registry()
        {
            // Arrange
            var registry = new ConnectionTypeRegistry();
            registry.Register<IConnection, IConnectionSerializer>("noTest", () => _connection, _connectionSerializer, null);

            // Act
            var success = registry.TryGetConnectionTest("noTest", out var found);

            // Assert
            success.Should().BeFalse();
            found.Should().BeNull();
        }
    }
}
