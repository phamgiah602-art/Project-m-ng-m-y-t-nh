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
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 ký tự. Hãy đặt key bí mật trong appsettings.Local.json hoặc biến môi trường Jwt__Key.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=remotecontrol.db"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = true;
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.HttpContext.WebSockets.IsWebSocketRequest)
            {
                var protocols = context.Request.Headers.SecWebSocketProtocol.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var bearerIndex = Array.FindIndex(protocols, p => p.Equals("bearer", StringComparison.OrdinalIgnoreCase));
                if (bearerIndex >= 0 && protocols.Length > bearerIndex + 1)
                {
                    context.Token = protocols[bearerIndex + 1];
                }
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPairingService, PairingService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAgentProvisioningService, AgentProvisioningService>();

builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<WebSocketEndpoint>();
builder.Services.AddHostedService<HeartbeatService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    
    // Seed / unlock admin user
    var bootstrapUsername = builder.Configuration["BootstrapAdmin:Username"]?.Trim();
    var bootstrapPassword = builder.Configuration["BootstrapAdmin:Password"];
    var adminUsername = !string.IsNullOrWhiteSpace(bootstrapUsername) ? bootstrapUsername : "admin";
    var adminPassword = !string.IsNullOrWhiteSpace(bootstrapPassword) && bootstrapPassword.Length >= 8 ? bootstrapPassword : "Admin@123";
    var adminUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);
    if (adminUser == null)
    {
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<RemoteControlLAN.Gateway.Models.AppUser>();
        adminUser = new RemoteControlLAN.Gateway.Models.AppUser { Username = adminUsername, IsAdmin = true };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, adminPassword);
        await dbContext.Users.AddAsync(adminUser);
        await dbContext.SaveChangesAsync();
        app.Logger.LogInformation("Đã tạo tài khoản admin mặc định ({Username} / {Password})", adminUsername, adminPassword);
    }
    else
    {
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<RemoteControlLAN.Gateway.Models.AppUser>();
        bool changed = false;
        if (!adminUser.IsAdmin) { adminUser.IsAdmin = true; changed = true; }
        if (adminUser.FailedLoginCount > 0) { adminUser.FailedLoginCount = 0; changed = true; }
        if (adminUser.LockedUntil != null) { adminUser.LockedUntil = null; changed = true; app.Logger.LogInformation("Đã mở khoá tài khoản admin '{Username}'.", adminUsername); }
        if (hasher.VerifyHashedPassword(adminUser, adminUser.PasswordHash, adminPassword) == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            adminUser.PasswordHash = hasher.HashPassword(adminUser, adminPassword);
            changed = true;
            app.Logger.LogInformation("Đã khôi phục mật khẩu mặc định cho '{Username}' ({Password}).", adminUsername, adminPassword);
        }
        if (changed) await dbContext.SaveChangesAsync();
    }
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.Map("/ws", context => context.RequestServices.GetRequiredService<WebSocketEndpoint>().HandleAsync(context));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
