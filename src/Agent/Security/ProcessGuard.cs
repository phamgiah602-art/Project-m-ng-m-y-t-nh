using System.Diagnostics;
using System.Runtime.InteropServices;
using RemoteControlLAN.Agent.Configuration;

namespace RemoteControlLAN.Agent.Security;

public sealed class ProcessGuard(AgentOptions options)
{
    public void EnsureCanStop(Process process)
    {
        var defaults = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new[] { "system", "svchost", "csrss", "wininit", "services", "lsass" } : new[] { "kernel_task", "launchd", "windowserver", "loginwindow" };
        var blocked = defaults.Concat(options.AdditionalProtectedProcesses).Append(Process.GetCurrentProcess().ProcessName);
        if (blocked.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Tiến trình hệ thống được bảo vệ.");
    }
}
