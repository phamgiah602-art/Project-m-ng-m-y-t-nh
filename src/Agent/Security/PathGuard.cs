using System.Runtime.InteropServices;
using RemoteControlLAN.Agent.Configuration;

namespace RemoteControlLAN.Agent.Security;

public sealed class PathGuard(AgentOptions options)
{
    private readonly StringComparison _comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public string ResolveAllowedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new UnauthorizedAccessException("Đường dẫn không hợp lệ.");
        var fullPath = Path.GetFullPath(path);
        foreach (var blocked in BlockedPaths()) if (IsSameOrChild(fullPath, blocked)) throw new UnauthorizedAccessException("Đường dẫn không hợp lệ.");
        return fullPath;
    }
    public string ResolveAllowedChild(string parentPath, string fileName)
    {
        if (Path.GetFileName(fileName) != fileName) throw new UnauthorizedAccessException("Tên file không hợp lệ.");
        var parent = ResolveAllowedPath(parentPath); var full = ResolveAllowedPath(Path.Combine(parent, fileName));
        if (!IsSameOrChild(full, parent)) throw new UnauthorizedAccessException("Đường dẫn không hợp lệ."); return full;
    }
    private IEnumerable<string> BlockedPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaults = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData" }
            : new[] { "/System", "/Library", "/usr", "/bin", "/sbin", "/private", Path.Combine(home, "Library") };
        return defaults.Append(AppContext.BaseDirectory).Concat(options.AdditionalBlockedPaths).Select(Path.GetFullPath);
    }
    private bool IsSameOrChild(string candidate, string parent)
    {
        var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return string.Equals(candidate, normalizedParent, _comparison) || candidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, _comparison) || candidate.StartsWith(normalizedParent + Path.AltDirectorySeparatorChar, _comparison);
    }
}
