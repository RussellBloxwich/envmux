using Envmux.Portal;

namespace Envmux.Tests;

public sealed class PortalTerminalTests
{
    [Fact]
    public void ReconnectsTheSameTerminalWithoutAliasingDifferentIds()
    {
        var first = PortalTerminal.LatchId("project", "session", null, "tab.one");
        Assert.Equal(first, PortalTerminal.LatchId("project", "session", null, "tab.one"));
        Assert.NotEqual(first, PortalTerminal.LatchId("project", "session", null, "tab-one"));
        Assert.NotEqual(first, PortalTerminal.LatchId("project", "session", "codex", "tab.one"));
        Assert.NotEqual(first, PortalTerminal.LatchId("project", "other", null, "tab.one"));
        Assert.NotEqual(first, PortalTerminal.LatchId("other", "session", null, "tab.one"));
        Assert.NotEqual(first, PortalTerminal.LatchId("project", "session", null, "tab.one;:$()"));
        Assert.True(PortalTerminal.LatchId("project", "session", null, new string('x', 128)).Length < 100);
        Assert.DoesNotContain('.', first);
        Assert.DoesNotContain(':', first);
        Assert.Equal("project-session-web", PortalTerminal.LatchId("project", "session", null, null));
    }
}
