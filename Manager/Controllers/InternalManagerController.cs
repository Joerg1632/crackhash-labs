using Manager.DTOs;
using Manager.Services;
using Microsoft.AspNetCore.Mvc;

namespace Manager.Controllers;

[ApiController]
[Route("internal/api/manager/hash/crack")]
public class InternalManagerController : ControllerBase
{
    private readonly ICrackManagerService service;
    
    public InternalManagerController(ICrackManagerService service) => this.service = service;
    
    [HttpPatch("request")]
    public IActionResult Report([FromBody] ReportDto report)
    {
        service.ReportResult(report.RequestId, report.FoundWords ?? [], report.WorkerId);
        return Ok();
    }
}