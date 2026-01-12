using Microsoft.AspNetCore.Mvc;

namespace TestModule.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TestController : ControllerBase
{
    [HttpGet("test")]
    public ActionResult<string> Test()
        => Ok();
}

