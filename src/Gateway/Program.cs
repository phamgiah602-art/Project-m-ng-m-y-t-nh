using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RemoteControlLAN.Gateway.Data;
using RemoteControlLAN.Gateway.Options;
using RemoteControlLAN.Gateway.Repositories;
using RemoteControlLAN.Gateway.Services;
using RemoteControlLAN.Gateway.WebSockets;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Host.UseSerilog((context, services, config) => config.ReadFrom.Configuration(context.Configuration).WriteTo.File("logs/gateway-.log", rollingInterval: RollingInterval.Day));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? throw new InvalidOperationException("Thiếu cấu hình Jwt.");
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 ký tự.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=remotecontrol.db"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.MapInboundClaims = true; o.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), ValidateLifetime = true, ClockSkew = TimeSpan.Zero }; o.Events = new JwtBearerEvents { OnMessageReceived = context => { var token = context.Request.Query["access_token"]; if (!string.IsNullOrEmpty(token) && context.HttpContext.WebSockets.IsWebSocketRequest) context.Token = token; return Task.CompletedTask; } }; });
builder.Services.AddAuthorization(); builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddScoped<IUserRepository, UserRepository>(); builder.Services.AddScoped<IAgentRepository, AgentRepository>(); builder.Services.AddScoped<ISessionRepository, SessionRepository>(); builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuthService, AuthService>(); builder.Services.AddScoped<IPairingService, PairingService>(); builder.Services.AddScoped<IAuditService, AuditService>(); builder.Services.AddScoped<IAgentProvisioningService, AgentProvisioningService>();
builder.Services.AddSingleton<ConnectionManager>(); builder.Services.AddSingleton<MessageRouter>(); builder.Services.AddSingleton<WebSocketEndpoint>(); builder.Services.AddHostedService<HeartbeatService>();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) { await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(); }
app.UseSerilogRequestLogging(); app.UseCors(); app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) }); app.UseAuthentication(); app.UseAuthorization();
app.MapControllers(); app.Map("/ws", context => context.RequestServices.GetRequiredService<WebSocketEndpoint>().HandleAsync(context)); app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
