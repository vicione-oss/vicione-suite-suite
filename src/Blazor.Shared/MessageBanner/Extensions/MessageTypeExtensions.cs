using Blazor.Shared.Enums;
using ViciOne.Ui.Localization.Resources;

using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.Extensions;

internal static class MessageTypeExtensions
{
    extension(MessageType messageType)
    {
        public SvgIcon ToIcon() => messageType switch
        {
            MessageType.Warning => SvgIcon.RtmWarning,
            MessageType.Error => SvgIcon.RtmError,
            _ => SvgIcon.InfoOutlined
        };

        public string ToTitle() => messageType switch
        {
            MessageType.Warning => CommonVocabulary.Warning,
            MessageType.Error => CommonVocabulary.Error,
            _ => CommonVocabulary.Information
        };
    }
}
