using Blazor.Wasm.Client.Infrastructure.Security.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Blazor.Wasm.Backend.Controllers;

[ApiController]
[Route("api/auth/[action]")]
public sealed class SimpleAuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager) : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly SignInManager<IdentityUser> _signInManager = signInManager;

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest model)
    {
        if (string.IsNullOrEmpty(model.UserName) || string.IsNullOrEmpty(model.Password))
            return BadRequest("Valid credentials are needed");

        var user = await _userManager.FindByNameAsync(model.UserName);
        if (user is null) return BadRequest("User does not exist");

        var singInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        if (!singInResult.Succeeded) return BadRequest("Invalid password");

        await _signInManager.SignInAsync(user, model.RememberMe);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }

    [HttpGet]
    public CurrentUser CurrentUserInfo()
    {
        // this is called by wasm client to get session information 
        return new CurrentUser
        {
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
            UserName = User.Identity?.Name ?? "No User",
            Claims = User.Claims.DistinctBy(k => new
            {
                k.Type,
                k.Value
            })
                .ToDictionary(c => c.Type, c => c.Value)
        };
    }
}
