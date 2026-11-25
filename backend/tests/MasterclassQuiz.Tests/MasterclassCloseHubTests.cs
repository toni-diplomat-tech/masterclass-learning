using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MasterclassQuiz.Api.HostAuth;
using MasterclassQuiz.Api.Participants;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class MasterclassCloseHubTests(SqlServerFixture sqlServer) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password));

    [Fact]
    public async Task Phones_hear_that_the_host_closed_the_masterclass()
    {
        var host = await HostClient.LoggedInAsync(_app);
        var (id, joinCode) = await HostClient.OpenMasterclassAsync(host);
        await using var phone = Connect(await JoinAsync(joinCode));
        var closed = Channel.CreateUnbounded<JsonObject>();
        phone.On<JsonObject>("MasterclassClosed", message => closed.Writer.TryWrite(message));
        await phone.StartAsync();
        await phone.InvokeAsync<JsonObject>("WatchAsParticipant");

        (await host.PostAsync($"/api/host/masterclasses/{id}/close", null)).EnsureSuccessStatusCode();

        var message = await closed.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        Assert.Equal(id, (int)message["masterclassId"]!);
    }

    private async Task<string> JoinAsync(string joinCode)
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var join = await client.PostAsJsonAsync("/api/participants/join", new { joinCode });
        join.EnsureSuccessStatusCode();
        return join.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(ParticipantEndpoints.CookieName + "=")).Split(';')[0];
    }

    // Long polling: the in-memory test server has no WebSockets for the client to use.
    private HubConnection Connect(string cookie) => new HubConnectionBuilder()
        .WithUrl(new Uri(_app.Server.BaseAddress, "/hubs/quiz"), options =>
        {
            options.HttpMessageHandlerFactory = _ => _app.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
            options.Headers["Cookie"] = cookie;
        })
        .Build();

    public void Dispose() => _app.Dispose();
}
