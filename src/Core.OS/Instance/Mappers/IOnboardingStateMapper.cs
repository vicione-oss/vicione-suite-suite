using Core.Shared.Instance.Contracts;
using Riok.Mapperly.Abstractions;

namespace Core.OS.Instance.Mappers;

[Mapper]
internal static partial class IOnboardingStateMapper
{
    public static partial void MapTo(this IOnboardingState from, IOnboardingState to);
}
