using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using loans_service.Audit;

namespace loans_service.Controllers;

[ApiController]
[Authorize]
[Route("audit")]
public class AuditController(AuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Search(
        [FromQuery] Guid? aggregateId,
        [FromQuery] string? eventType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (hits, total) = await auditService.SearchAsync(
            aggregateId?.ToString(),
            eventType,
            from,
            to,
            page,
            pageSize,
            cancellationToken);

        return Ok(new { hits, total });
    }
}
