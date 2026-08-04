using Core.OS.Connections;
using Sdk.Connections.Contracts;

namespace Core.OS.Tests.Connections;

public class HttpConnectionTestTests
{
    public sealed class Test : HttpConnectionTestTests
    {
        [Fact]
        public async Task Should_return_failure_when_connection_is_not_http_connection()
        {
            // Arrange
            var connection = Substitute.For<IConnection>();
            var sut = new HttpConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert            
            result.Success.Should().BeFalse();
            result.ErrorInfo.Should().NotBeNull();
            result.ErrorInfo.ErrorCode.Should().Be(500);
            result.ErrorInfo.Message.Should().Contain("Invalid connection type");
        }

        [Fact]
        public async Task Should_return_failure_when_base_address_is_invalid()
        {
            // Arrange
            var connection = new HttpConnection { BaseAddress = "not a valid uri" };
            var sut = new HttpConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorInfo.Should().NotBeNull();
            result.ErrorInfo.Message.Should().StartWith("HTTP connection test failed");
        }

        [Fact]
        public async Task Should_return_success_for_valid_reachable_address()
        {
            // Arrange
            var connection = new HttpConnection { BaseAddress = "https://example.com" };
            var sut = new HttpConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue();
            result.ErrorInfo.Should().BeNull();
        }

        [Fact]
        public async Task Should_return_failure_when_status_code_is_not_success()
        {
            // Arrange
            var connection = new HttpConnection { BaseAddress = "https://ifm.not-exists.com/notfoundpage" };
            var sut = new HttpConnectionTest();

            // Act
            var result = await sut.Test(connection, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorInfo.Should().NotBeNull();
            result.ErrorInfo.ErrorCode.Should().Be(500);
        }
    }
}
