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
    /// The username used to sent mails from and which is used to authenticate with the server.
    /// </summary>
    public required string FromUserName { get; init; }

    /// <summary>
    /// Address the mails are sent from.
    /// </summary>
    public required string FromAddress { get; init; }

    /// <summary>
    /// The sender's account password.
    /// </summary>
    public required string Password { get; init; }
}
