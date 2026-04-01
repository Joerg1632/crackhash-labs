using Manager.DTOs;
using Manager.Services;
using Microsoft.AspNetCore.Mvc;

namespace Manager.Controllers;

[ApiController]
[Route("api/hash")]
public class HashController : ControllerBase
{
    private readonly ICrackManagerService service;

    public HashController(ICrackManagerService service) => this.service = service;

    [HttpPost("crack")]
    public IActionResult Crack([FromBody] CrackRequest request)
    {
        var requestId = service.StartCrack(request);
        var crackRequestId = new CrackRequestResponse(requestId);
        
        return Ok(crackRequestId);
    }

    [HttpGet("status")]
    public IActionResult Status([FromQuery] string requestId)
    {
        
        var (status, data) = service.GetStatus(requestId);
        var crackStatus = new CrackStatusResponse(status,  data);

        return Ok(crackStatus);
    }
}