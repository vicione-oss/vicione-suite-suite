namespace Core.OS.Mail.Extensions;

internal static class SmtpMailOptionsExtensions
{
    extension(SmtpMailOptions options)
    {
        /// <summary>
        /// Whether every value needed to send mail is set. A partly filled section still binds, because the binder
        /// does not enforce the <c>required</c> members.
        /// </summary>
        public bool IsConfigured()
            => options.ServerPort != 0
               && new[] { options.ServerAddress, options.FromUserName, options.FromAddress, options.Password }
                   .All(value => !string.IsNullOrWhiteSpace(value));
    }
}
