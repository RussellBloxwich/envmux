using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;

using Envmux.Backends;
using Envmux.Backends.DockerEngine;
using Envmux.Config;
using Envmux.Host;
using Envmux.Incus;
using Envmux.Portal;
using Envmux.Session;
using Envmux.Tests.DockerEngine;

using LiveSession = Envmux.Session.Session;

namespace Envmux.Tests;

/// <summary>The authenticated shell API selects, reconnects and closes the intended tmux terminal.</summary>
public sealed class PortalTerminalHttpTests
{
    [Fact]
    public async Task IndependentSocketsReconnectToTheSameNamedTerminal()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var identity in new[] { "one", "two", "one" })
        {
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", $"Bearer {fixture.Session.Plan.Portal.Token}");
            await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{fixture.Listener.Port}/api/shell?terminal={identity}"), deadline.Token);
            var bytes = new byte[16];
            var read = await socket.ReceiveAsync(bytes, deadline.Token);
            Assert.True(read.Count > 0);
            // The server's output proves it reached the interactive backend;
            // the names below are the commands sent there, not helper outputs.
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "detach", deadline.Token);
        }

        var attachments = fixture.Engine.AllExecs.Where(exec => exec.Create.Tty).ToList();
        Assert.Equal(3, attachments.Count);
        Assert.Equal(attachments[0].CommandLine, attachments[2].CommandLine);
        Assert.NotEqual(attachments[0].CommandLine, attachments[1].CommandLine);
        Assert.All(attachments, exec => Assert.Contains("tmux new-session -A -s", exec.CommandLine, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("codex")]
    public async Task ExplicitCloseUsesAnExactTargetAndCanBeRepeated(string? tool)
    {
        await using var fixture = await Fixture.CreateAsync();
        using var http = fixture.Client();
        var url = $"http://127.0.0.1:{fixture.Listener.Port}/api/shell/close?terminal=one&tool={tool}";
        using var first = await http.PostAsync(url, null);
        using var second = await http.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var latch = PortalTerminal.LatchId(fixture.Session.Plan.Project, fixture.Session.Plan.Session, tool, "one");
        Assert.All(fixture.Engine.AllExecs, exec =>
        {
            Assert.Contains("has-session -t '=" + latch + "'", exec.CommandLine, StringComparison.Ordinal);
            Assert.Contains("kill-session -t '=" + latch + "'", exec.CommandLine, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("", false, HttpStatusCode.Unauthorized)]
    [InlineData("", true, HttpStatusCode.BadRequest)]
    [InlineData("?terminal=one&tool=not-mounted", true, HttpStatusCode.BadRequest)]
    public async Task InvalidOrUnauthorizedCloseNeverRunsAnExec(string query, bool authenticated, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.CreateAsync();
        using var http = authenticated ? fixture.Client() : new HttpClient();
        using var response = await http.PostAsync($"http://127.0.0.1:{fixture.Listener.Port}/api/shell/close{query}", null);
        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(fixture.Engine.AllExecs);
    }

    [Fact]
    public async Task AnExecFailureDoesNotReportTheTerminalClosed()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Engine.OnExec = _ => new FakeExecResult(1);
        using var http = fixture.Client();
        using var response = await http.PostAsync($"http://127.0.0.1:{fixture.Listener.Port}/api/shell/close?terminal=one", null);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private sealed class Fixture(string directory, FakeDockerEngine engine, DockerBackend backend, LiveSession session, PortalListener listener) : IAsyncDisposable
    {
        public FakeDockerEngine Engine { get; } = engine;
        public LiveSession Session { get; } = session;
        public PortalListener Listener { get; } = listener;

        public HttpClient Client()
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Session.Plan.Portal.Token);
            return http;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "envmux-terminal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var plan = SessionPlan.Resolve(JsonSerializer.Deserialize<SessionConfig>("{}", SessionConfig.JsonOptions)!, directory, "terminals");
            var engine = new FakeDockerEngine { ImagesMustExist = false };
            engine.AddNetwork(DockerBackend.Network);
            var spec = InstanceSpec.ForSession(plan, new HostConfig(), false, DateTimeOffset.UnixEpoch);
            await engine.CreateContainerAsync(plan.InstanceName, DockerSpec.ForInstance(spec, DockerBackend.Network, new DockerBackendConfig()));
            await engine.StartAsync(plan.InstanceName);
            engine.OnExecStream = exec =>
            {
                if (!exec.Create.Tty) return null;
                var stream = new ScriptedExecStream();
                stream.Feed("shell ready"u8.ToArray());
                return stream;
            };
            var backend = new DockerBackend(engine, new DockerBackendConfig());
            var session = new LiveSession(plan);
            // Enter after provisioning without adding an adoption API solely
            // for tests. All shell routing and authentication run unchanged.
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LiveSession).GetField("_backend", Flags)!.SetValue(session, backend);
            typeof(LiveSession).GetField("<Address>k__BackingField", Flags)!.SetValue(session, "172.18.0.2");
            var listener = new PortalListener(session);
            await listener.StartAsync(PortSpec.Single(45268));
            return new Fixture(directory, engine, backend, session, listener);
        }

        public async ValueTask DisposeAsync()
        {
            await Listener.DisposeAsync();
            await backend.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }
}
