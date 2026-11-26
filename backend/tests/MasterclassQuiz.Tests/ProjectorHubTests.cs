using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MasterclassQuiz.Api.HostAuth;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class ProjectorHubTests(SqlServerFixture sqlServer) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password));

    [Fact]
    public async Task The_projector_sees_the_participant_count_go_up_as_people_join()
    {
        var (host, hostCookie) = await LoggedInHostAsync();
        var (id, joinCode) = await HostClient.OpenMasterclassAsync(host, sqlServer.Connection);
        await using var projector = Connect(hostCookie);
        var counts = Channel.CreateUnbounded<JsonObject>();
        projector.On<JsonObject>("ParticipantCountUpdated", update => counts.Writer.TryWrite(update));
        await projector.StartAsync();

        var watched = await projector.InvokeAsync<JsonObject>("WatchMasterclass", id);
        await _app.CreateClient().PostAsJsonAsync("/api/participants/join", new { joinCode });
        await _app.CreateClient().PostAsJsonAsync("/api/participants/join", new { joinCode });

        Assert.Equal((joinCode, 0), ((string?)watched["joinCode"], (int)watched["participants"]!));
        var first = await counts.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        var second = await counts.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        Assert.Equal((id, 1), ((int)first["masterclassId"]!, (int)first["participants"]!));
        Assert.Equal((id, 2), ((int)second["masterclassId"]!, (int)second["participants"]!));
    }

    [Fact]
    public async Task Only_the_host_can_watch_a_masterclass()
    {
        await using var stranger = Connect(cookie: null);
        await stranger.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => stranger.InvokeAsync<JsonObject>("WatchMasterclass", 1));
    }

    private async Task<(HttpClient Client, string Cookie)> LoggedInHostAsync()
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/host/login", new { password = HostClient.Password });
        login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(HostSession.CookieName + "=")).Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return (client, cookie);
    }

    // Long polling: the in-memory test server has no WebSockets for the client to use.
    private HubConnection Connect(string? cookie) => new HubConnectionBuilder()
        .WithUrl(new Uri(_app.Server.BaseAddress, "/hubs/quiz"), options =>
        {
            options.HttpMessageHandlerFactory = _ => _app.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
            if (cookie is not null)
            {
                options.Headers["Cookie"] = cookie;
            }
        })
        .Build();

    public void Dispose() => _app.Dispose();
}
