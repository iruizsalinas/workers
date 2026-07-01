using System.Text;
using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task DurableObjectStateDispatchesStorageAndAlarmOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"value":{"id":7,"name":"Ada"}}""",
            """{"values":{"user:1":{"id":1,"name":"Lin"},"user:2":null}}""",
            "{}",
            "{}",
            """{"deleted":true}""",
            """{"deletedCount":2}""",
            "{}",
            "{}",
            """{"values":{"user:1":{"id":1,"name":"Lin"}}}""",
            """{"scheduledTime":1782734400000}""",
            "{}",
            "{}");
        var state = new DurableObjectState(
            "invocation-do-1",
            new DurableObjectId("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "room-1"),
            dispatcher);

        var user = await state.Storage.GetJsonAsync<UserRow>(
            "user:7",
            new DurableObjectStorageReadOptions { AllowConcurrency = true, NoCache = true });
        var users = await state.Storage.GetJsonAsync<UserRow>(["user:1", "user:2"]);
        await state.Storage.PutJsonAsync(
            "user:8",
            new UserRow { Id = 8, Name = "Grace" },
            new DurableObjectStorageWriteOptions { AllowUnconfirmed = true });
        await state.Storage.PutJsonAsync(
            new Dictionary<string, UserRow>
            {
                ["user:9"] = new() { Id = 9, Name = "Katherine" }
            });
        var deleted = await state.Storage.DeleteAsync("user:8");
        var deletedCount = await state.Storage.DeleteAsync(["user:9", "user:10"]);
        await state.Storage.DeleteAllAsync();
        await state.Storage.SyncAsync();
        var listed = await state.Storage.ListJsonAsync<UserRow>(
            new DurableObjectStorageListOptions
            {
                Prefix = "user:",
                Limit = 10,
                Reverse = true,
                AllowConcurrency = true
            });
        var alarm = await state.Storage.GetAlarmAsync();
        await state.Storage.SetAlarmAsync(alarm!.Value.AddMinutes(5));
        await state.Storage.DeleteAlarmAsync();

        Assert.Equal("room-1", state.Id.Name);
        Assert.NotNull(user);
        Assert.Equal(7, user.Id);
        Assert.Equal("Ada", user.Name);
        Assert.Equal(2, users.Count);
        Assert.Equal("Lin", users["user:1"]!.Name);
        Assert.Null(users["user:2"]);
        Assert.True(deleted);
        Assert.Equal(2, deletedCount);
        Assert.Equal("Lin", listed["user:1"]!.Name);
        Assert.Equal(1782734400000, alarm.Value.ToUnixTimeMilliseconds());
        Assert.Equal(
            [
                "durable.storage.get",
                "durable.storage.getMany",
                "durable.storage.put",
                "durable.storage.putMany",
                "durable.storage.delete",
                "durable.storage.deleteMany",
                "durable.storage.deleteAll",
                "durable.storage.sync",
                "durable.storage.list",
                "durable.storage.getAlarm",
                "durable.storage.setAlarm",
                "durable.storage.deleteAlarm"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var getPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("user:7", getPayload.RootElement.GetProperty("key").GetString());
        Assert.True(getPayload.RootElement.GetProperty("options").GetProperty("allowConcurrency").GetBoolean());
        Assert.True(getPayload.RootElement.GetProperty("options").GetProperty("noCache").GetBoolean());

        using var putPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("Grace", putPayload.RootElement.GetProperty("value").GetProperty("name").GetString());
        Assert.True(putPayload.RootElement.GetProperty("options").GetProperty("allowUnconfirmed").GetBoolean());

        using var listPayload = JsonDocument.Parse(dispatcher.Invocations[8].PayloadJson);
        var listOptions = listPayload.RootElement.GetProperty("options");
        Assert.Equal("user:", listOptions.GetProperty("prefix").GetString());
        Assert.Equal(10, listOptions.GetProperty("limit").GetInt32());
        Assert.True(listOptions.GetProperty("reverse").GetBoolean());

        using var alarmPayload = JsonDocument.Parse(dispatcher.Invocations[10].PayloadJson);
        Assert.Equal(1782734700000, alarmPayload.RootElement.GetProperty("scheduledTime").GetInt64());
    }

    [Fact]
    public async Task DurableObjectContainerDispatchesLifecycleAndExecOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"running":true}""",
            "{}",
            "{}",
            "{}",
            "{}",
            """{"status":201,"headers":[{"name":"x-container","value":"ok"}],"bodyBase64":"c3RhdGU="}""",
            """{"handle":"tcp:container"}""",
            """{"handle":"container-exec:1","pid":42}""",
            """{"stdoutBase64":"aGVsbG8=","stderrBase64":"ZXJy","exitCode":7}""",
            """{"exitCode":7}""",
            "{}",
            "{}");
        var state = new DurableObjectState(
            "invocation-do-container",
            new DurableObjectId("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "container-1"),
            dispatcher);

        var running = await state.Container.GetRunningAsync();
        await state.Container.StartAsync(new ContainerStartOptions
        {
            Env = new Dictionary<string, string> { ["FOO"] = "bar" },
            Entrypoint = ["node", "server.js"],
            EnableInternet = false
        });
        await state.Container.SignalAsync(15);
        await state.Container.MonitorAsync();
        await state.Container.DestroyAsync("done");
        var tcpPort = state.Container.GetTcpPort(8080);
        var portResponse = await tcpPort.FetchAsync(Request.Post(
            "http://container/set-state",
            Body.Text("state")));
        var socket = await tcpPort.ConnectAsync(new SocketAddress("10.0.0.1", 8080));
        var process = await state.Container.ExecAsync(
            ["node", "--version"],
            new ContainerExecOptions
            {
                Cwd = "/app",
                Stdout = "pipe",
                Stderr = "combined",
                Env = new Dictionary<string, string> { ["NODE_ENV"] = "test" },
                User = "worker"
            });
        var output = await process.OutputAsync();
        var exitCode = await process.GetExitCodeAsync();
        await process.KillAsync(9);
        await process.DisposeAsync();

        Assert.True(running);
        Assert.Equal(8080, tcpPort.Port);
        Assert.Equal(201, portResponse.Status);
        Assert.Equal("state", portResponse.Body.AsText());
        Assert.Equal("ok", portResponse.Headers.Get("x-container"));
        Assert.NotNull(socket);
        Assert.Equal(42, process.Pid);
        Assert.Equal("hello", Encoding.UTF8.GetString(output.Stdout));
        Assert.Equal("err", Encoding.UTF8.GetString(output.Stderr));
        Assert.Equal(7, output.ExitCode);
        Assert.Equal(7, exitCode);
        Assert.Equal(
            [
                "durable.container.running",
                "durable.container.start",
                "durable.container.signal",
                "durable.container.monitor",
                "durable.container.destroy",
                "durable.container.tcpPort.fetch",
                "durable.container.tcpPort.connect",
                "durable.container.exec",
                "durable.container.exec.output",
                "durable.container.exec.exitCode",
                "durable.container.exec.kill",
                "durable.container.exec.release"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var startPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        var startOptions = startPayload.RootElement.GetProperty("options");
        Assert.Equal("bar", startOptions.GetProperty("env").GetProperty("FOO").GetString());
        Assert.Equal("node", startOptions.GetProperty("entrypoint")[0].GetString());
        Assert.Equal("server.js", startOptions.GetProperty("entrypoint")[1].GetString());
        Assert.False(startOptions.GetProperty("enableInternet").GetBoolean());

        using var signalPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal(15, signalPayload.RootElement.GetProperty("signal").GetInt32());

        using var destroyPayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal("done", destroyPayload.RootElement.GetProperty("error").GetString());

        using var fetchPayload = JsonDocument.Parse(dispatcher.Invocations[5].PayloadJson);
        Assert.Equal(8080, fetchPayload.RootElement.GetProperty("port").GetInt32());
        Assert.Equal(
            "http://container/set-state",
            fetchPayload.RootElement.GetProperty("fetch").GetProperty("request").GetProperty("url").GetString());
        Assert.Equal(
            "POST",
            fetchPayload.RootElement.GetProperty("fetch").GetProperty("request").GetProperty("method").GetString());

        using var connectPayload = JsonDocument.Parse(dispatcher.Invocations[6].PayloadJson);
        Assert.Equal(8080, connectPayload.RootElement.GetProperty("port").GetInt32());
        Assert.Equal("10.0.0.1", connectPayload.RootElement.GetProperty("address").GetProperty("hostname").GetString());
        Assert.Equal(8080, connectPayload.RootElement.GetProperty("address").GetProperty("port").GetInt32());

        using var execPayload = JsonDocument.Parse(dispatcher.Invocations[7].PayloadJson);
        Assert.Equal("node", execPayload.RootElement.GetProperty("command")[0].GetString());
        Assert.Equal("--version", execPayload.RootElement.GetProperty("command")[1].GetString());
        var execOptions = execPayload.RootElement.GetProperty("options");
        Assert.Equal("/app", execOptions.GetProperty("cwd").GetString());
        Assert.Equal("pipe", execOptions.GetProperty("stdout").GetString());
        Assert.Equal("combined", execOptions.GetProperty("stderr").GetString());
        Assert.Equal("test", execOptions.GetProperty("env").GetProperty("NODE_ENV").GetString());
        Assert.Equal("worker", execOptions.GetProperty("user").GetString());

        using var killPayload = JsonDocument.Parse(dispatcher.Invocations[10].PayloadJson);
        Assert.Equal("container-exec:1", killPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal(9, killPayload.RootElement.GetProperty("signal").GetInt32());
    }

    [Fact]
    public async Task DurableObjectContainerDispatchesOutboundInterceptors()
    {
        var dispatcher = new CapturingDispatcher(
            """{"handle":"rpc:entrypoint"}""",
            "{}",
            "{}",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-do-container-intercept");
        var worker = await environment.Service("PROXY").InvokeStubAsync("entrypoint");
        var state = new DurableObjectState(
            "invocation-do-container-intercept",
            new DurableObjectId("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "container-1"),
            dispatcher);

        await state.Container.InterceptOutboundHttpAsync("api.example.com", worker);
        await state.Container.InterceptAllOutboundHttpAsync(worker);
        await state.Container.InterceptOutboundHttpsAsync("*.example.com", worker);

        Assert.Equal(
            [
                "service.rpcStub",
                "durable.container.interceptOutboundHttp",
                "durable.container.interceptAllOutboundHttp",
                "durable.container.interceptOutboundHttps"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.Equal("PROXY", dispatcher.Invocations[0].BindingName);
        Assert.All(dispatcher.Invocations.Skip(1), call => Assert.Equal("$durableObjectState", call.BindingName));

        using var httpPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("api.example.com", httpPayload.RootElement.GetProperty("target").GetString());
        Assert.Equal("rpc:entrypoint", httpPayload.RootElement.GetProperty("workerHandle").GetString());

        using var allHttpPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("rpc:entrypoint", allHttpPayload.RootElement.GetProperty("workerHandle").GetString());

        using var httpsPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("*.example.com", httpsPayload.RootElement.GetProperty("target").GetString());
        Assert.Equal("rpc:entrypoint", httpsPayload.RootElement.GetProperty("workerHandle").GetString());
    }

    [Fact]
    public async Task DurableObjectContainerRejectsInvalidSignalsAndCommands()
    {
        var dispatcher = new CapturingDispatcher("{}");
        var state = new DurableObjectState(
            "invocation-do-container-invalid",
            new DurableObjectId("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "container-invalid"),
            dispatcher);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => state.Container.SignalAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => state.Container.SignalAsync(65));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Container.GetTcpPort(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Container.GetTcpPort(65536));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            state.Container.InterceptOutboundHttpAsync("", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            state.Container.InterceptAllOutboundHttpAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            state.Container.InterceptOutboundHttpsAsync(" ", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => state.Container.ExecAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() => state.Container.ExecAsync(["node", ""]));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task DurableObjectKvStorageDispatchesSynchronousKvOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"value":{"id":7,"name":"Ada"}}""",
            "{}",
            """{"deleted":true}""",
            """{"values":{"user:7":{"id":7,"name":"Ada"},"missing":null}}""");
        var state = new DurableObjectState(
            "invocation-do-kv",
            new DurableObjectId("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "room-1"),
            dispatcher);

        var user = await state.Storage.Kv.GetJsonAsync<UserRow>("user:7");
        await state.Storage.Kv.PutJsonAsync("user:8", new UserRow { Id = 8, Name = "Grace" });
        var deleted = await state.Storage.Kv.DeleteAsync("user:8");
        var listed = await state.Storage.Kv.ListJsonAsync<UserRow>(
            new DurableObjectKvListOptions
            {
                Prefix = "user:",
                Limit = 10,
                Reverse = true
            });

        Assert.NotNull(user);
        Assert.Equal(7, user.Id);
        Assert.Equal("Ada", user.Name);
        Assert.True(deleted);
        Assert.Equal("Ada", listed["user:7"]!.Name);
        Assert.Null(listed["missing"]);
        Assert.Equal(
            [
                "durable.storage.kv.get",
                "durable.storage.kv.put",
                "durable.storage.kv.delete",
                "durable.storage.kv.list"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var getPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("user:7", getPayload.RootElement.GetProperty("key").GetString());

        using var putPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("user:8", putPayload.RootElement.GetProperty("key").GetString());
        Assert.Equal("Grace", putPayload.RootElement.GetProperty("value").GetProperty("name").GetString());

        using var listPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        var listOptions = listPayload.RootElement.GetProperty("options");
        Assert.Equal("user:", listOptions.GetProperty("prefix").GetString());
        Assert.Equal(10, listOptions.GetProperty("limit").GetInt32());
        Assert.True(listOptions.GetProperty("reverse").GetBoolean());
    }
}
