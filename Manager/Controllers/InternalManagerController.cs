using Manager.DTOs;
using Manager.Services;
using Microsoft.AspNetCore.Mvc;

namespace Manager.Controllers;

[ApiController]
[Route("internal/api/manager/hash/crack")]
public class InternalManagerController : ControllerBase
{
    private readonly CrackManagerService _service;
    
    public InternalManagerController(CrackManagerService service) => _service = service;
    
    [HttpPatch("request")]
    public IActionResult Report([FromBody] ReportDto report)
    {
        _service.ReportResult(report.RequestId, report.FoundWords ?? new(), report.WorkerId);
        return Ok();
    }
}