using System.Text.Json;
using Core.OS.HostManagement;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Communication.NamedPipe.Client;

namespace Core.OS.Tests.HostManagement;

public sealed class PipeResponseTests
{
    [Fact]
    public void Should_return_the_typed_result()
    {
        // Arrange
        var content = JsonSerializer.Serialize(
            new SystemControlResult { Status = OperationStatus.Success, Message = "Restarting" },
            SourceGenerationContext.Default.SystemControlResult);

        // Act
        var result = PipeResponse.Deserialize(PipeRequests.RestartSystem, content);

        // Assert
        result.Status.Should().Be(OperationStatus.Success);
        result.Message.Should().Be("Restarting");
    }

    [Fact]
    public void Should_pass_a_typed_error_result_on_without_throwing()
    {
        // Arrange
        var content = JsonSerializer.Serialize(
            new SystemControlResult { Status = OperationStatus.Error, Message = "Topic is disabled" },
            SourceGenerationContext.Default.SystemControlResult);

        // Act
        var result = PipeResponse.Deserialize(PipeRequests.RestartSystem, content);

        // Assert
        result.Status.Should().Be(OperationStatus.Error);
        result.Message.Should().Be("Topic is disabled");
    }

    [Fact]
    public void Should_throw_with_the_reason_when_host_management_answers_with_a_plain_response()
    {
        // Arrange
        var content = JsonSerializer.Serialize(new Response { Message = "Handler failed" }, SourceGenerationContext.Default.Response);

        // Act
        var act = () => PipeResponse.Deserialize(PipeRequests.RestartSystem, content);

        // Assert
        var exception = act.Should().Throw<HostManagementRequestException>().Which;
        exception.Topic.Should().Be(PipeRequests.RestartSystem.Topic);
        exception.Reason.Should().Be("Handler failed");
        exception.Message.Should().Contain("Handler failed");
        exception.InnerException.Should().BeOfType<JsonException>();
    }

    [Fact]
    public void Should_throw_with_the_content_as_reason_when_the_response_is_no_json()
    {
        // Act
        var act = () => PipeResponse.Deserialize(PipeRequests.RestartSystem, "Ok");

        // Assert
        act.Should().Throw<HostManagementRequestException>().Which.Reason.Should().Be("Ok");
    }

    [Fact]
    public void Should_throw_when_the_response_is_null()
    {
        // Act
        var act = () => PipeResponse.Deserialize(PipeRequests.RestartSystem, "null");

        // Assert
        act.Should().Throw<HostManagementRequestException>();
    }
}
