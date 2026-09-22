using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Worker.DTOs;
using Worker.Services;

namespace Worker.Controllers;

[ApiController]
[Route("internal/api/worker/hash/crack")]
public class WorkerController : ControllerBase
{
    private readonly ICrackService _crackService;

    public WorkerController(ICrackService crackService)
    {
        _crackService = crackService;
    }

    [HttpPost("task")]
    public async Task<IActionResult> RecieveTask([FromBody] WorkerTaskRequest request)
    {
        var results = await _crackService.CrackRangeAsync(
            request.Hash,
            request.MaxLength,
            request.StartIndex,
            request.Count,
            request.Alphabet
        );
        
        await _crackService.ReportResultsAsync(request.RequestId, results, request.WorkerId);
        
        return Ok();
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        var healthStatus = new HealthResponse("ALIVE");
        
        return Ok(healthStatus);
    } 
}
