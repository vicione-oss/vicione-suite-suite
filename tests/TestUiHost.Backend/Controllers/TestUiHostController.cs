using Microsoft.AspNetCore.Mvc;

namespace TestUiHost.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TestUiHostController : ControllerBase
{
    [HttpGet("test")]
    public ActionResult<string> Test()
        => Ok();
}

