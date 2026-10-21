using System.Text.Json;
using HostManagement.Shared.Communication.NamedPipe.Client;
using CommunicationJsonContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement;

internal static class PipeResponse
{
    /// <exception cref="HostManagementRequestException">
    /// Thrown when HostManagement answered with a plain response instead of the result <paramref name="request"/> expects.
    /// </exception>
    public static TResponse Deserialize<TResponse>(PipeRequest<TResponse> request, string content)
    {
        try
        {
            return JsonSerializer.Deserialize(content, request.ResponseTypeInfo)
                   ?? throw new JsonException($"Response for topic '{request.Topic}' deserialized to null.");
        }
        catch (JsonException jsonException)
        {
            // HostManagement answers with a plain Response carrying only the reason when it does not know the topic,
            // has no handler for it, or the handler throws or is canceled.
            // A disabled topic gets a typed result with an error status instead.
            throw new HostManagementRequestException(request.Topic, TryReadReason(content), jsonException);
        }
    }

    private static string? TryReadReason(string content)
    {
        try
        {
            return JsonSerializer.Deserialize(content, CommunicationJsonContext.Default.Response)?.Message;
        }
        catch (JsonException)
        {
            return content;
        }
    }
}
