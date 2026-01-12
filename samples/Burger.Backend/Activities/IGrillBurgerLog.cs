using Burger.Internal.Contracts;

namespace Burger.Backend.Activities;

public interface IGrillBurgerLog
{
    BurgerPatty Patty { get; }
}
