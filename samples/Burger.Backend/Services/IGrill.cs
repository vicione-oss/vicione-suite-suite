using Burger.Internal.Contracts;

namespace Burger.Backend.Services;

public interface IGrill
{
    Task<BurgerPatty> CookOrUseExistingPatty(decimal weight, bool cheese);
    void Add(BurgerPatty patty);
}
