using Manager.DTOs;
using Manager.Services;
using Microsoft.AspNetCore.Mvc;

namespace Manager.Controllers;

[ApiController]
[Route("internal/api/manager/hash/crack")]
public class InternalManagerController : ControllerBase
{
    private readonly ICrackManagerService crackManagerService;
    
    public InternalManagerController(ICrackManagerService service) => crackManagerService = service;
    
    [HttpPatch("request")]
    public IActionResult Report([FromBody] ReportDto report)
    {
        crackManagerService.ReportResult(report.RequestId, report.FoundWords ?? [], report.WorkerId);
        return Ok();
    }
}