using Microsoft.Extensions.Options;
using RemoteControlLAN.Agent.Commands;
using RemoteControlLAN.Agent.Configuration;
using RemoteControlLAN.Agent.Platform;
using RemoteControlLAN.Agent.Security;
using RemoteControlLAN.Agent.Services;
using RemoteControlLAN.Agent.Transfers;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
var settings = builder.Configuration.GetSection("Agent").Get<AgentOptions>() ?? new AgentOptions();
if (!Guid.TryParse(settings.AgentId, out _) || string.IsNullOrWhiteSpace(settings.AgentSecretKey)) throw new InvalidOperationException("Hãy cấu hình Agent:AgentId và Agent:AgentSecretKey từ endpoint /api/agents.");
builder.Services.AddSingleton(settings); builder.Services.AddSingleton<PathGuard>(); builder.Services.AddSingleton<ProcessGuard>(); builder.Services.AddSingleton<FileTransferService>();
var platform = new PlatformServiceFactory().Create(); builder.Services.AddSingleton(platform.Screen); builder.Services.AddSingleton(platform.Webcam); builder.Services.AddSingleton(platform.Keyboard); builder.Services.AddSingleton(platform.Power); builder.Services.AddSingleton(platform.Launcher); builder.Services.AddSingleton(platform.Notifications);
builder.Services.AddSingleton<AgentCommandDispatcher>(); builder.Services.AddSingleton<GatewayConnection>(); builder.Services.AddSingleton<AgentProcessor>(); builder.Services.AddHostedService<AgentWorker>();
await builder.Build().RunAsync();
