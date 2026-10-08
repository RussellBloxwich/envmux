using System.Net;
using System.Net.Http.Headers;
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

/// <summary>Restart through the session and HTTP boundary, with the engine held at real transition points.</summary>
public sealed class SessionRestartLifecycleTests
{
    [Fact]
    public async Task HttpRestartKeepsCredentialsAndDispatchesTheReloadedTasks()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var listener = new PortalListener(fixture.Session);
        await listener.StartAsync(PortSpec.Single(45267));
        using var http = new HttpClient();
        var credentials = fixture.Session.Plan.Portal;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Token);
        await fixture.ConfigAsync("echo changed");

        using var restart = await http.PostAsync($"http://127.0.0.1:{listener.Port}/api/restart", null);
        Assert.Equal(HttpStatusCode.NoContent, restart.StatusCode);
        Assert.Equal("echo changed", Assert.Single(fixture.Session.Tasks).Plan.Display);
        Assert.True(fixture.Session.IsReady);
        Assert.Null(fixture.Session.FailedWith);
        Assert.Equal(credentials.Token, fixture.Session.Plan.Portal.Token);
        Assert.Equal(credentials.RoomToken, fixture.Session.Plan.Portal.RoomToken);
        using var state = await http.GetAsync($"http://127.0.0.1:{listener.Port}/api/state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
    }

    [Fact]
    public async Task ReloadStartsAnAddedServiceWithoutRecreatingTheWorkspace()
    {
        await using var fixture = await Fixture.CreateAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Session.Plan.Directory, ".envmux.json"),
            """{"services":{"cache":{"type":"redis"}},"tasks":{"work":{"command":"echo changed","autostart":false}}}""");

        await fixture.Session.RestartAsync();

        var service = Assert.Single(fixture.Session.Plan.Services);
        Assert.True(fixture.Engine.Container(service.InstanceName)!.Running);
        Assert.Equal(2, fixture.Engine.Created.Count);
        Assert.Equal("echo changed", Assert.Single(fixture.Session.Tasks).Plan.Display);
        Assert.True(fixture.Session.IsReady);
    }

    [Fact]
    public async Task ASecondRestartIsRefusedWithoutDisposingTheFirstRestartsTasks()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (entered, release) = fixture.HoldTaskStop();
        var first = fixture.Session.RestartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            var error = await Assert.ThrowsAsync<SessionException>(() => fixture.Session.RestartAsync());
            Assert.Contains("already restarting", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            release.TrySetResult();
        }

        await first.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(fixture.Session.Tasks);
        Assert.True(fixture.Session.IsReady);
    }

    [Fact]
    public async Task TeardownWaitsForRestartBeforeDisposingItsBackend()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (entered, release) = fixture.HoldTaskStop();
        var restart = fixture.Session.RestartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stop = fixture.Session.StopAsync();
        Assert.False(stop.IsCompleted);
        Assert.False(fixture.Engine.Disposed);
        release.SetResult();

        await Task.WhenAll(restart, stop).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(fixture.Engine.Disposed);
        await Assert.ThrowsAsync<SessionException>(() => fixture.Session.RestartAsync());
    }

    [Fact]
    public async Task FailedResumeIsVisibleOverHttpAndCanBeRetried()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Engine.StopAsync(fixture.Session.Plan.InstanceName);
        fixture.Engine.Intercept = (operation, _) =>
        {
            if (operation == "Start")
            {
                throw new BackendException("resume refused");
            }

            return Task.CompletedTask;
        };
        await using var listener = new PortalListener(fixture.Session);
        await listener.StartAsync(PortSpec.Single(45267));
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Session.Plan.Portal.Token);
        var endpoint = $"http://127.0.0.1:{listener.Port}/api/restart";

        using var failure = await http.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
        Assert.False(fixture.Session.IsReady);
        Assert.Equal("resume refused", fixture.Session.FailedWith);
        fixture.Engine.Intercept = null;
        using var retry = await http.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.True(fixture.Session.IsReady);
        Assert.Null(fixture.Session.FailedWith);
        Assert.Single(fixture.Engine.Created);
    }

    private sealed class Fixture(string directory, FakeDockerEngine engine, DockerBackend backend, LiveSession session) : IAsyncDisposable
    {
        public FakeDockerEngine Engine { get; } = engine;
        public LiveSession Session { get; } = session;

        public Task ConfigAsync(string command) => File.WriteAllTextAsync(Path.Combine(directory, ".envmux.json"),
            JsonSerializer.Serialize(new { tasks = new { work = new { command, autostart = false } } }));

        public static async Task<Fixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "envmux-restart-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            const string Config = """{"tasks":{"work":{"command":"echo before","autostart":false}}}""";
            await File.WriteAllTextAsync(Path.Combine(directory, ".envmux.json"), Config);
            var plan = SessionPlan.Resolve(JsonSerializer.Deserialize<SessionConfig>(Config, SessionConfig.JsonOptions)!, directory, "restart");
            var engine = new FakeDockerEngine { ImagesMustExist = false };
            engine.AddNetwork(DockerBackend.Network);
            var spec = InstanceSpec.ForSession(plan, new HostConfig(), false, DateTimeOffset.UnixEpoch);
            await engine.CreateContainerAsync(plan.InstanceName, DockerSpec.ForInstance(spec, DockerBackend.Network, new DockerBackendConfig()));
            await engine.StartAsync(plan.InstanceName);
            var backend = new DockerBackend(engine, new DockerBackendConfig());
            var session = new LiveSession(plan);
            // StartAsync also provisions SSH, tools and a Git bundle. These
            // tests enter after that boundary without adding a test-only
            // adoption API to the application.
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LiveSession).GetField("_backend", Flags)!.SetValue(session, backend);
            typeof(LiveSession).GetField("_retainedInstance", Flags)!.SetValue(session, await backend.Instances.GetAsync(plan.InstanceName));
            await session.RestartAsync();
            return new Fixture(directory, engine, backend, session);
        }

        public (TaskCompletionSource Entered, TaskCompletionSource Release) HoldTaskStop()
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Engine.Intercept = async (operation, _) =>
            {
                if (operation == "ExecCreate")
                {
                    entered.TrySetResult();
                    await release.Task;
                }
            };
            return (entered, release);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var task in Session.Tasks)
            {
                task.Dispose();
            }

            await backend.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }
}
