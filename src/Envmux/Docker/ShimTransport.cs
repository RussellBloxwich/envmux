using Envmux.Host;

namespace Envmux.Docker;

/// <summary>The endpoint could not be offered, or a platform has no transport yet.</summary>
internal sealed class ShimException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// One client connection to the endpoint: a duplex stream, and a way to say
/// "nothing more from this side" without closing it.
/// </summary>
/// <remarks>
/// The half-close is the whole reason this is an interface rather than a
/// <see cref="Stream"/>. A hijacked exec ends when the shim has no more
/// output, and the client reads that as EOF — but it may still be sending,
/// and the connection has to stay up until it has read the end. On a socket
/// that is <c>shutdown(SHUT_WR)</c>; on a Windows message pipe it is a
/// zero-length message. Reads returning zero mean the client did the same
/// thing in the other direction, which for a non-tty exec is stdin's EOF.
/// </remarks>
internal interface IShimConnection : IAsyncDisposable
{
    Stream Stream { get; }

    /// <summary>EOF towards the client. The connection stays open for the client to finish reading and close.</summary>
    ValueTask CompleteWriteAsync(CancellationToken ct);
}

/// <summary>Where the endpoint is offered, one platform at a time.</summary>
internal interface IShimListener : IAsyncDisposable
{
    /// <summary>What <c>DOCKER_HOST</c> should be set to, to reach this.</summary>
    string Address { get; }

    /// <summary>The next client, or null once the listener is being taken down.</summary>
    ValueTask<IShimConnection?> AcceptAsync(CancellationToken ct);
}

/// <summary>
/// The endpoint's identity per platform, and the listener that serves it.
/// </summary>
/// <remarks>
/// <para>
/// Windows is served, by <see cref="Windows.MessagePipeListener"/>: a named
/// pipe, per user, in message mode — the mode is not optional, see that class.
/// </para>
/// <para>
/// macOS and Linux are not served yet. The shape is decided: a unix socket
/// under <see cref="HostConfig.Directory"/> at mode 0600, where half-close is
/// the socket's own <c>shutdown</c> and the same <see cref="IShimListener"/>
/// contract applies. docs/vscode-remote.md §3.1 records it; nothing here
/// pretends to it.
/// </para>
/// </remarks>
internal static class ShimEndpoint
{
    public const string PipeName = "envmux-docker";

    public const string SocketFileName = "docker.sock";

    public static string UnixSocketPath => Path.Combine(HostConfig.Directory, SocketFileName);

    /// <summary>The <c>DOCKER_HOST</c> for this machine, whether or not anything is serving it yet.</summary>
    public static string DockerHost =>
        OperatingSystem.IsWindows()
            ? $"npipe:////./pipe/{PipeName}"
            : $"unix://{UnixSocketPath}";

    /// <summary>
    /// Whether something is serving the endpoint right now.
    /// </summary>
    /// <remarks>
    /// A pipe under <c>\\.\pipe\</c> exists exactly while a server instance is
    /// listening; a unix socket file exists once bound. Cheap, and it does not
    /// consume a server instance the way a probe connection would — so the
    /// editor path can check before it launches and say what to run if nothing
    /// is up.
    /// </remarks>
    public static bool IsServed()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return Directory.EnumerateFiles(@"\\.\pipe\")
                    .Any(p => string.Equals(Path.GetFileName(p), PipeName, StringComparison.OrdinalIgnoreCase));
            }
            catch (IOException)
            {
                return false;
            }
        }

        return File.Exists(UnixSocketPath);
    }

    public static IShimListener Listen()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Windows.MessagePipeListener(PipeName);
        }

        throw new ShimException(
            "the Docker endpoint is served on Windows only so far. On macOS and Linux it will be a " +
            $"unix socket at {UnixSocketPath} — docs/vscode-remote.md §3.1 has the shape; the listener is not written.");
    }
}
