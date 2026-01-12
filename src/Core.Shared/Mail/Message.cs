namespace Core.Shared.Mail;

public record Message
{
    public required string RecipientAddress { get; init; }

    public required string Subject { get; init; }

    public string? Body { get; init; }
}
