using Blazor.Shared.Onboarding.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Extensions;

internal static class IHasNetworkInterfaceNumberExtensions
{
    extension(IHasNetworkInterfaceNumber networkInterface)
    {
        public string GetName()
            => $"lan{networkInterface.Number}";

        public string GetFriendlyName()
            => $"{TechnicalAcronyms.LAN} {networkInterface.Number}";

        public string GetFriendlyNameVerbose()
            => $"{networkInterface.GetFriendlyName()} ({networkInterface.GetNetworkDescription()})";

        private string GetNetworkDescription()
        {
            return networkInterface.Number switch
            {
                Constants.LocalNetworkInterfaceNameNumber => TechnicalTerms.LocalNetwork,
                Constants.InternetNetworkInterfaceNameNumber => TechnicalTerms.InternetConnection,
                _ => CommonVocabulary.Unknown
            };
        }
    }
}
