using Manager.DTOs;
using Manager.Services;
using Microsoft.AspNetCore.Mvc;

namespace Manager.Controllers;

[ApiController]
[Route("api/hash")]
public class HashController : ControllerBase
{
    private readonly CrackManagerService _service;

    public HashController(CrackManagerService service) => _service = service;

    [HttpPost("crack")]
    public IActionResult Crack([FromBody] CrackRequest request)
    {
        var id = _service.StartCrack(request);
        return Ok(new { RequestId = id });
    }

    [HttpGet("status")]
    public IActionResult Status([FromQuery] string requestId)
    {
        var (status, data) = _service.GetStatus(requestId);
        return Ok(new { Status = status, Data = data });
    }

}