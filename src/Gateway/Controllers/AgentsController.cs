using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RemoteControlLAN.Gateway.Data;
using RemoteControlLAN.Gateway.Services;

namespace RemoteControlLAN.Gateway.Controllers;

[ApiController, Authorize, Route("api/agents")]
public sealed class AgentsController(IAgentProvisioningService provisioning, AppDbContext db) : ControllerBase
{
    [HttpGet] public IResult List() => Results.Ok(db.Agents.Select(a => new { id = a.Id, name = a.AgentName, platform = a.Platform, lastOnlineAt = a.LastOnlineAt }).OrderBy(x => x.name));
    [HttpPost] public async Task<IResult> Create(CreateAgentRequest request) { var agent = await provisioning.CreateAsync(request.AgentName, request.Platform); return agent is null ? Results.Conflict(new { message = "Tên Agent đã tồn tại hoặc không hợp lệ." }) : Results.Created($"api/agents/{agent.AgentId}", agent); }
}
public sealed record CreateAgentRequest(string AgentName, string Platform);
