using System.Security.Cryptography;
using System.Text;

using Envmux.Incus;

namespace Envmux.Portal;

/// <summary>Stable tmux identities for independent portal terminals.</summary>
/// <remarks>
/// Slugging client IDs aliases punctuation variants. Hashing keeps arbitrary
/// IDs distinct without introducing tmux target separators or unbounded names.
/// Omitting the ID retains the original shared shell for existing clients.
/// </remarks>
internal static class PortalTerminal
{
    public static string LatchId(string project, string session, string? tool, string? terminal)
    {
        var task = tool is { Length: > 0 } ? $"tool-{tool}" : "web";
        if (terminal is not null)
        {
            task += "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terminal))).ToLowerInvariant()[..32];
        }

        return Latch.Id(project, session, task);
    }
}
