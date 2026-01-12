using Blazor.Shared.Enums;
using ViciOne.Ui.Localization.Resources;

using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.Extensions;

internal static class MessageTypeExtensions
{
    public static SvgIcon ToIcon(this MessageType messageType) => messageType switch
    {
        MessageType.Warning => SvgIcon.RtmWarning,
        MessageType.Error => SvgIcon.RtmError,
        _ => SvgIcon.InfoOutlined
    };

    public static string ToTitle(this MessageType messageType) => messageType switch
    {
        MessageType.Warning => CommonVocabulary.Warning,
        MessageType.Error => CommonVocabulary.Error,
        _ => CommonVocabulary.Information
    };
}
