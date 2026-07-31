using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemoteControlLAN.Gateway.Data;

namespace RemoteControlLAN.Gateway.Controllers;

[ApiController, Authorize, Route("api/audit")]
public sealed class AuditController(AppDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IResult> List() => Results.Ok(await db.AuditLogs.OrderByDescending(x => x.Timestamp).Take(200).ToListAsync());
}
