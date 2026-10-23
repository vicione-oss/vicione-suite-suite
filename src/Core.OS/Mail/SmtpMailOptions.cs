namespace Core.OS.Mail;

public record SmtpMailOptions
{
    public const string ConfigSection = "Smtp";

    /// <summary>
    /// The servers network DNS or IP address.
    /// </summary>
    public required string ServerAddress { get; init; }

    /// <summary>
    /// The servers port.
    /// </summary>
    public required int ServerPort { get; init; }

    /// <summary>
    /// The name used to log in to the server; often, but not always, an email address.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Address the mails are sent from.
    /// </summary>
    public required string FromAddress { get; init; }

    /// <summary>
    /// Display name in front of <see cref="FromAddress"/>; without it, mails show the bare address.
    /// </summary>
    public string? FromName { get; init; }

    /// <summary>
    /// The sender's account password.
    /// </summary>
    public required string Password { get; init; }
}
