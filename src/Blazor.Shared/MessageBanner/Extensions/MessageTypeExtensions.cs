using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.MessageBanner.Extensions;

internal static class MessageTypeExtensions
{
    extension(MessageType messageType)
    {
        public MonochromeIconName ToIcon() => messageType switch
        {
            MessageType.Warning => MonochromeIconName.WarningLight,
            MessageType.Error => MonochromeIconName.ErrorLight,
            _ => MonochromeIconName.InfoLight
        };

        public string ToTitle() => messageType switch
        {
            MessageType.Warning => CommonVocabulary.Warning,
            MessageType.Error => CommonVocabulary.Error,
            _ => CommonVocabulary.Information
        };
    }
}
