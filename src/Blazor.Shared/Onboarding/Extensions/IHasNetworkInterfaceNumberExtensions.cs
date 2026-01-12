using Blazor.Shared.Onboarding.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Extensions;

internal static class IHasNetworkInterfaceNumberExtensions
{
    public static string GetName(this IHasNetworkInterfaceNumber networkInterface)
        => $"lan{networkInterface.Number}";

    public static string GetFriendlyName(this IHasNetworkInterfaceNumber networkInterface)
        => $"{TechnicalAcronyms.LAN} {networkInterface.Number}";

    public static string GetFriendlyNameVerbose(this IHasNetworkInterfaceNumber networkInterface)
        => $"{networkInterface.GetFriendlyName()} ({networkInterface.GetNetworkDescription()})";

    private static string GetNetworkDescription(this IHasNetworkInterfaceNumber networkInterface)
    {
        if (networkInterface.Number == Constants.LocalNetworkInterfaceNameNumber)
            return TechnicalTerms.LocalNetwork;
        else if (networkInterface.Number == Constants.InternetNetworkInterfaceNameNumber)
            return TechnicalTerms.InternetConnection;
        else
            return CommonVocabulary.Unknown;
    }
}
