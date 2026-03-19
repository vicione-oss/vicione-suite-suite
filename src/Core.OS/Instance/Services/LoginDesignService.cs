using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.Extensions.Options;

namespace Core.OS.Instance.Services;

public class LoginDesignService(IOptions<InstanceOptions> instanceOptions) : ILoginDesignService
{
    public LoginDesign Design => instanceOptions.Value.LoginDesign;
}

