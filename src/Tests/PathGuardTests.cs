using RemoteControlLAN.Agent.Configuration;
using RemoteControlLAN.Agent.Security;
using Xunit;

namespace RemoteControlLAN.Tests;

public sealed class PathGuardTests
{
    [Fact]
    public void ResolveAllowedChild_RejectsTraversalOutsideParent()
    {
        var root = Path.Combine(Path.GetTempPath(), "rclan-tests");
        var guard = new PathGuard(new AgentOptions { AdditionalBlockedPaths = [Path.Combine(root, "blocked")] });
        Assert.Throws<UnauthorizedAccessException>(() => guard.ResolveAllowedChild(root, "../escape.txt"));
    }
    [Fact]
    public void ResolveAllowedPath_RejectsConfiguredBlockedDirectory()
    {
        var blocked = Path.Combine(Path.GetTempPath(), "rclan-blocked");
        var guard = new PathGuard(new AgentOptions { AdditionalBlockedPaths = [blocked] });
        Assert.Throws<UnauthorizedAccessException>(() => guard.ResolveAllowedPath(Path.Combine(blocked, "child.txt")));
    }
}
