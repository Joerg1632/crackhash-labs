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
        var requestId = _service.StartCrack(request);

        var crackRequestId = new CrackRequestResponse()
        {
            RequestId = requestId,
        };
        return Ok(crackRequestId);
    }

    [HttpGet("status")]
    public IActionResult Status([FromQuery] string requestId)
    {
        
        var (status, data) = _service.GetStatus(requestId);
        
        var crackStatus = new CrackStatusResponse()
        {
            Status = status,
            Data = data
        };
        return Ok(crackStatus);
    }
}